using System;

[Serializable]
public struct CellAddress : IEquatable<CellAddress>
{
    public int X;
    public int Y;

    public CellAddress(int x, int y)
    {
        X = x;
        Y = y;
    }

    public bool Equals(CellAddress other) => X == other.X && Y == other.Y;
    public override bool Equals(object obj) => obj is CellAddress other && Equals(other);
    public override int GetHashCode() => unchecked(X * 397 ^ Y);
    public override string ToString() => $"({X},{Y})";
}
