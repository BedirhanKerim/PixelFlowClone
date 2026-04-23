using UnityEngine;
using VContainer;

public class GridRenderer : MonoBehaviour
{
    private const int InstanceBatchLimit = 1023;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly Color PlaceholderTint = new Color(0.3f, 0.3f, 0.3f, 1f);
    private static readonly Color StoneColor = new Color(0.18f, 0.18f, 0.22f, 1f);

    [Inject] private GameEventBus _eventBus;
    [Inject] private GameConfig _config;
    [Inject] private GridModel _model;

    [SerializeField] private Mesh _cubeMesh;
    [SerializeField] private Material _normalMaterial;
    [SerializeField] private Material _stoneMaterial;

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

    private CubeState[] _states;
    private Matrix4x4[] _normalMatrices;
    private Vector4[] _normalColors;
    private Matrix4x4[] _stoneMatrices;
    private readonly Matrix4x4[] _batchMatrices = new Matrix4x4[InstanceBatchLimit];
    private readonly Vector4[] _batchColors = new Vector4[InstanceBatchLimit];
    private int _totalCells;
    private int _cellCount;
    private float _cubeSize;
    private Vector3 _originOffset;
    private MaterialPropertyBlock _mpb;

    private bool _hasGrid;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (_cubeMesh == null) _cubeMesh = BuildDefaultCubeMesh();
    }

    private void OnEnable()
    {
        _eventBus.SubscribeTo<LevelLoaded>(OnLevelLoaded);
        _eventBus.SubscribeTo<CellPainted>(OnCellPainted);
    }

    private void OnDisable()
    {
        _eventBus.UnsubscribeFrom<LevelLoaded>(OnLevelLoaded);
        _eventBus.UnsubscribeFrom<CellPainted>(OnCellPainted);
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

    private void BuildGrid(LevelData data)
    {
        _cellCount = data.GridSize.x * data.GridSize.y;
        _totalCells = _cellCount;
        _states = new CubeState[_cellCount];

        _cubeSize = _config.BoardWorldWidth / Mathf.Max(data.GridSize.x, data.GridSize.y);
        float halfBoardX = (data.GridSize.x - 1) * _cubeSize * 0.5f;
        float halfBoardZ = (data.GridSize.y - 1) * _cubeSize * 0.5f;
        _originOffset = new Vector3(-halfBoardX, 0f, -halfBoardZ);

        int normalBuffer = 0;
        int stoneBuffer = 0;
        for (int i = 0; i < _cellCount; i++)
        {
            var type = data.CellTypes[i];
            if (type == CellType.Empty) { _states[i].IsDead = true; continue; }

            int x = i % data.GridSize.x;
            int y = i / data.GridSize.x;
            var pos = _originOffset + new Vector3(x * _cubeSize, 0f, y * _cubeSize);

            _states[i] = new CubeState
            {
                Position = pos,
                PopInDelay = i * _config.CubePopInStagger,
                PopInElapsed = 0f,
                IsStone = type == CellType.Stone,
                IsDead = false,
                IsDestroying = false,
                DestroyElapsed = 0f,
                FinalColor = data.PaletteColors[data.CellColorIndices[i]],
                PlaceholderColor = type == CellType.Stone
                    ? StoneColor
                    : BlendToPlaceholder(data.PaletteColors[data.CellColorIndices[i]])
            };

            if (_states[i].IsStone) stoneBuffer++;
            else normalBuffer++;
        }

        _normalMatrices = new Matrix4x4[normalBuffer];
        _normalColors = new Vector4[normalBuffer];
        _stoneMatrices = new Matrix4x4[stoneBuffer];
        _hasGrid = true;
    }

    private void LateUpdate()
    {
        if (!_hasGrid || _states == null) return;

        int normalCount = 0;
        int stoneCount = 0;
        float popDur = _config.CubePopInDuration;
        float paintDur = _config.CellPaintDuration;
        float dt = Time.deltaTime;
        Vector3 targetScale = Vector3.one * _cubeSize * 0.95f;

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

            Vector3 scale = targetScale * popT;
            Quaternion rot = Quaternion.identity;
            Color color = s.IsStone ? StoneColor : s.PlaceholderColor;

            if (s.IsDestroying)
            {
                s.DestroyElapsed += dt;
                float dt01 = Mathf.Clamp01(s.DestroyElapsed / Mathf.Max(paintDur, 0.0001f));
                color = s.FinalColor;
                scale = targetScale * EaseInBack(1f - dt01);
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

    private static Color BlendToPlaceholder(Color target) => target;

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
