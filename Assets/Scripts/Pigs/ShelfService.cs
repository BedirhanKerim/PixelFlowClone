using UnityEngine;

public class ShelfService
{
    private readonly PigEntity[] _slots;
    private readonly Vector3[] _slotPositions;
    private readonly int _slotCount;

    public int SlotCount => _slotCount;

    public ShelfService(int slotCount)
    {
        _slotCount = slotCount;
        _slots = new PigEntity[slotCount];
        _slotPositions = new Vector3[slotCount];
    }

    public void Clear()
    {
        for (int i = 0; i < _slotCount; i++) _slots[i] = null;
    }

    public void SetSlotPosition(int index, Vector3 worldPos) => _slotPositions[index] = worldPos;
    public Vector3 GetSlotPosition(int index) => _slotPositions[index];
    public PigEntity GetPigInSlot(int index) => _slots[index];

    public bool TryPlaceAtSlot(int index, PigEntity pig)
    {
        if (_slots[index] != null) return false;
        _slots[index] = pig;
        pig.ShelfSlotIndex = index;
        pig.SetInteractable(true);
        return true;
    }

    public void DispatchFromSlot(int index)
    {
        if (_slots[index] == null) return;
        _slots[index] = null;
    }

    public bool TryFindEmptySlot(out int index)
    {
        for (int i = 0; i < _slotCount; i++)
        {
            if (_slots[i] == null)
            {
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }
}
