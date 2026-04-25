using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class QueueService
{
    public const int QueueCount = 3;
    private const float ShiftDuration = 0.25f;

    private readonly List<PigEntity>[] _queues;
    private readonly Vector3[] _firstSlots;
    private Vector3 _slotOffset;
    private bool _hasOffset;

    public QueueService()
    {
        _queues = new List<PigEntity>[QueueCount];
        _firstSlots = new Vector3[QueueCount];
        for (int q = 0; q < QueueCount; q++)
        {
            _queues[q] = new List<PigEntity>();
        }
    }

    public int TotalCount
    {
        get
        {
            int total = 0;
            for (int q = 0; q < QueueCount; q++) total += _queues[q].Count;
            return total;
        }
    }

    public void Clear()
    {
        for (int q = 0; q < QueueCount; q++)
        {
            _queues[q].Clear();
        }
    }

    public void InitializeQueueSlots(int queueIndex, Vector3 firstSlot, Vector3 slotOffset)
    {
        _firstSlots[queueIndex] = firstSlot;
        _slotOffset = slotOffset;
        _hasOffset = true;
        _queues[queueIndex].Clear();
    }

    public void Enqueue(int queueIndex, PigEntity pig)
    {
        _queues[queueIndex].Add(pig);
        pig.SetInteractable(_queues[queueIndex].Count == 1);
    }

    public Vector3 GetSlotPosition(int queueIndex, int slotIndex)
    {
        if (!_hasOffset) return Vector3.zero;
        return _firstSlots[queueIndex] + _slotOffset * slotIndex;
    }

    public bool IsSelectable(PigEntity pig)
    {
        for (int q = 0; q < QueueCount; q++)
        {
            if (_queues[q].Count > 0 && _queues[q][0] == pig) return true;
        }
        return false;
    }

    public bool TryRemove(PigEntity pig)
    {
        for (int q = 0; q < QueueCount; q++)
        {
            int idx = _queues[q].IndexOf(pig);
            if (idx < 0) continue;
            _queues[q].RemoveAt(idx);
            ShiftQueueForward(q, idx);
            if (_queues[q].Count > 0) _queues[q][0].SetInteractable(true);
            return true;
        }
        return false;
    }

    private void ShiftQueueForward(int queueIndex, int fromIndex)
    {
        var queue = _queues[queueIndex];
        var first = _firstSlots[queueIndex];
        for (int i = fromIndex; i < queue.Count; i++)
        {
            queue[i].transform.DOMove(first + _slotOffset * i, ShiftDuration).SetEase(Ease.OutCubic);
        }
    }
}
