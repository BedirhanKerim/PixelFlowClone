using UnityEngine;
using UnityEngine.Pool;

public class BulletFactory : MonoBehaviour
{
    [SerializeField] private BulletController _bulletPrefab;

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
        if (_bulletPrefab == null)
        {
            Debug.LogError("BulletFactory: bullet prefab not assigned. Run PixelFlow > Create Default Prefabs.");
            return null;
        }
        var instance = Instantiate(_bulletPrefab, transform);
        instance.gameObject.SetActive(false);
        return instance;
    }
}
