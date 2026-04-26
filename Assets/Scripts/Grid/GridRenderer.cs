using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public class GridRenderer : MonoBehaviour
{
    private const int BatchSize = 1023;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly Color StoneColor = new Color(0.18f, 0.18f, 0.22f, 1f);

    [Inject] private GameEventBus _eventBus;
    [Inject] private GameConfig _config;
    [Inject] private GridModel _model;

    [SerializeField] private Mesh _cubeMesh;
    [SerializeField] private Material _normalMaterial;
    [SerializeField] private Material _stoneMaterial;
    [SerializeField] private TextMeshPro _hpTextPrefab;
    [SerializeField] private float _hpTextHeight = 0.6f;
    [SerializeField] private float _hpTextBounceStrength = 0.3f;
    [SerializeField] private float _hpTextBounceDuration = 0.25f;

    private struct CubeState
    {
        public Vector3 Position;
        public Color PlaceholderColor;
        public Color FinalColor;
        public float PopInDelay;
        public float PopInElapsed;
        public float DestroyElapsed;
        public int BatchIndex;
        public int SlotIndex;
        public bool IsStone;
        public bool IsDestroying;
        public bool IsAnimating;
        public bool IsDead;
    }

    private struct BlockState
    {
        public Vector3 Position;
        public Vector3 Scale;
        public Color Color;
        public float PopInDelay;
        public float PopInElapsed;
        public float DestroyElapsed;
        public int BatchIndex;
        public int SlotIndex;
        public bool IsDestroying;
        public bool IsAnimating;
        public bool IsDead;
    }

    private CubeState[] _states;
    private BlockState[] _blockStates;

    private Matrix4x4[][] _normalBatches;
    private Vector4[][] _normalBatchColors;
    private MaterialPropertyBlock[] _normalBatchMpbs;
    private bool[] _normalBatchColorsDirty;
    private int[] _normalBatchCounts;
    private int _normalBatchCount;

    private Matrix4x4[][] _stoneBatches;
    private int[] _stoneBatchCounts;
    private int _stoneBatchCount;

    private readonly List<int> _animatingCells = new List<int>(64);
    private readonly List<int> _animatingBlocks = new List<int>(8);

    private int _cellCount;
    private float _cubeSize;
    private Vector3 _originOffset;

    private bool _hasGrid;
    private int _buildVersion;

    private ObjectPool<TextMeshPro> _hpTextPool;
    private readonly Dictionary<int, TextMeshPro> _blockHpTexts = new Dictionary<int, TextMeshPro>();
    private Vector3 _hpTextBaseScale = Vector3.one;
    private bool _hpTextBaseScaleCached;

    private void Awake()
    {
        if (_cubeMesh == null) _cubeMesh = BuildDefaultCubeMesh();
        if (_hpTextPrefab != null)
        {
            _hpTextBaseScale = _hpTextPrefab.transform.localScale;
            _hpTextBaseScaleCached = true;
        }
        _hpTextPool = new ObjectPool<TextMeshPro>(
            createFunc: CreateHpText,
            actionOnGet: t => t.gameObject.SetActive(true),
            actionOnRelease: t =>
            {
                if (t == null) return;
                t.transform.DOKill();
                if (_hpTextBaseScaleCached) t.transform.localScale = _hpTextBaseScale;
                t.gameObject.SetActive(false);
            },
            actionOnDestroy: t => { if (t != null) Destroy(t.gameObject); },
            defaultCapacity: 8,
            maxSize: 64);
    }

    private void OnEnable()
    {
        _eventBus.SubscribeTo<LevelLoaded>(OnLevelLoaded);
        _eventBus.SubscribeTo<CellPainted>(OnCellPainted);
        _eventBus.SubscribeTo<BlockDamaged>(OnBlockDamaged);
        _eventBus.SubscribeTo<BlockPainted>(OnBlockPainted);
    }

    private void OnDisable()
    {
        _eventBus.UnsubscribeFrom<LevelLoaded>(OnLevelLoaded);
        _eventBus.UnsubscribeFrom<CellPainted>(OnCellPainted);
        _eventBus.UnsubscribeFrom<BlockDamaged>(OnBlockDamaged);
        _eventBus.UnsubscribeFrom<BlockPainted>(OnBlockPainted);
    }

    private TextMeshPro CreateHpText()
    {
        if (_hpTextPrefab == null) return null;
        var t = Instantiate(_hpTextPrefab, transform);
        t.gameObject.SetActive(false);
        return t;
    }

    private void ClearBlockHpTexts()
    {
        foreach (var kvp in _blockHpTexts)
        {
            if (kvp.Value != null) _hpTextPool.Release(kvp.Value);
        }
        _blockHpTexts.Clear();
    }

    public float CubeSize => _cubeSize;
    public Vector3 OriginOffset => _originOffset;
    public Vector3 GetCellWorldPos(CellAddress c) => _originOffset + new Vector3(c.X * _cubeSize, 0f, c.Y * _cubeSize);

    private void OnLevelLoaded(ref LevelLoaded e)
    {
        BuildGrid(e.Data);
    }

    private void OnCellPainted(ref CellPainted e)
    {
        int idx = _model.Index(e.Cell);
        if (idx < 0 || _states == null || idx >= _states.Length) return;
        ref var state = ref _states[idx];
        if (state.IsDead) return;

        _model.MarkDestroyed(e.Cell);
        state.FinalColor = _model.PaletteColor(e.ColorIndex);
        state.IsDestroying = true;
        state.DestroyElapsed = 0f;
        if (!state.IsAnimating)
        {
            state.IsAnimating = true;
            _animatingCells.Add(idx);
        }
    }

    private void OnBlockDamaged(ref BlockDamaged e)
    {
        if (!_blockHpTexts.TryGetValue(e.BlockId, out var t) || t == null) return;
        t.text = e.RemainingHealth.ToString();
        if (!_hpTextBaseScaleCached) return;
        var block = _model.GetBlock(e.BlockId);
        Vector3 scale = block != null ? GetHpTextScale(block.Size) : _hpTextBaseScale;
        t.transform.DOKill();
        t.transform.localScale = scale;
        t.transform.DOPunchScale(scale * _hpTextBounceStrength, _hpTextBounceDuration, 1, 0.5f);
    }

    private void OnBlockPainted(ref BlockPainted e)
    {
        if (_blockHpTexts.TryGetValue(e.BlockId, out var t))
        {
            if (t != null) _hpTextPool.Release(t);
            _blockHpTexts.Remove(e.BlockId);
        }
        if (_blockStates == null || e.BlockId < 0 || e.BlockId >= _blockStates.Length) return;
        ref var bs = ref _blockStates[e.BlockId];
        if (bs.IsDead || bs.IsDestroying) return;
        bs.IsDestroying = true;
        bs.DestroyElapsed = 0f;
        if (!bs.IsAnimating)
        {
            bs.IsAnimating = true;
            _animatingBlocks.Add(e.BlockId);
        }
    }

    private void BuildGrid(LevelData data)
    {
        _buildVersion++;
        _animatingCells.Clear();
        _animatingBlocks.Clear();

        _cellCount = data.GridSize.x * data.GridSize.y;
        _states = new CubeState[_cellCount];

        _cubeSize = _config.BoardWorldWidth / Mathf.Max(data.GridSize.x, data.GridSize.y);
        float halfBoardX = (data.GridSize.x - 1) * _cubeSize * 0.5f;
        float halfBoardZ = (data.GridSize.y - 1) * _cubeSize * 0.5f;
        _originOffset = new Vector3(-halfBoardX, 0f, -halfBoardZ);

        float maxStagger = _cellCount > 1
            ? (_config.GridPopInTotalDuration - _config.CubePopInDuration) / (_cellCount - 1)
            : 0f;
        float stagger = Mathf.Max(0f, Mathf.Min(_config.CubePopInStagger, maxStagger));

        int normalCount = 0;
        int stoneCount = 0;
        for (int i = 0; i < _cellCount; i++)
        {
            var type = data.CellTypes[i];
            if (type == CellType.Empty || type == CellType.HealthBlock) continue;
            if (type == CellType.Stone) stoneCount++;
            else normalCount++;
        }
        int blockCount = data.ManualBlocks?.Length ?? 0;

        AllocateBatches(normalCount + blockCount, stoneCount);

        int nextNormalSlot = 0;
        int nextStoneSlot = 0;
        for (int i = 0; i < _cellCount; i++)
        {
            var type = data.CellTypes[i];
            if (type == CellType.Empty || type == CellType.HealthBlock)
            {
                _states[i].IsDead = true;
                _states[i].BatchIndex = -1;
                continue;
            }

            int x = i % data.GridSize.x;
            int y = i / data.GridSize.x;
            var pos = _originOffset + new Vector3(x * _cubeSize, 0f, y * _cubeSize);

            bool isStone = type == CellType.Stone;
            int slot = isStone ? nextStoneSlot++ : nextNormalSlot++;
            int batch = slot / BatchSize;
            int slotInBatch = slot % BatchSize;

            ref var s = ref _states[i];
            s.Position = pos;
            s.PopInDelay = i * stagger;
            s.PopInElapsed = 0f;
            s.IsStone = isStone;
            s.IsDead = false;
            s.IsDestroying = false;
            s.DestroyElapsed = 0f;
            s.FinalColor = data.PaletteColors[data.CellColorIndices[i]];
            s.PlaceholderColor = isStone ? StoneColor : (Color)data.PaletteColors[data.CellColorIndices[i]];
            s.BatchIndex = batch;
            s.SlotIndex = slotInBatch;
            s.IsAnimating = true;

            if (isStone)
            {
                _stoneBatches[batch][slotInBatch] = default;
            }
            else
            {
                _normalBatches[batch][slotInBatch] = default;
                _normalBatchColors[batch][slotInBatch] = s.PlaceholderColor;
                _normalBatchColorsDirty[batch] = true;
            }

            _animatingCells.Add(i);
        }

        BuildBlockStates(data, stagger, ref nextNormalSlot);

        float popInDuration = (_cellCount - 1) * stagger + _config.CubePopInDuration;
        SpawnBlockHpTexts(data, popInDuration);

        _hasGrid = true;
    }

    private void AllocateBatches(int normalSlotCount, int stoneSlotCount)
    {
        _normalBatchCount = normalSlotCount > 0 ? (normalSlotCount + BatchSize - 1) / BatchSize : 0;
        _normalBatches = new Matrix4x4[_normalBatchCount][];
        _normalBatchColors = new Vector4[_normalBatchCount][];
        _normalBatchMpbs = new MaterialPropertyBlock[_normalBatchCount];
        _normalBatchCounts = new int[_normalBatchCount];
        _normalBatchColorsDirty = new bool[_normalBatchCount];
        for (int b = 0; b < _normalBatchCount; b++)
        {
            int size = Mathf.Min(BatchSize, normalSlotCount - b * BatchSize);
            _normalBatches[b] = new Matrix4x4[size];
            _normalBatchColors[b] = new Vector4[size];
            _normalBatchMpbs[b] = new MaterialPropertyBlock();
            _normalBatchCounts[b] = size;
            _normalBatchColorsDirty[b] = true;
        }

        _stoneBatchCount = stoneSlotCount > 0 ? (stoneSlotCount + BatchSize - 1) / BatchSize : 0;
        _stoneBatches = new Matrix4x4[_stoneBatchCount][];
        _stoneBatchCounts = new int[_stoneBatchCount];
        for (int b = 0; b < _stoneBatchCount; b++)
        {
            int size = Mathf.Min(BatchSize, stoneSlotCount - b * BatchSize);
            _stoneBatches[b] = new Matrix4x4[size];
            _stoneBatchCounts[b] = size;
        }
    }

    private void BuildBlockStates(LevelData data, float stagger, ref int nextNormalSlot)
    {
        if (data.ManualBlocks == null || data.ManualBlocks.Length == 0)
        {
            _blockStates = Array.Empty<BlockState>();
            return;
        }

        _blockStates = new BlockState[data.ManualBlocks.Length];
        for (int b = 0; b < data.ManualBlocks.Length; b++)
        {
            var mb = data.ManualBlocks[b];
            Vector3 cellPos = _originOffset + new Vector3(mb.Origin.x * _cubeSize, 0f, mb.Origin.y * _cubeSize);
            Vector3 center = cellPos + new Vector3(
                (mb.Size.x - 1) * 0.5f * _cubeSize,
                0f,
                (mb.Size.y - 1) * 0.5f * _cubeSize);

            int popDelayIndex = mb.Origin.y * data.GridSize.x + mb.Origin.x;
            Color color = mb.ColorIndex < data.PaletteColors.Length
                ? (Color)data.PaletteColors[mb.ColorIndex]
                : Color.white;

            int slot = nextNormalSlot++;
            int batch = slot / BatchSize;
            int slotInBatch = slot % BatchSize;

            _blockStates[b] = new BlockState
            {
                Position = center,
                Scale = new Vector3(mb.Size.x, 1f, mb.Size.y) * (_cubeSize * 0.95f),
                Color = color,
                PopInDelay = popDelayIndex * stagger,
                PopInElapsed = 0f,
                DestroyElapsed = 0f,
                BatchIndex = batch,
                SlotIndex = slotInBatch,
                IsDestroying = false,
                IsAnimating = true,
                IsDead = false
            };

            _normalBatches[batch][slotInBatch] = default;
            _normalBatchColors[batch][slotInBatch] = color;
            _normalBatchColorsDirty[batch] = true;

            _animatingBlocks.Add(b);
        }
    }

    private void SpawnBlockHpTexts(LevelData data, float popInDuration)
    {
        ClearBlockHpTexts();
        if (_hpTextPrefab == null || data.ManualBlocks == null) return;

        for (int b = 0; b < _blockStates.Length; b++)
        {
            var mb = data.ManualBlocks[b];
            if (mb.Health <= 0) continue;

            var t = _hpTextPool.Get();
            if (t == null) continue;
            t.transform.position = _blockStates[b].Position + new Vector3(0f, _hpTextHeight, 0f);
            t.transform.localScale = GetHpTextScale(mb.Size);
            t.text = mb.Health.ToString();
            t.gameObject.SetActive(false);
            _blockHpTexts[b] = t;
        }

        if (_blockHpTexts.Count > 0)
        {
            ShowHpTextsAfterDelay(popInDuration, _buildVersion).Forget();
        }
    }

    private async UniTaskVoid ShowHpTextsAfterDelay(float delay, int version)
    {
        if (delay > 0f) await UniTask.Delay(TimeSpan.FromSeconds(delay));
        if (version != _buildVersion) return;
        foreach (var kvp in _blockHpTexts)
        {
            if (kvp.Value != null) kvp.Value.gameObject.SetActive(true);
        }
    }

    private Vector3 GetHpTextScale(Vector2Int blockSize)
    {
        float area = Mathf.Max(1, blockSize.x * blockSize.y);
        float factor = Mathf.Sqrt(area) * _cubeSize;
        return _hpTextBaseScale * factor;
    }

    private void LateUpdate()
    {
        if (!_hasGrid) return;

        float dt = Time.deltaTime;
        float popDur = _config.CubePopInDuration;
        float paintDur = _config.CellPaintDuration;
        float invPopDur = 1f / Mathf.Max(popDur, 0.0001f);
        float invPaintDur = 1f / Mathf.Max(paintDur, 0.0001f);
        float cellTargetScale = _cubeSize * 0.95f;

        TickCells(dt, popDur, invPopDur, invPaintDur, cellTargetScale);
        TickBlocks(dt, popDur, invPopDur, invPaintDur);

        DrawInstances();
    }

    private void TickCells(float dt, float popDur, float invPopDur, float invPaintDur, float cellTargetScale)
    {
        for (int li = _animatingCells.Count - 1; li >= 0; li--)
        {
            int cellIdx = _animatingCells[li];
            ref var s = ref _states[cellIdx];

            if (s.PopInElapsed < s.PopInDelay)
            {
                s.PopInElapsed += dt;
                continue;
            }

            float popEnd = popDur + s.PopInDelay;
            if (s.PopInElapsed < popEnd) s.PopInElapsed += dt;

            float popT = (s.PopInElapsed - s.PopInDelay) * invPopDur;
            if (popT > 1f) popT = 1f;
            float popEased = popT <= 0f ? 0f : EaseOutBack(popT);

            Matrix4x4 matrix;
            bool stillAnimating = true;
            bool colorChanged = false;

            if (s.IsDestroying)
            {
                s.DestroyElapsed += dt;
                float dt01 = s.DestroyElapsed * invPaintDur;
                if (dt01 > 1f) dt01 = 1f;
                float ds = cellTargetScale * EaseInBack(1f - dt01);
                Quaternion rot = Quaternion.Euler(dt01 * 180f, dt01 * 180f, 0f);
                matrix = Matrix4x4.TRS(s.Position, rot, new Vector3(ds, ds, ds));
                colorChanged = true;

                if (dt01 >= 1f)
                {
                    s.IsDead = true;
                    stillAnimating = false;
                    matrix = default;
                }
            }
            else
            {
                float scale = cellTargetScale * popEased;
                matrix = BuildIdentityTRS(s.Position, scale);
                if (popT >= 1f) stillAnimating = false;
            }

            if (s.IsStone)
            {
                _stoneBatches[s.BatchIndex][s.SlotIndex] = matrix;
            }
            else
            {
                _normalBatches[s.BatchIndex][s.SlotIndex] = matrix;
                if (colorChanged)
                {
                    _normalBatchColors[s.BatchIndex][s.SlotIndex] = s.FinalColor;
                    _normalBatchColorsDirty[s.BatchIndex] = true;
                }
            }

            if (!stillAnimating)
            {
                s.IsAnimating = false;
                int last = _animatingCells.Count - 1;
                _animatingCells[li] = _animatingCells[last];
                _animatingCells.RemoveAt(last);
            }
        }
    }

    private void TickBlocks(float dt, float popDur, float invPopDur, float invPaintDur)
    {
        for (int li = _animatingBlocks.Count - 1; li >= 0; li--)
        {
            int blockIdx = _animatingBlocks[li];
            ref var bs = ref _blockStates[blockIdx];

            if (bs.PopInElapsed < bs.PopInDelay)
            {
                bs.PopInElapsed += dt;
                continue;
            }

            float popEnd = popDur + bs.PopInDelay;
            if (bs.PopInElapsed < popEnd) bs.PopInElapsed += dt;

            float popT = (bs.PopInElapsed - bs.PopInDelay) * invPopDur;
            if (popT > 1f) popT = 1f;
            float popEased = popT <= 0f ? 0f : EaseOutBack(popT);

            Matrix4x4 matrix;
            bool stillAnimating = true;

            if (bs.IsDestroying)
            {
                bs.DestroyElapsed += dt;
                float dt01 = bs.DestroyElapsed * invPaintDur;
                if (dt01 > 1f) dt01 = 1f;
                Vector3 ds = bs.Scale * EaseInBack(1f - dt01);
                Quaternion rot = Quaternion.Euler(dt01 * 180f, dt01 * 180f, 0f);
                matrix = Matrix4x4.TRS(bs.Position, rot, ds);

                if (dt01 >= 1f)
                {
                    bs.IsDead = true;
                    stillAnimating = false;
                    matrix = default;
                    _model.MarkBlockDestroyed(blockIdx);
                }
            }
            else
            {
                matrix = BuildIdentityTRS(bs.Position, bs.Scale * popEased);
                if (popT >= 1f) stillAnimating = false;
            }

            _normalBatches[bs.BatchIndex][bs.SlotIndex] = matrix;

            if (!stillAnimating)
            {
                bs.IsAnimating = false;
                int last = _animatingBlocks.Count - 1;
                _animatingBlocks[li] = _animatingBlocks[last];
                _animatingBlocks.RemoveAt(last);
            }
        }
    }

    private void DrawInstances()
    {
        if (_normalMaterial != null)
        {
            for (int b = 0; b < _normalBatchCount; b++)
            {
                int count = _normalBatchCounts[b];
                if (count == 0) continue;
                var mpb = _normalBatchMpbs[b];
                if (_normalBatchColorsDirty[b])
                {
                    mpb.SetVectorArray(BaseColorId, _normalBatchColors[b]);
                    _normalBatchColorsDirty[b] = false;
                }
                Graphics.DrawMeshInstanced(_cubeMesh, 0, _normalMaterial, _normalBatches[b], count, mpb);
            }
        }

        if (_stoneMaterial != null)
        {
            for (int b = 0; b < _stoneBatchCount; b++)
            {
                int count = _stoneBatchCounts[b];
                if (count == 0) continue;
                Graphics.DrawMeshInstanced(_cubeMesh, 0, _stoneMaterial, _stoneBatches[b], count);
            }
        }
    }

    private static Matrix4x4 BuildIdentityTRS(Vector3 pos, float uniformScale)
    {
        Matrix4x4 m = default;
        m.m00 = uniformScale;
        m.m11 = uniformScale;
        m.m22 = uniformScale;
        m.m03 = pos.x;
        m.m13 = pos.y;
        m.m23 = pos.z;
        m.m33 = 1f;
        return m;
    }

    private static Matrix4x4 BuildIdentityTRS(Vector3 pos, Vector3 scale)
    {
        Matrix4x4 m = default;
        m.m00 = scale.x;
        m.m11 = scale.y;
        m.m22 = scale.z;
        m.m03 = pos.x;
        m.m13 = pos.y;
        m.m23 = pos.z;
        m.m33 = 1f;
        return m;
    }

    private static float EaseOutBack(float t)
    {
        const float c = 1.70158f;
        t -= 1f;
        return 1f + (c + 1f) * t * t * t + c * t * t;
    }

    private static float EaseInBack(float t)
    {
        const float c = 1.70158f;
        return (c + 1f) * t * t * t - c * t * t;
    }

    private static Mesh BuildDefaultCubeMesh()
    {
        var proto = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mesh = proto.GetComponent<MeshFilter>().sharedMesh;
        var collider = proto.GetComponent<Collider>();
        if (collider != null) DestroyImmediate(collider);
        DestroyImmediate(proto);
        return mesh;
    }
}
