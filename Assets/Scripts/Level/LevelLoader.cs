using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

public class LevelLoader : MonoBehaviour
{
    [Inject] private GameEventBus _eventBus;
    [Inject] private GameConfig _config;
    [Inject] private LevelLibrary _library;
    [Inject] private LevelProgress _progress;
    [Inject] private GridModel _model;
    [Inject] private GridRenderer _renderer;
    [Inject] private PigFactory _pigFactory;
    [Inject] private ShelfService _shelf;
    [Inject] private QueueService _queue;
    [Inject] private PigPathService _pathService;

    [SerializeField] private Transform _shelfRoot;
    [SerializeField] private Transform _queueRoot;
    [SerializeField] private Transform _activePigsRoot;

    private PerimeterTrack _track;
    private CancellationTokenSource _cts;

    private void Start()
    {
        _cts = new CancellationTokenSource();
        EnsureRoots();
        _track = new PerimeterTrack();
        _pathService.Bind(_track);
        LoadCurrent();
    }

    private void OnEnable()
    {
        if (_eventBus == null) return;
        _eventBus.SubscribeTo<LevelCompleted>(OnLevelCompleted);
        _eventBus.SubscribeTo<LevelFailed>(OnLevelFailed);
    }

    private void OnDisable()
    {
        if (_eventBus == null) return;
        _eventBus.UnsubscribeFrom<LevelCompleted>(OnLevelCompleted);
        _eventBus.UnsubscribeFrom<LevelFailed>(OnLevelFailed);
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private void OnLevelCompleted(ref LevelCompleted e)
    {
        AdvanceAfterDelay(_config.LevelRevealDuration + 1.0f).Forget();
    }

    private void OnLevelFailed(ref LevelFailed e)
    {
        ReloadAfterDelay(1.5f).Forget();
    }

    private async UniTaskVoid AdvanceAfterDelay(float delay)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: _cts.Token);
        if (_library != null && _library.Levels != null && _progress.CurrentIndex < _library.Levels.Length - 1)
        {
            _progress.Advance();
        }
        LoadCurrent();
    }

    private async UniTaskVoid ReloadAfterDelay(float delay)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: _cts.Token);
        LoadCurrent();
    }

    private void EnsureRoots()
    {
        if (_shelfRoot == null) _shelfRoot = CreateRoot("ShelfRoot");
        if (_queueRoot == null) _queueRoot = CreateRoot("QueueRoot");
        if (_activePigsRoot == null) _activePigsRoot = CreateRoot("ActivePigsRoot");
    }

    private Transform CreateRoot(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform.parent, false);
        return go.transform;
    }

    public void LoadCurrent()
    {
        if (_library == null || _library.Levels == null || _library.Levels.Length == 0)
        {
            Debug.LogError("LevelLibrary has no levels assigned.");
            return;
        }

        ClearPreviousPigs();
        _shelf.Clear();
        _queue.Clear();
        _pathService.ClearAll();

        int index = Mathf.Clamp(_progress.CurrentIndex, 0, _library.Levels.Length - 1);
        var data = _library.Levels[index];

        _model.Initialize(data);
        _eventBus.Raise(new LevelLoadRequested { LevelIndex = index });
        _eventBus.Raise(new LevelLoaded { Data = data });

        float cubeSize = _config.BoardWorldWidth / Mathf.Max(data.GridSize.x, data.GridSize.y);
        _track.Rebuild(data.GridSize, cubeSize, _config.PerimeterOffset, _config.PerimeterHeight);

        SpawnShelfPigs(data);
        SpawnQueuePigs(data);
    }

    private void ClearPreviousPigs()
    {
        ClearChildren(_shelfRoot);
        ClearChildren(_queueRoot);
        ClearChildren(_activePigsRoot);
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void SpawnShelfPigs(LevelData data)
    {
        int count = Mathf.Min(_config.ShelfSlotCount, data.ShelfPigs?.Length ?? 0);
        float halfBoard = _config.BoardWorldWidth * 0.5f;
        float halfGridZ = data.GridSize.y * 0.5f * (_config.BoardWorldWidth / Mathf.Max(data.GridSize.x, data.GridSize.y));
        float spacing = _config.BoardWorldWidth / _config.ShelfSlotCount;
        float startX = -halfBoard + spacing * 0.5f;
        float z = -halfGridZ - _config.PerimeterOffset - 1.2f;

        for (int i = 0; i < count; i++)
        {
            var cfg = data.ShelfPigs[i];
            Color32 color = data.PaletteColors[cfg.ColorIndex];
            var pig = _pigFactory.Create(cfg.ColorIndex, cfg.Ammo, color, PigOrigin.Shelf, _shelfRoot);
            var slotPos = new Vector3(startX + i * spacing, _config.PerimeterHeight, z);
            pig.transform.position = slotPos;
            _shelf.SetSlotPosition(i, slotPos);
            _shelf.TryPlaceAtSlot(i, pig);
        }
    }

    private void SpawnQueuePigs(LevelData data)
    {
        if (data.QueuePigs == null || data.QueuePigs.Length == 0)
        {
            _queue.InitializeSlots(System.Array.Empty<Vector3>());
            return;
        }

        int count = data.QueuePigs.Length;
        float halfBoard = _config.BoardWorldWidth * 0.5f;
        float halfGridZ = data.GridSize.y * 0.5f * (_config.BoardWorldWidth / Mathf.Max(data.GridSize.x, data.GridSize.y));
        float spacing = _config.BoardWorldWidth / Mathf.Max(count, 1);
        float startX = -halfBoard + spacing * 0.5f;
        float z = -halfGridZ - _config.PerimeterOffset - 2.6f;

        var positions = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            positions[i] = new Vector3(startX + i * spacing, _config.PerimeterHeight, z);
        }
        _queue.InitializeSlots(positions);

        for (int i = 0; i < count; i++)
        {
            var cfg = data.QueuePigs[i];
            Color32 color = data.PaletteColors[cfg.ColorIndex];
            var pig = _pigFactory.Create(cfg.ColorIndex, cfg.Ammo, color, PigOrigin.Queue, _queueRoot);
            pig.transform.position = positions[i];
            _queue.Enqueue(pig);
        }
    }
}
