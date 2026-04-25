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
        RaiseCountChanged();
    }

    public bool CanDispatch() => _activePigs.Count < _config.MaxSimultaneousPigsOnPath;

    private void AddActivePig(PigEntity pig)
    {
        _activePigs.Add(pig);
        RaiseCountChanged();
    }

    private void RemoveActivePig(PigEntity pig)
    {
        _activePigs.Remove(pig);
        RaiseCountChanged();
    }

    private void RaiseCountChanged()
    {
        if (_eventBus != null) _eventBus.Raise(new ActivePigCountChanged { Count = _activePigs.Count });
    }

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
        AddActivePig(pig);

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

            float segmentDuration = _config.PigLapDuration / PerimeterTrack.FiringSegmentCount;
            bool depleted = false;

            for (int seg = 0; seg < PerimeterTrack.FiringSegmentCount && !depleted; seg++)
            {
                pig.CurrentSegment = seg;
                var targetRot = _track.SegmentRotations[seg];
                var nextRot = seg + 1 < PerimeterTrack.FiringSegmentCount
                    ? _track.SegmentRotations[seg + 1]
                    : targetRot;

                int totalCells = _track.GetSegmentCellCount(seg);
                int lastFiredColIndex = -1;
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

                    int currentColIndex = Mathf.Clamp(Mathf.FloorToInt(t * totalCells), 0, totalCells - 1);
                    while (lastFiredColIndex < currentColIndex)
                    {
                        lastFiredColIndex++;
                        if (!pig.HasAmmo) { depleted = true; break; }
                        float syntheticProgress = (lastFiredColIndex + 0.5f) / totalCells;
                        if (_track.GetLineOfSight(seg, syntheticProgress, out var startCell, out var dir))
                        {
                            _shooting.TryFire(pig, startCell, dir);
                        }
                    }
                    if (depleted) break;

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }

            if (depleted || !pig.HasAmmo)
            {
                RemoveActivePig(pig);
                _eventBus.Raise(new PigDepleted { PigId = pig.Id });
                DestroyPig(pig);
                return;
            }

            pig.State = PigState.Returning;
            _eventBus.Raise(new PigLapCompleted { PigId = pig.Id, AmmoLeft = pig.Ammo });

            if (_shelf.TryFindEmptySlot(out var freeIdx))
            {
                pig.Origin = PigOrigin.Shelf;
                pig.ShelfSlotIndex = freeIdx;
                _shelf.TryPlaceAtSlot(freeIdx, pig);
                Vector3 slot = _shelf.GetSlotPosition(freeIdx);
                pig.MeshTransform.DOLocalRotateQuaternion(PigEntity.IdleLocalRotation, 0.4f).SetEase(Ease.InOutCubic);
                await pig.transform.DOMove(slot, 0.4f).SetEase(Ease.InOutCubic).AwaitCompletion(ct);
                pig.State = PigState.Idle;
                RemoveActivePig(pig);
                _eventBus.Raise(new PigReturnedToShelf { PigId = pig.Id, SlotIndex = freeIdx });
            }
            else
            {
                RemoveActivePig(pig);
                _eventBus.Raise(new ShelfOverflowFail { PigId = pig.Id });
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
