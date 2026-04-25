using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

public class GridRenderer : MonoBehaviour
{
    private const int InstanceBatchLimit = 1023;
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

    private struct CubeState
    {
        public Vector3 Position;
        public Color PlaceholderColor;
        public Color FinalColor;
        public float PopInDelay;
        public float PopInElapsed;
        public float DestroyElapsed;
        public bool IsStone;
        public bool IsDestroying;
        public bool IsDead;
    }

    private struct BlockState
    {
        public int Id;
        public Vector3 Position;
        public Vector3 Scale;
        public Color Color;
        public float PopInDelay;
        public float PopInElapsed;
        public float DestroyElapsed;
        public bool IsDestroying;
        public bool IsDead;
    }

    private CubeState[] _states;
    private BlockState[] _blockStates;
    private Matrix4x4[] _normalMatrices;
    private Vector4[] _normalColors;
    private Matrix4x4[] _stoneMatrices;
    private readonly Matrix4x4[] _batchMatrices = new Matrix4x4[InstanceBatchLimit];
    private readonly Vector4[] _batchColors = new Vector4[InstanceBatchLimit];
    private int _cellCount;
    private float _cubeSize;
    private Vector3 _originOffset;
    private MaterialPropertyBlock _mpb;

    private bool _hasGrid;

    private ObjectPool<TextMeshPro> _hpTextPool;
    private readonly Dictionary<int, TextMeshPro> _blockHpTexts = new Dictionary<int, TextMeshPro>();

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (_cubeMesh == null) _cubeMesh = BuildDefaultCubeMesh();
        _hpTextPool = new ObjectPool<TextMeshPro>(
            createFunc: CreateHpText,
            actionOnGet: t => t.gameObject.SetActive(true),
            actionOnRelease: t => { if (t != null) t.gameObject.SetActive(false); },
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
        if (_hpTextPrefab == null)
        {
            Debug.LogError("GridRenderer: HP text prefab not assigned.");
            return null;
        }
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
        if (idx < 0 || idx >= _states.Length) return;
        ref var state = ref _states[idx];
        if (state.IsDead) return;

        _model.MarkDestroyed(e.Cell);
        state.FinalColor = _model.PaletteColor(e.ColorIndex);
        state.IsDestroying = true;
        state.DestroyElapsed = 0f;
    }

    private void OnBlockDamaged(ref BlockDamaged e)
    {
        if (_blockHpTexts.TryGetValue(e.BlockId, out var t) && t != null)
        {
            t.text = e.RemainingHealth.ToString();
        }
    }

    private void OnBlockPainted(ref BlockPainted e)
    {
        if (_blockHpTexts.TryGetValue(e.BlockId, out var t))
        {
            if (t != null) _hpTextPool.Release(t);
            _blockHpTexts.Remove(e.BlockId);
        }
        if (_blockStates != null && e.BlockId >= 0 && e.BlockId < _blockStates.Length)
        {
            ref var bs = ref _blockStates[e.BlockId];
            if (!bs.IsDead && !bs.IsDestroying)
            {
                bs.IsDestroying = true;
                bs.DestroyElapsed = 0f;
            }
        }
    }

    private void BuildGrid(LevelData data)
    {
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

        int normalBuffer = 0;
        int stoneBuffer = 0;
        for (int i = 0; i < _cellCount; i++)
        {
            var type = data.CellTypes[i];
            if (type == CellType.Empty || type == CellType.HealthBlock)
            {
                _states[i].IsDead = true;
                continue;
            }

            int x = i % data.GridSize.x;
            int y = i / data.GridSize.x;
            var pos = _originOffset + new Vector3(x * _cubeSize, 0f, y * _cubeSize);

            _states[i] = new CubeState
            {
                Position = pos,
                PopInDelay = i * stagger,
                PopInElapsed = 0f,
                IsStone = type == CellType.Stone,
                IsDead = false,
                IsDestroying = false,
                DestroyElapsed = 0f,
                FinalColor = data.PaletteColors[data.CellColorIndices[i]],
                PlaceholderColor = type == CellType.Stone
                    ? StoneColor
                    : (Color)data.PaletteColors[data.CellColorIndices[i]]
            };

            if (_states[i].IsStone) stoneBuffer++;
            else normalBuffer++;
        }

        BuildBlockStates(data, stagger);

        _normalMatrices = new Matrix4x4[normalBuffer + _blockStates.Length];
        _normalColors = new Vector4[normalBuffer + _blockStates.Length];
        _stoneMatrices = new Matrix4x4[stoneBuffer];

        SpawnBlockHpTexts(data);

        _hasGrid = true;
    }

    private void BuildBlockStates(LevelData data, float stagger)
    {
        if (data.ManualBlocks == null || data.ManualBlocks.Length == 0)
        {
            _blockStates = System.Array.Empty<BlockState>();
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

            _blockStates[b] = new BlockState
            {
                Id = b,
                Position = center,
                Scale = new Vector3(mb.Size.x, 1f, mb.Size.y) * (_cubeSize * 0.95f),
                Color = mb.ColorIndex < data.PaletteColors.Length
                    ? (Color)data.PaletteColors[mb.ColorIndex]
                    : Color.white,
                PopInDelay = popDelayIndex * stagger,
                PopInElapsed = 0f,
                DestroyElapsed = 0f,
                IsDestroying = false,
                IsDead = false
            };
        }
    }

