using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class QueueService
{
    public const int QueueCount = 3;
    private const float ShiftDuration = 0.25f;

    private readonly List<PigEntity>[] _queues;
    private Vector3[][] _slotPositions;

    public QueueService()
    {
        _queues = new List<PigEntity>[QueueCount];
        _slotPositions = new Vector3[QueueCount][];
        for (int q = 0; q < QueueCount; q++)
        {
            _queues[q] = new List<PigEntity>();
            _slotPositions[q] = System.Array.Empty<Vector3>();
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
            _slotPositions[q] = System.Array.Empty<Vector3>();
        }
    }

    public void InitializeQueueSlots(int queueIndex, Vector3[] positions)
    {
        _slotPositions[queueIndex] = positions;
        _queues[queueIndex].Clear();
    }

    public void Enqueue(int queueIndex, PigEntity pig)
    {
        _queues[queueIndex].Add(pig);
    }

    public Vector3 GetSlotPosition(int queueIndex, int slotIndex)
    {
        var positions = _slotPositions[queueIndex];
        if (positions.Length == 0) return Vector3.zero;
        return positions[Mathf.Clamp(slotIndex, 0, positions.Length - 1)];
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
            return true;
        }
        return false;
    }

    private void ShiftQueueForward(int queueIndex, int fromIndex)
    {
        var queue = _queues[queueIndex];
        var positions = _slotPositions[queueIndex];
        for (int i = fromIndex; i < queue.Count && i < positions.Length; i++)
        {
            queue[i].transform.DOMove(positions[i], ShiftDuration).SetEase(Ease.OutCubic);
        }
    }
}
