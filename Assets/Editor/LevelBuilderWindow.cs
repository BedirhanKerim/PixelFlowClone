using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LevelBuilderWindow : EditorWindow
{
    private enum DownscaleFilter { Point, Bilinear }

    private Texture2D _source;
    private Vector2Int _gridSize = new Vector2Int(20, 20);
    private int _maxColors = 6;
    private float _paletteMergeThreshold = 55f;
    private DownscaleFilter _filter = DownscaleFilter.Bilinear;
    private string _levelName = "";

    private Color32[] _sampledPixels;
    private List<Color32> _palette;
    private byte[] _indices;
    private Texture2D _previewTex;
    private bool _previewDirty;

    [MenuItem("PixelFlow/Level Builder")]
    public static void Open()
    {
        var window = GetWindow<LevelBuilderWindow>("Level Builder");
        window.minSize = new Vector2(420, 600);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Level Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawImportSection();
        EditorGUILayout.Space();
        DrawPaletteSection();
        EditorGUILayout.Space();
        DrawPreviewSection();
        EditorGUILayout.Space();
        DrawSaveSection();

        if (_previewDirty)
        {
            RebuildPreview();
            _previewDirty = false;
            Repaint();
        }
    }

    private void DrawImportSection()
    {
        EditorGUILayout.LabelField("1. Import", EditorStyles.boldLabel);

        var newSource = (Texture2D)EditorGUILayout.ObjectField("Source Image", _source, typeof(Texture2D), false);
        if (newSource != _source)
        {
            _source = newSource;
            _levelName = _source != null ? _source.name : "";
            _previewDirty = true;
        }

        int newW = EditorGUILayout.IntSlider("Grid Width", _gridSize.x, 6, 40);
        int newH = EditorGUILayout.IntSlider("Grid Height", _gridSize.y, 6, 40);
        if (newW != _gridSize.x || newH != _gridSize.y)
        {
            _gridSize = new Vector2Int(newW, newH);
            _previewDirty = true;
        }

        var newFilter = (DownscaleFilter)EditorGUILayout.EnumPopup("Downscale Filter", _filter);
        if (newFilter != _filter)
        {
            _filter = newFilter;
            _previewDirty = true;
        }
    }

    private void DrawPaletteSection()
    {
        EditorGUILayout.LabelField("2. Palette", EditorStyles.boldLabel);

        int newMax = EditorGUILayout.IntSlider("Max Colors", _maxColors, 2, 12);
        if (newMax != _maxColors)
        {
            _maxColors = newMax;
            _previewDirty = true;
        }

        float newThreshold = EditorGUILayout.Slider("Merge Threshold", _paletteMergeThreshold, 20f, 120f);
        if (!Mathf.Approximately(newThreshold, _paletteMergeThreshold))
        {
            _paletteMergeThreshold = newThreshold;
            _previewDirty = true;
        }

        if (_palette != null && _palette.Count > 0)
        {
            EditorGUILayout.LabelField($"Detected: {_palette.Count} colors");
            var rect = GUILayoutUtility.GetRect(200, 24);
            float swatchW = rect.width / _palette.Count;
            for (int i = 0; i < _palette.Count; i++)
            {
                var r = new Rect(rect.x + i * swatchW, rect.y, swatchW, rect.height);
                EditorGUI.DrawRect(r, _palette[i]);
            }
        }
    }

    private void DrawPreviewSection()
    {
        EditorGUILayout.LabelField("3. Preview", EditorStyles.boldLabel);

        if (_previewTex == null)
        {
            EditorGUILayout.HelpBox("Assign a source image above.", MessageType.Info);
            return;
        }

        float size = Mathf.Min(position.width - 40, 320);
        var rect = GUILayoutUtility.GetRect(size, size);
        rect.width = size;
        rect.height = size;
        EditorGUI.DrawPreviewTexture(rect, _previewTex, null, ScaleMode.StretchToFill);
    }

    private void DrawSaveSection()
    {
        EditorGUILayout.LabelField("4. Save", EditorStyles.boldLabel);
        _levelName = EditorGUILayout.TextField("Level Name", _levelName);

        GUI.enabled = _source != null && _indices != null && _palette != null && !string.IsNullOrWhiteSpace(_levelName);
        if (GUILayout.Button("Save as LevelData", GUILayout.Height(28)))
        {
            SaveLevel();
        }
        GUI.enabled = true;
    }

    private void RebuildPreview()
    {
        if (_source == null)
        {
            _palette = null;
            _indices = null;
            _sampledPixels = null;
            if (_previewTex != null) DestroyImmediate(_previewTex);
            _previewTex = null;
            return;
        }

        EnsureReadable(_source);
        SamplePixels();
        BuildPalette();
        QuantizeIndices();
        BuildPreviewTexture();
    }

    private void SamplePixels()
    {
        int total = _gridSize.x * _gridSize.y;
        _sampledPixels = new Color32[total];
        int w = _source.width;
        int h = _source.height;

        for (int y = 0; y < _gridSize.y; y++)
        {
            for (int x = 0; x < _gridSize.x; x++)
            {
                float u = (x + 0.5f) / _gridSize.x;
                float v = (y + 0.5f) / _gridSize.y;
                _sampledPixels[y * _gridSize.x + x] = _filter == DownscaleFilter.Point
                    ? SamplePoint(u, v, w, h)
                    : SampleBilinear(u, v);
            }
        }
    }

    private Color32 SamplePoint(float u, float v, int w, int h)
    {
        int sx = Mathf.Clamp(Mathf.FloorToInt(u * w), 0, w - 1);
        int sy = Mathf.Clamp(Mathf.FloorToInt(v * h), 0, h - 1);
        return _source.GetPixel(sx, sy);
    }

    private Color32 SampleBilinear(float u, float v)
    {
        return _source.GetPixelBilinear(u, v);
    }

    private void BuildPalette()
    {
        _palette = new List<Color32>();
        for (int i = 0; i < _sampledPixels.Length; i++)
        {
            var c = _sampledPixels[i];
            bool unique = true;
            for (int p = 0; p < _palette.Count; p++)
            {
                if (ColorDistance(c, _palette[p]) < _paletteMergeThreshold)
                {
                    unique = false;
                    break;
                }
            }
            if (unique)
            {
                _palette.Add(c);
                if (_palette.Count >= _maxColors) break;
            }
        }
        if (_palette.Count == 0) _palette.Add(new Color32(128, 128, 128, 255));
    }

    private void QuantizeIndices()
    {
        _indices = new byte[_sampledPixels.Length];
        for (int i = 0; i < _sampledPixels.Length; i++)
        {
            _indices[i] = (byte)NearestPaletteIndex(_sampledPixels[i]);
        }
    }

    private int NearestPaletteIndex(Color32 c)
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < _palette.Count; i++)
        {
            float d = ColorDistance(c, _palette[i]);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    private void BuildPreviewTexture()
    {
        if (_previewTex != null) DestroyImmediate(_previewTex);
        _previewTex = new Texture2D(_gridSize.x, _gridSize.y, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[_indices.Length];
        for (int y = 0; y < _gridSize.y; y++)
        {
            for (int x = 0; x < _gridSize.x; x++)
            {
                int i = y * _gridSize.x + x;
                pixels[i] = _palette[_indices[i]];
            }
        }
        _previewTex.SetPixels32(pixels);
        _previewTex.Apply();
    }

    private void SaveLevel()
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        level.LevelName = _levelName;
        level.GridSize = _gridSize;
        level.PaletteColors = _palette.ToArray();
        level.CellColorIndices = (byte[])_indices.Clone();
        level.StoneMask = new bool[_indices.Length];
        AutoFillPigs(level);

        const string folder = "Assets/ScriptableObjects/Levels";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Levels");
        }

        string path = $"{folder}/Level_{_levelName}.asset";
        path = AssetDatabase.GenerateUniqueAssetPath(path);
        AssetDatabase.CreateAsset(level, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = level;
        EditorGUIUtility.PingObject(level);
        Debug.Log($"Saved {path}");
    }

    private void AutoFillPigs(LevelData level)
    {
        var counts = new int[_palette.Count];
        for (int i = 0; i < _indices.Length; i++) counts[_indices[i]]++;

        var all = new List<PigConfig>();
        for (byte c = 0; c < _palette.Count; c++)
        {
            int needed = counts[c];
            if (needed == 0) continue;
            int ammoPerPig = needed > 15 ? 20 : 10;
            int pigCount = Mathf.CeilToInt((float)needed / ammoPerPig);
            for (int p = 0; p < pigCount; p++)
            {
                all.Add(new PigConfig { ColorIndex = c, Ammo = ammoPerPig });
            }
        }

        int shelfCount = Mathf.Min(5, all.Count);
        level.ShelfPigs = new PigConfig[shelfCount];
        for (int i = 0; i < shelfCount; i++) level.ShelfPigs[i] = all[i];

        int queueCount = Mathf.Max(0, all.Count - shelfCount);
        level.QueuePigs = new PigConfig[queueCount];
        for (int i = 0; i < queueCount; i++) level.QueuePigs[i] = all[shelfCount + i];
    }

    private static void EnsureReadable(Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    private static float ColorDistance(Color32 a, Color32 b)
    {
        int dr = a.r - b.r;
        int dg = a.g - b.g;
        int db = a.b - b.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }
}
