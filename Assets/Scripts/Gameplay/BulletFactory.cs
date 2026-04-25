using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public class BulletFactory : MonoBehaviour
{
    [SerializeField] private BulletController _bulletPrefab;

    [Inject] private GameEventBus _eventBus;

    private ObjectPool<BulletController> _pool;

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

    public BulletController Get() => _pool.Get();
    public void Release(BulletController bullet) => _pool.Release(bullet);

    private BulletController CreateBullet()
    {
        if (_bulletPrefab == null) return null;
        var instance = Instantiate(_bulletPrefab, transform);
        instance.Bind(this, _eventBus);
        instance.gameObject.SetActive(false);
        return instance;
    }
}
