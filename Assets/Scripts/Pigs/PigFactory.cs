using UnityEngine;

public class PigFactory : MonoBehaviour
{
    [SerializeField] private PigEntity _pigPrefab;

    private int _nextId;

    public PigEntity Create(byte colorIndex, int ammo, Color32 color, PigOrigin origin, Transform parent)
    {
        if (_pigPrefab == null)
        {
            Debug.LogError("PigFactory: pig prefab not assigned. Run PixelFlow > Create Default Prefabs.");
            return null;
        }
        var pig = Instantiate(_pigPrefab, parent);
        pig.Configure(++_nextId, colorIndex, ammo, color, origin);
        return pig;
    }
}
