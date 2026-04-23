using DG.Tweening;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public class VfxService : MonoBehaviour
{
    [Inject] private GameEventBus _eventBus;
    [Inject] private GridModel _model;
    [Inject] private GridRenderer _renderer;

    [SerializeField] private ParticleSystem _sparkPrefab;

    private ObjectPool<ParticleSystem> _sparkPool;

    private void Awake()
    {
        _sparkPool = new ObjectPool<ParticleSystem>(
            createFunc: CreateSpark,
            actionOnGet: ps => ps.gameObject.SetActive(true),
            actionOnRelease: ps => { if (ps != null) { ps.Clear(); ps.gameObject.SetActive(false); } },
            actionOnDestroy: ps => { if (ps != null) Destroy(ps.gameObject); },
            defaultCapacity: 8,
            maxSize: 32);
    }

    private void OnEnable()
    {
        _eventBus.SubscribeTo<CellPainted>(OnCellPainted);
    }

    private void OnDisable()
    {
        _eventBus.UnsubscribeFrom<CellPainted>(OnCellPainted);
    }

    private void OnCellPainted(ref CellPainted e)
    {
        if (_sparkPrefab == null) return;

        var ps = _sparkPool.Get();
        if (ps == null) return;
        ps.transform.position = _renderer.GetCellWorldPos(e.Cell) + Vector3.up * 0.3f;
        var main = ps.main;
        Color c = _model.PaletteColor(e.ColorIndex);
        main.startColor = new ParticleSystem.MinMaxGradient(c);
        ps.Play();
        DOVirtual.DelayedCall(1f, () => _sparkPool.Release(ps));
    }

    private ParticleSystem CreateSpark()
    {
        if (_sparkPrefab == null)
        {
            Debug.LogError("VfxService: spark prefab not assigned. Run PixelFlow > Create Default Prefabs.");
            return null;
        }
        var instance = Instantiate(_sparkPrefab, transform);
        instance.gameObject.SetActive(false);
        return instance;
    }
}
