using UnityEngine;
using VContainer;

public class PigFactory : MonoBehaviour
{
    [SerializeField] private PigEntity _pigPrefab;

    [Inject] private GameEventBus _eventBus;
    [Inject] private ShelfService _shelf;
    [Inject] private QueueService _queue;
    [Inject] private PigPathService _pathService;

    private int _nextId;

    public PigEntity Create(byte colorIndex, int ammo, Color32 color, PigOrigin origin, Transform parent)
    {
        if (_pigPrefab == null)
        {
            Debug.LogError("PigFactory: pig prefab not assigned.");
            return null;
        }
        var pig = Instantiate(_pigPrefab, parent);
        pig.InjectServices(_eventBus, _shelf, _queue, _pathService);
        pig.Configure(++_nextId, colorIndex, ammo, color, origin);
        return pig;
    }
}
