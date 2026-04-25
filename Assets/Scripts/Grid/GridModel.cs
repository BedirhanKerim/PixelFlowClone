using UnityEngine;

public class GridModel
{
    public class Block
    {
        public int Id;
        public Vector2Int Origin;
        public Vector2Int Size;
        public byte ColorIndex;
        public int Health;
        public int MaxHealth;
        public bool Destroyed;
    }

    private Vector2Int _size;
    private byte[] _targetColors;
    private bool[] _painted;
    private CellType[] _types;
    private int[] _blockIds;
    private Block[] _blocks;
    private Color32[] _palette;
    private int _paintableCount;
    private int _paintedCount;

    public Vector2Int Size => _size;
    public int TotalCells => _size.x * _size.y;
    public int PaintableCount => _paintableCount;
    public int PaintedCount => _paintedCount;
    public Color32[] Palette => _palette;
    public Block[] Blocks => _blocks;

    public void Initialize(LevelData data)
    {
        _size = data.GridSize;
        int total = _size.x * _size.y;
        _targetColors = new byte[total];
        _painted = new bool[total];
        _types = new CellType[total];
        _blockIds = new int[total];
        _palette = data.PaletteColors;

        int cellLen = data.CellColorIndices?.Length ?? 0;
        int typeLen = data.CellTypes?.Length ?? 0;
        if (cellLen != total || typeLen != total) return;

        System.Array.Copy(data.CellColorIndices, _targetColors, total);
        System.Array.Copy(data.CellTypes, _types, total);
        for (int i = 0; i < total; i++) _blockIds[i] = -1;

        BuildBlocks(data);

        _paintableCount = 0;
        _paintedCount = 0;
        for (int i = 0; i < total; i++)
        {
            if (_types[i] == CellType.Normal) _paintableCount++;
        }
        _paintableCount += _blocks.Length;
    }

    private void BuildBlocks(LevelData data)
    {
        if (data.ManualBlocks == null || data.ManualBlocks.Length == 0)
        {
            _blocks = System.Array.Empty<Block>();
            return;
        }

        _blocks = new Block[data.ManualBlocks.Length];
        for (int b = 0; b < data.ManualBlocks.Length; b++)
        {
            var mb = data.ManualBlocks[b];
            int hp = Mathf.Max(1, mb.Health);
            _blocks[b] = new Block
            {
                Id = b,
                Origin = mb.Origin,
                Size = mb.Size,
                ColorIndex = mb.ColorIndex,
                Health = hp,
                MaxHealth = hp,
                Destroyed = false
            };

            for (int by = 0; by < mb.Size.y; by++)
            {
                for (int bx = 0; bx < mb.Size.x; bx++)
                {
                    int gx = mb.Origin.x + bx;
                    int gy = mb.Origin.y + by;
                    if (!InBounds(gx, gy)) continue;
                    int idx = Index(gx, gy);
                    _blockIds[idx] = b;
                    _types[idx] = CellType.HealthBlock;
                    _targetColors[idx] = mb.ColorIndex;
                }
            }
        }
    }

    public bool InBounds(int x, int y) => x >= 0 && x < _size.x && y >= 0 && y < _size.y;
    public bool InBounds(CellAddress c) => InBounds(c.X, c.Y);

    public int Index(int x, int y) => y * _size.x + x;
    public int Index(CellAddress c) => Index(c.X, c.Y);

    public byte GetTargetColor(CellAddress c) => _targetColors[Index(c)];
    public bool IsPainted(CellAddress c) => _painted[Index(c)];
    public CellType GetCellType(CellAddress c) => _types[Index(c)];
    public bool IsEmpty(CellAddress c) => _types[Index(c)] == CellType.Empty;
    public bool IsStone(CellAddress c) => _types[Index(c)] == CellType.Stone;
    public int GetBlockId(CellAddress c) => _blockIds[Index(c)];
    public Block GetBlock(int id) => (id >= 0 && id < _blocks.Length) ? _blocks[id] : null;

    public Color32 PaletteColor(byte index) => _palette[index];

    public HitResult TryHit(CellAddress c, byte colorIndex, out int blockId, out int remainingHealth)
    {
        blockId = -1;
        remainingHealth = 0;

        int i = Index(c);
        var type = _types[i];
        if (type != CellType.Normal && type != CellType.HealthBlock) return HitResult.Missed;
        if (_painted[i]) return HitResult.Missed;
        if (_targetColors[i] != colorIndex) return HitResult.Missed;

        if (type == CellType.Normal)
        {
            _painted[i] = true;
            _paintedCount++;
            return HitResult.Destroyed;
        }

        int id = _blockIds[i];
        var block = _blocks[id];
        if (block.Destroyed) return HitResult.Missed;

        block.Health--;
        blockId = id;
        remainingHealth = block.Health;
        if (block.Health <= 0)
        {
            block.Destroyed = true;
            _paintedCount++;
            for (int by = 0; by < block.Size.y; by++)
            {
                for (int bx = 0; bx < block.Size.x; bx++)
                {
                    int gx = block.Origin.x + bx;
                    int gy = block.Origin.y + by;
                    if (!InBounds(gx, gy)) continue;
                    _painted[Index(gx, gy)] = true;
                }
            }
            return HitResult.Destroyed;
        }
        return HitResult.Damaged;
    }

    public bool IsLevelComplete() => _paintedCount >= _paintableCount;

    public bool TryFindFirstMatch(CellAddress startCell, Vector2Int direction, byte colorIndex, out CellAddress hit)
    {
        hit = default;
        int x = startCell.X;
        int y = startCell.Y;
        while (InBounds(x, y))
        {
            int i = Index(x, y);
            var type = _types[i];

            if (type == CellType.Empty)
            {
                x += direction.x;
                y += direction.y;
                continue;
            }

            if (_painted[i]) return false;
            if (type == CellType.Stone) return false;

            if (_targetColors[i] == colorIndex)
            {
                hit = new CellAddress(x, y);
                return true;
            }
            return false;
        }
        return false;
    }

    public void MarkDestroyed(CellAddress c)
    {
        _types[Index(c)] = CellType.Empty;
    }

    public void MarkBlockDestroyed(int blockId)
    {
        if (blockId < 0 || blockId >= _blocks.Length) return;
        var block = _blocks[blockId];
        for (int by = 0; by < block.Size.y; by++)
        {
            for (int bx = 0; bx < block.Size.x; bx++)
            {
                int gx = block.Origin.x + bx;
                int gy = block.Origin.y + by;
                if (!InBounds(gx, gy)) continue;
                _types[Index(gx, gy)] = CellType.Empty;
            }
        }
    }
}
