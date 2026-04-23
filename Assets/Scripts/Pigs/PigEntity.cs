using TMPro;
using UnityEngine;

public class PigEntity : MonoBehaviour, IInteractable
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private const int MaxBodyRenderers = 2;

    [SerializeField] private MeshRenderer[] _bodyRenderers = new MeshRenderer[MaxBodyRenderers];
    [SerializeField] private TextMeshPro _ammoText;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_bodyRenderers == null)
        {
            _bodyRenderers = new MeshRenderer[MaxBodyRenderers];
            return;
        }
        if (_bodyRenderers.Length != MaxBodyRenderers)
        {
            System.Array.Resize(ref _bodyRenderers, MaxBodyRenderers);
        }
    }
#endif

    public int Id { get; private set; }
    public byte ColorIndex { get; private set; }
    public int Ammo { get; private set; }
    public PigOrigin Origin { get; set; }
    public PigState State { get; set; }
    public int ShelfSlotIndex { get; set; } = -1;
    public Color32 TintColor { get; private set; }

    public int CurrentSegment { get; set; }
    public float CurrentSegmentProgress { get; set; }

    private MaterialPropertyBlock _mpb;

    private GameEventBus _eventBus;
    private ShelfService _shelf;
    private QueueService _queue;
    private PigPathService _pathService;

    public bool HasAmmo => Ammo > 0;

    public void InjectServices(GameEventBus eventBus, ShelfService shelf, QueueService queue, PigPathService pathService)
    {
        _eventBus = eventBus;
        _shelf = shelf;
        _queue = queue;
        _pathService = pathService;
    }

    public void Configure(int id, byte colorIndex, int ammo, Color32 color, PigOrigin origin)
    {
        Id = id;
        ColorIndex = colorIndex;
        Ammo = ammo;
        Origin = origin;
        State = PigState.Idle;
        TintColor = color;

        _mpb ??= new MaterialPropertyBlock();
        if (_bodyRenderers != null)
        {
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                var r = _bodyRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, color);
                r.SetPropertyBlock(_mpb);
            }
        }

        RefreshAmmoText();
    }

    public void ConsumeAmmo()
    {
        Ammo--;
        RefreshAmmoText();
    }

    public void OnTap()
    {
        if (State != PigState.Idle) return;
        if (!_pathService.CanDispatch()) return;

        if (Origin == PigOrigin.Shelf)
        {
            int slot = ShelfSlotIndex;
            _shelf.DispatchFromSlot(slot);
            _pathService.Dispatch(this, _shelf.GetSlotPosition(slot));
            _eventBus.Raise(new PigTappedFromShelf { PigId = Id, SlotIndex = slot });
        }
        else
        {
            if (!_queue.IsSelectable(this)) return;
            var pos = transform.position;
            _queue.TryRemove(this);
            _pathService.Dispatch(this, pos);
            _eventBus.Raise(new PigTappedFromQueue { PigId = Id });
        }
    }

    private void RefreshAmmoText()
    {
        if (_ammoText != null) _ammoText.text = Ammo.ToString();
    }
}
