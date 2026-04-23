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

    [System.Serializable]
    public class QueueSlotSet
    {
        public Transform[] Points;
    }

    [SerializeField] private Transform _shelfRoot;
    [SerializeField] private Transform _queueRoot;
    [SerializeField] private Transform _activePigsRoot;
    [SerializeField] private Transform[] _shelfSlotPoints;
    [SerializeField] private QueueSlotSet[] _queueSlotSets = new QueueSlotSet[QueueService.QueueCount];
    [SerializeField] private Transform _pathBackLeft;
    [SerializeField] private Transform _pathBackRight;
    [SerializeField] private Transform _pathTopRight;
    [SerializeField] private Transform _pathTopLeft;
    [SerializeField] private Transform _pathFinish;

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
        AdvanceAfterDelay(0.5f).Forget();
    }

    private void OnLevelFailed(ref LevelFailed e)
    {
        ReloadAfterDelay(1.5f).Forget();
    }

    private async UniTaskVoid AdvanceAfterDelay(float delay)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: _cts.Token);
        if (_library != null && _library.Levels != null && _library.Levels.Length > 0)
        {
            if (_progress.CurrentIndex < _library.Levels.Length - 1)
            {
                _progress.Advance();
            }
            else
            {
                _progress.Reset();
            }
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

        if (_pathBackLeft == null || _pathBackRight == null || _pathTopRight == null || _pathTopLeft == null || _pathFinish == null)
        {
            Debug.LogError("LevelLoader: assign all 5 path transforms (BackLeft, BackRight, TopRight, TopLeft, Finish).");
            return;
        }
        _track.Rebuild(data.GridSize, _pathBackLeft.position, _pathBackRight.position, _pathTopRight.position, _pathTopLeft.position, _pathFinish.position);

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
        if (_shelfSlotPoints == null || _shelfSlotPoints.Length < _config.ShelfSlotCount)
        {
            Debug.LogError($"LevelLoader: ShelfSlotPoints needs {_config.ShelfSlotCount} transforms assigned.");
            return;
        }

        for (int i = 0; i < _config.ShelfSlotCount; i++)
        {
            _shelf.SetSlotPosition(i, _shelfSlotPoints[i].position);
        }

        int count = Mathf.Min(_config.ShelfSlotCount, data.ShelfPigs?.Length ?? 0);
        for (int i = 0; i < count; i++)
        {
            var cfg = data.ShelfPigs[i];
            Color32 color = data.PaletteColors[cfg.ColorIndex];
            var pig = _pigFactory.Create(cfg.ColorIndex, cfg.Ammo, color, PigOrigin.Shelf, _shelfRoot);
            pig.transform.position = _shelf.GetSlotPosition(i);
            _shelf.TryPlaceAtSlot(i, pig);
        }
    }

    private void SpawnQueuePigs(LevelData data)
    {
        for (int q = 0; q < QueueService.QueueCount; q++)
        {
            var points = (_queueSlotSets != null && q < _queueSlotSets.Length) ? _queueSlotSets[q]?.Points : null;
            if (points == null) points = System.Array.Empty<Transform>();
            var positions = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                positions[i] = points[i] != null ? points[i].position : Vector3.zero;
            }
            _queue.InitializeQueueSlots(q, positions);
        }

        if (data.QueuePigs == null || data.QueuePigs.Length == 0) return;

        var shuffled = new System.Collections.Generic.List<PigConfig>(data.QueuePigs);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var perQueueCounts = new int[QueueService.QueueCount];
        for (int i = 0; i < shuffled.Count; i++)
        {
            int q = i % QueueService.QueueCount;
            int cap = (_queueSlotSets != null && q < _queueSlotSets.Length && _queueSlotSets[q]?.Points != null)
                ? _queueSlotSets[q].Points.Length
                : 0;

            if (perQueueCounts[q] >= cap)
            {
                Debug.LogError($"LevelLoader: queue {q} has no more slot for pig {i}. Add more slot points.");
                continue;
            }

            var cfg = shuffled[i];
            Color32 color = data.PaletteColors[cfg.ColorIndex];
            var pig = _pigFactory.Create(cfg.ColorIndex, cfg.Ammo, color, PigOrigin.Queue, _queueRoot);
            pig.transform.position = _queue.GetSlotPosition(q, perQueueCounts[q]);
            _queue.Enqueue(q, pig);
            perQueueCounts[q]++;
        }
    }
}
