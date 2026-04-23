using UnityEngine;

public class ShelfService
{
    private readonly PigEntity[] _slots;
    private readonly bool[] _reserved;
    private readonly Vector3[] _slotPositions;
    private readonly int _slotCount;

    public int SlotCount => _slotCount;

    public ShelfService(int slotCount)
    {
        _slotCount = slotCount;
        _slots = new PigEntity[slotCount];
        _reserved = new bool[slotCount];
        _slotPositions = new Vector3[slotCount];
    }

    public void Clear()
    {
        for (int i = 0; i < _slotCount; i++)
        {
            _slots[i] = null;
            _reserved[i] = false;
        }
    }

    public void SetSlotPosition(int index, Vector3 worldPos) => _slotPositions[index] = worldPos;
    public Vector3 GetSlotPosition(int index) => _slotPositions[index];
    public PigEntity GetPigInSlot(int index) => _slots[index];

    public bool TryPlaceAtSlot(int index, PigEntity pig)
    {
        if (_slots[index] != null || _reserved[index]) return false;
        _slots[index] = pig;
        pig.ShelfSlotIndex = index;
        pig.SetInteractable(true);
        return true;
    }

    public void DispatchFromSlot(int index)
    {
        var pig = _slots[index];
        if (pig == null) return;
        _slots[index] = null;
        _reserved[index] = true;
    }

    public void ReturnDispatchedPig(PigEntity pig)
    {
        if (pig.ShelfSlotIndex < 0) return;
        _slots[pig.ShelfSlotIndex] = pig;
        _reserved[pig.ShelfSlotIndex] = false;
        pig.SetInteractable(true);
    }

    public void ReleaseReservation(int index)
    {
        _reserved[index] = false;
    }

    public bool TryFindEmptySlot(out int index)
    {
        for (int i = 0; i < _slotCount; i++)
        {
            if (_slots[i] == null && !_reserved[i])
            {
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }
}
