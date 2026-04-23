using UnityEngine;
using Debug = UnityEngine.Debug;

public class GridModel
{
    private Vector2Int _size;
    private byte[] _targetColors;
    private bool[] _painted;
    private bool[] _stone;
    private Color32[] _palette;
    private int _normalCellCount;
    private int _paintedCount;

    public Vector2Int Size => _size;
    public int TotalCells => _size.x * _size.y;
    public int NormalCellCount => _normalCellCount;
    public int PaintedCount => _paintedCount;
    public Color32[] Palette => _palette;

    public void Initialize(LevelData data)
    {
        _size = data.GridSize;
        int total = _size.x * _size.y;
        _targetColors = new byte[total];
        _painted = new bool[total];
        _stone = new bool[total];
        _palette = data.PaletteColors;

        int cellLen = data.CellColorIndices?.Length ?? 0;
        int stoneLen = data.StoneMask?.Length ?? 0;
        if (cellLen != total || stoneLen != total)
        {
            Debug.LogError($"LevelData '{data.name}' arrays do not match grid size {_size.x}x{_size.y}={total}. " +
                           $"CellColorIndices={cellLen}, StoneMask={stoneLen}. Regenerate the level.");
            return;
        }

        System.Array.Copy(data.CellColorIndices, _targetColors, total);
        System.Array.Copy(data.StoneMask, _stone, total);

        _normalCellCount = 0;
        _paintedCount = 0;
        for (int i = 0; i < total; i++)
        {
            if (!_stone[i]) _normalCellCount++;
        }
    }

    public bool InBounds(int x, int y) => x >= 0 && x < _size.x && y >= 0 && y < _size.y;
    public bool InBounds(CellAddress c) => InBounds(c.X, c.Y);

    public int Index(int x, int y) => y * _size.x + x;
    public int Index(CellAddress c) => Index(c.X, c.Y);

    public byte GetTargetColor(CellAddress c) => _targetColors[Index(c)];
    public bool IsPainted(CellAddress c) => _painted[Index(c)];
    public bool IsStone(CellAddress c) => _stone[Index(c)];

    public Color32 PaletteColor(byte index) => _palette[index];

    public bool TryPaint(CellAddress c, byte colorIndex)
    {
        int i = Index(c);
        if (_stone[i] || _painted[i] || _targetColors[i] != colorIndex) return false;
        _painted[i] = true;
        _paintedCount++;
        return true;
    }

    public bool IsLevelComplete() => _paintedCount >= _normalCellCount;

    /// Walks cells from startCell in direction. Returns first cell whose target color matches
    /// and is unpainted. Stone blocks the line entirely. Painted cells are passed through.
    public bool TryFindFirstMatch(CellAddress startCell, Vector2Int direction, byte colorIndex, out CellAddress hit)
    {
        hit = default;
        int x = startCell.X;
        int y = startCell.Y;
        while (InBounds(x, y))
        {
            int i = Index(x, y);
            if (_stone[i]) return false;
            if (!_painted[i] && _targetColors[i] == colorIndex)
            {
                hit = new CellAddress(x, y);
                return true;
            }
            x += direction.x;
            y += direction.y;
        }
        return false;
    }
}
