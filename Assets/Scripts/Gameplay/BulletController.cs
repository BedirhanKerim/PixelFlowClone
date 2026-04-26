using UnityEngine;

public class BulletController : MonoBehaviour
{
    public struct Payload
    {
        public Vector3 From;
        public Vector3 To;
        public Color32 Color;
        public CellAddress TargetCell;
        public int BlockId;
        public byte ColorIndex;
        public bool IsBlockDestroyed;
        public int RemainingHealth;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private MeshRenderer _renderer;
    [SerializeField] private TrailRenderer _trail;

    private MaterialPropertyBlock _mpb;
    private BulletFactory _factory;
    private GameEventBus _bus;

    private Vector3 _from;
    private Vector3 _to;
    private float _duration;
    private float _elapsed;
    private bool _flying;

    private CellAddress _targetCell;
    private int _blockId;
    private byte _colorIndex;
    private bool _isBlockDestroyed;
    private int _remainingHealth;

    public void Bind(BulletFactory factory, GameEventBus bus)
    {
        _factory = factory;
        _bus = bus;
    }

    public void Launch(in Payload payload, float duration)
    {
        _from = payload.From;
        _to = payload.To;
        _duration = Mathf.Max(0.0001f, duration);
        _elapsed = 0f;
        _targetCell = payload.TargetCell;
        _blockId = payload.BlockId;
        _colorIndex = payload.ColorIndex;
        _isBlockDestroyed = payload.IsBlockDestroyed;
        _remainingHealth = payload.RemainingHealth;

        transform.position = _from;

        if (_renderer != null)
        {
            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetColor(BaseColorId, payload.Color);
            _renderer.SetPropertyBlock(_mpb);
        }

        if (_trail != null)
        {
            Color c = payload.Color;
            _trail.startColor = c;
            c.a = 0f;
            _trail.endColor = c;
            _trail.Clear();
        }

        _flying = true;
    }

    public void Tick(float dt)
    {
        if (!_flying) return;

        _elapsed += dt;
        float t = _elapsed >= _duration ? 1f : _elapsed / _duration;
        transform.position = Vector3.LerpUnclamped(_from, _to, t);

        if (t >= 1f)
        {
            _flying = false;
            HandleArrived();
        }
    }

    private void HandleArrived()
    {
        if (_blockId >= 0)
        {
            if (_isBlockDestroyed)
            {
                _bus.Raise(new BlockPainted { BlockId = _blockId, ColorIndex = _colorIndex });
            }
            else
            {
                _bus.Raise(new BlockDamaged { BlockId = _blockId, RemainingHealth = _remainingHealth });
            }
        }
        else
        {
            _bus.Raise(new CellPainted { Cell = _targetCell, ColorIndex = _colorIndex });
        }

        if (_factory != null) _factory.Release(this);
    }
}