    private void SpawnBlockHpTexts(LevelData data)
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
            t.text = mb.Health.ToString();
            _blockHpTexts[b] = t;
        }
    }

    private void LateUpdate()
    {
        if (!_hasGrid || _states == null) return;

        int normalCount = 0;
        int stoneCount = 0;
        float popDur = _config.CubePopInDuration;
        float paintDur = _config.CellPaintDuration;
        float dt = Time.deltaTime;
        Vector3 cellTargetScale = Vector3.one * _cubeSize * 0.95f;

        for (int i = 0; i < _cellCount; i++)
        {
            ref var s = ref _states[i];
            if (s.IsDead) continue;

            if (s.PopInElapsed < popDur + s.PopInDelay)
            {
                s.PopInElapsed += dt;
            }

            float popT = Mathf.Clamp01((s.PopInElapsed - s.PopInDelay) / Mathf.Max(popDur, 0.0001f));
            popT = popT <= 0f ? 0f : EaseOutBack(popT);

            Vector3 scale = cellTargetScale * popT;
            Quaternion rot = Quaternion.identity;
            Color color = s.IsStone ? StoneColor : s.PlaceholderColor;

            if (s.IsDestroying)
            {
                s.DestroyElapsed += dt;
                float dt01 = Mathf.Clamp01(s.DestroyElapsed / Mathf.Max(paintDur, 0.0001f));
                color = s.FinalColor;
                scale = cellTargetScale * EaseInBack(1f - dt01);
                rot = Quaternion.Euler(dt01 * 180f, dt01 * 180f, 0f);

                if (dt01 >= 1f)
                {
                    s.IsDead = true;
                    continue;
                }
            }

            var matrix = Matrix4x4.TRS(s.Position, rot, scale);
            if (s.IsStone)
            {
                if (stoneCount < _stoneMatrices.Length) _stoneMatrices[stoneCount++] = matrix;
            }
            else
            {
                if (normalCount < _normalMatrices.Length)
                {
                    _normalMatrices[normalCount] = matrix;
                    _normalColors[normalCount] = color;
                    normalCount++;
                }
            }
        }

        for (int b = 0; b < _blockStates.Length; b++)
        {
            ref var bs = ref _blockStates[b];
            if (bs.IsDead) continue;

            if (bs.PopInElapsed < popDur + bs.PopInDelay)
            {
                bs.PopInElapsed += dt;
            }

            float popT = Mathf.Clamp01((bs.PopInElapsed - bs.PopInDelay) / Mathf.Max(popDur, 0.0001f));
            popT = popT <= 0f ? 0f : EaseOutBack(popT);

            Vector3 scale = bs.Scale * popT;
            Quaternion rot = Quaternion.identity;

            if (bs.IsDestroying)
            {
                bs.DestroyElapsed += dt;
                float dt01 = Mathf.Clamp01(bs.DestroyElapsed / Mathf.Max(paintDur, 0.0001f));
                scale = bs.Scale * EaseInBack(1f - dt01);
                rot = Quaternion.Euler(dt01 * 180f, dt01 * 180f, 0f);

                if (dt01 >= 1f)
                {
                    bs.IsDead = true;
                    _model.MarkBlockDestroyed(bs.Id);
                    continue;
                }
            }

            var matrix = Matrix4x4.TRS(bs.Position, rot, scale);
            if (normalCount < _normalMatrices.Length)
            {
                _normalMatrices[normalCount] = matrix;
                _normalColors[normalCount] = bs.Color;
                normalCount++;
            }
        }

        DrawInstances(normalCount, stoneCount);
    }

    private void DrawInstances(int normalCount, int stoneCount)
    {
        if (normalCount > 0 && _normalMaterial != null)
        {
            int drawn = 0;
            while (drawn < normalCount)
            {
                int batch = Mathf.Min(InstanceBatchLimit, normalCount - drawn);
                System.Array.Copy(_normalMatrices, drawn, _batchMatrices, 0, batch);
                System.Array.Copy(_normalColors, drawn, _batchColors, 0, batch);

                _mpb.Clear();
                _mpb.SetVectorArray(BaseColorId, _batchColors);
                Graphics.DrawMeshInstanced(_cubeMesh, 0, _normalMaterial, _batchMatrices, batch, _mpb);
                drawn += batch;
            }
        }

        if (stoneCount > 0 && _stoneMaterial != null)
        {
            int drawn = 0;
            while (drawn < stoneCount)
            {
                int batch = Mathf.Min(InstanceBatchLimit, stoneCount - drawn);
                System.Array.Copy(_stoneMatrices, drawn, _batchMatrices, 0, batch);
                Graphics.DrawMeshInstanced(_cubeMesh, 0, _stoneMaterial, _batchMatrices, batch);
                drawn += batch;
            }
        }
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
