using DG.Tweening;
using UnityEngine;
using VContainer;

public class GridRenderer : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly Color PlaceholderTint = new Color(0.3f, 0.3f, 0.3f, 1f);
    private static readonly Color StoneColor = new Color(0.18f, 0.18f, 0.22f, 1f);

    [Inject] private GameEventBus _eventBus;
    [Inject] private GameConfig _config;
    [Inject] private GridModel _model;

    private Mesh _cubeMesh;
    private Material _sharedNormalMat;
    private Material _sharedStoneMat;
    private Transform _cubesRoot;
    private Transform[] _cubes;
    private MeshRenderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private float _cubeSize;
    private Vector3 _originOffset;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _cubeMesh = BuildCubeMesh();
        _sharedNormalMat = CreateInstancedMaterial();
        _sharedStoneMat = CreateInstancedMaterial();
        _cubesRoot = new GameObject("Cubes").transform;
        _cubesRoot.SetParent(transform, false);
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
    public Vector3 GetCellWorldPos(CellAddress c) => _originOffset + new Vector3(c.X * _cubeSize, 0f, c.Y * _cubeSize);

    private void OnLevelLoaded(ref LevelLoaded e)
    {
        BuildGrid(e.Data);
    }

    private void OnCellPainted(ref CellPainted e)
    {
        int idx = _model.Index(e.Cell);
        if (_renderers[idx] == null) return;
        var color = (Color)_model.PaletteColor(e.ColorIndex);
        _renderers[idx].GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, color);
        _renderers[idx].SetPropertyBlock(_mpb);
        _cubes[idx].DOPunchScale(Vector3.one * 0.25f, _config.CellPaintDuration, 6, 0.5f);
    }

    private void BuildGrid(LevelData data)
    {
        ClearCubes();

        var size = data.GridSize;
        int total = size.x * size.y;
        _cubes = new Transform[total];
        _renderers = new MeshRenderer[total];

        _cubeSize = _config.BoardWorldWidth / Mathf.Max(size.x, size.y);
        float halfBoardX = (size.x - 1) * _cubeSize * 0.5f;
        float halfBoardZ = (size.y - 1) * _cubeSize * 0.5f;
        _originOffset = new Vector3(-halfBoardX, 0f, -halfBoardZ);

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                int i = y * size.x + x;
                CellType type = data.CellTypes[i];
                if (type == CellType.Empty) continue;

                byte colorIndex = data.CellColorIndices[i];
                Color target = data.PaletteColors[colorIndex];

                var go = new GameObject($"Cell_{x}_{y}");
                go.transform.SetParent(_cubesRoot, false);
                go.transform.localPosition = _originOffset + new Vector3(x * _cubeSize, 0f, y * _cubeSize);
                go.transform.localScale = Vector3.zero;

                go.AddComponent<MeshFilter>().sharedMesh = _cubeMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = type == CellType.Stone ? _sharedStoneMat : _sharedNormalMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                Color initial = type == CellType.Stone ? StoneColor : BlendToPlaceholder(target);
                mr.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, initial);
                mr.SetPropertyBlock(_mpb);

                _cubes[i] = go.transform;
                _renderers[i] = mr;
            }
        }

        AnimatePopIn();
    }

    private void AnimatePopIn()
    {
        Vector3 scale = Vector3.one * _cubeSize * 0.95f;
        int stagger = 0;
        for (int i = 0; i < _cubes.Length; i++)
        {
            if (_cubes[i] == null) continue;
            _cubes[i].DOScale(scale, _config.CubePopInDuration)
                .SetEase(Ease.OutBack)
                .SetDelay(stagger++ * _config.CubePopInStagger);
        }
    }

    private void ClearCubes()
    {
        if (_cubes == null) return;
        for (int i = 0; i < _cubes.Length; i++)
        {
            if (_cubes[i] != null)
            {
                _cubes[i].DOKill();
                Destroy(_cubes[i].gameObject);
            }
        }
        _cubes = null;
        _renderers = null;
    }

    private Color BlendToPlaceholder(Color target) => Color.Lerp(target, PlaceholderTint, 0.72f);

    private Material CreateInstancedMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader) { enableInstancing = true };
        mat.SetFloat("_Smoothness", 0.15f);
        mat.SetFloat("_Metallic", 0f);
        return mat;
    }

    private static Mesh BuildCubeMesh()
    {
        var proto = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mesh = proto.GetComponent<MeshFilter>().sharedMesh;
        var collider = proto.GetComponent<Collider>();
        if (collider != null) DestroyImmediate(collider);
        DestroyImmediate(proto);
        return mesh;
    }
}
