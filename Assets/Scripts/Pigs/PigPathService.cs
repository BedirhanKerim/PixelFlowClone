using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using VContainer;

public class PigPathService : MonoBehaviour
{
    [Inject] private GameEventBus _eventBus;
    [Inject] private GameConfig _config;
    [Inject] private ShootingService _shooting;
    [Inject] private ShelfService _shelf;
    [Inject] private QueueService _queue;

    private PerimeterTrack _track;
    private readonly List<PigEntity> _activePigs = new List<PigEntity>();
    private readonly Dictionary<int, CancellationTokenSource> _pigTokens = new Dictionary<int, CancellationTokenSource>();
    private CancellationTokenSource _lifetimeCts;

    private const float FireInterval = 0.05f;

    public int ActivePigCount => _activePigs.Count;

    public void Bind(PerimeterTrack track) => _track = track;

    private void Awake()
    {
        _lifetimeCts = new CancellationTokenSource();
    }

    private void OnDestroy()
    {
        _lifetimeCts?.Cancel();
        _lifetimeCts?.Dispose();
        foreach (var kvp in _pigTokens) { kvp.Value.Cancel(); kvp.Value.Dispose(); }
        _pigTokens.Clear();
    }

    public void ClearAll()
    {
        foreach (var kvp in _pigTokens) { kvp.Value.Cancel(); kvp.Value.Dispose(); }
        _pigTokens.Clear();
        _activePigs.Clear();
    }

    public bool CanDispatch() => _activePigs.Count < _config.MaxSimultaneousPigsOnPath;

    private void DestroyPig(PigEntity pig)
    {
        pig.State = PigState.Depleted;
        pig.transform.DOKill();
        pig.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack)
            .OnComplete(() => { if (pig != null) Destroy(pig.gameObject); });
    }

    public void Dispatch(PigEntity pig, Vector3 sourcePosition)
    {
        if (!CanDispatch()) return;
        pig.State = PigState.Dispatched;
        _activePigs.Add(pig);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        _pigTokens[pig.Id] = cts;
        RunPigAsync(pig, sourcePosition, cts.Token).Forget();
    }

    private async UniTask RunPigAsync(PigEntity pig, Vector3 sourcePos, CancellationToken ct)
    {
        try
        {
            Vector3 entry = _track.GetEntryPoint();
            pig.MeshTransform.rotation = _track.SegmentRotations[0];
            await pig.transform.DOMove(entry, 0.35f).SetEase(Ease.InOutCubic).AwaitCompletion(ct);

            pig.State = PigState.OnPath;
            pig.CurrentSegment = 0;
            pig.CurrentSegmentProgress = 0f;
            _eventBus.Raise(new PigEnteredPath { PigId = pig.Id, Entry = entry });

            float segmentDuration = _config.PigLapDuration * 0.25f;
            float fireCooldown = 0f;
            bool depleted = false;

            for (int seg = 0; seg < 4 && !depleted; seg++)
            {
                pig.CurrentSegment = seg;
                var targetRot = _track.SegmentRotations[seg];
                var nextRot = _track.SegmentRotations[(seg + 1) % 4];

                float elapsed = 0f;
                while (elapsed < segmentDuration)
                {
                    if (ct.IsCancellationRequested) return;
                    if (!pig.HasAmmo)
                    {
                        depleted = true;
                        break;
                    }

                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / segmentDuration);
                    pig.CurrentSegmentProgress = t;
                    pig.transform.position = _track.GetWorldPosition(seg, t);

                    float rotBlend = Mathf.InverseLerp(0.8f, 1f, t);
                    pig.MeshTransform.rotation = Quaternion.Slerp(targetRot, nextRot, rotBlend);

                    fireCooldown -= Time.deltaTime;
                    if (fireCooldown <= 0f)
                    {
                        if (_track.GetLineOfSight(seg, t, out var startCell, out var dir))
                        {
                            if (_shooting.TryFire(pig, startCell, dir))
                            {
                                fireCooldown = FireInterval;
                            }
                        }
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }

            if (depleted || !pig.HasAmmo)
            {
                _activePigs.Remove(pig);
                if (pig.Origin == PigOrigin.Shelf) _shelf.ReleaseReservation(pig.ShelfSlotIndex);
                _eventBus.Raise(new PigDepleted { PigId = pig.Id });
                DestroyPig(pig);
                return;
            }

            pig.State = PigState.Returning;
            _eventBus.Raise(new PigLapCompleted { PigId = pig.Id, AmmoLeft = pig.Ammo });

            if (pig.Origin == PigOrigin.Shelf)
            {
                Vector3 slot = _shelf.GetSlotPosition(pig.ShelfSlotIndex);
                await pig.transform.DOMove(slot, 0.4f).SetEase(Ease.InOutCubic).AwaitCompletion(ct);
                _shelf.ReturnDispatchedPig(pig);
                pig.State = PigState.Idle;
                pig.ResetMeshToIdle();
                _activePigs.Remove(pig);
                _eventBus.Raise(new PigReturnedToShelf { PigId = pig.Id, SlotIndex = pig.ShelfSlotIndex });
            }
            else
            {
                if (_shelf.TryFindEmptySlot(out var freeIdx))
                {
                    pig.Origin = PigOrigin.Shelf;
                    pig.ShelfSlotIndex = freeIdx;
                    _shelf.TryPlaceAtSlot(freeIdx, pig);
                    Vector3 slot = _shelf.GetSlotPosition(freeIdx);
                    await pig.transform.DOMove(slot, 0.4f).SetEase(Ease.InOutCubic).AwaitCompletion(ct);
                    pig.State = PigState.Idle;
                    pig.ResetMeshToIdle();
                    _activePigs.Remove(pig);
                    _eventBus.Raise(new PigReturnedToShelf { PigId = pig.Id, SlotIndex = freeIdx });
                }
                else
                {
                    _activePigs.Remove(pig);
                    _eventBus.Raise(new ShelfOverflowFail { PigId = pig.Id });
                }
            }
        }
        finally
        {
            if (_pigTokens.TryGetValue(pig.Id, out var cts))
            {
                _pigTokens.Remove(pig.Id);
                cts.Dispose();
            }
        }
    }
}
