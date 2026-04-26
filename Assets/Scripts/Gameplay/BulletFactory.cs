using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public class BulletFactory : MonoBehaviour
{
    [SerializeField] private BulletController _bulletPrefab;

    [Inject] private GameEventBus _eventBus;

    private ObjectPool<BulletController> _pool;
    private readonly List<BulletController> _active = new List<BulletController>(64);

    private void Awake()
    {
        _pool = new ObjectPool<BulletController>(
            createFunc: CreateBullet,
            actionOnGet: b => b.gameObject.SetActive(true),
            actionOnRelease: b => { if (b != null) b.gameObject.SetActive(false); },
            actionOnDestroy: b => { if (b != null) Destroy(b.gameObject); },
            defaultCapacity: 20,
            maxSize: 64);
    }

    private void OnEnable()
    {
        if (_eventBus != null) _eventBus.SubscribeTo<LevelLoadRequested>(OnLevelLoadRequested);
    }

    private void OnDisable()
    {
        if (_eventBus != null) _eventBus.UnsubscribeFrom<LevelLoadRequested>(OnLevelLoadRequested);
    }

    private void OnLevelLoadRequested(ref LevelLoadRequested e)
    {
        ClearActive();
    }

    private void ClearActive()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var b = _active[i];
            if (b == null) continue;
            b.Abort();
            _pool.Release(b);
        }
        _active.Clear();
    }

    public BulletController Get()
    {
        var b = _pool.Get();
        if (b != null) _active.Add(b);
        return b;
    }

    public void Release(BulletController bullet)
    {
        if (bullet == null) return;
        int idx = _active.IndexOf(bullet);
        if (idx >= 0)
        {
            int last = _active.Count - 1;
            _active[idx] = _active[last];
            _active.RemoveAt(last);
        }
        _pool.Release(bullet);
    }

    private void Update()
    {
        if (_active.Count == 0) return;
        float dt = Time.deltaTime;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var b = _active[i];
            if (b != null) b.Tick(dt);
        }
    }

    private BulletController CreateBullet()
    {
        if (_bulletPrefab == null) return null;
        var instance = Instantiate(_bulletPrefab, transform);
        instance.Bind(this, _eventBus);
        instance.gameObject.SetActive(false);
        return instance;
    }
}
