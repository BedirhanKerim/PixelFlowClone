using UnityEngine;

[CreateAssetMenu(menuName = "PixelFlow/Level", fileName = "Level")]
public class LevelData : ScriptableObject
{
    public string LevelName;
    public Vector2Int GridSize = new Vector2Int(20, 20);
    public Color32[] PaletteColors;
    public byte[] CellColorIndices;
    public bool[] StoneMask;
    public PigConfig[] ShelfPigs;
    public PigConfig[] QueuePigs;
}
