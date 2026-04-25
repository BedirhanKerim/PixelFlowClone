using System;
using UnityEngine;

[Serializable]
public struct ManualBlock
{
    public Vector2Int Origin;
    public Vector2Int Size;
    public byte ColorIndex;
    public byte Health;
}
