using UnityEngine;

[CreateAssetMenu(menuName = "PixelFlow/GameConfig", fileName = "GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("Board")]
    public float BoardWorldWidth = 10f;
    public Vector3 CameraEuler = new Vector3(55f, 0f, 0f);
    public float CameraDistanceMultiplier = 1.8f;

    [Header("Shelf / Queue")]
    public int ShelfSlotCount = 5;
    public int MaxSimultaneousPigsOnPath = 5;

    [Header("Tweens")]
    public float CubePopInDuration = 0.25f;
    public float CubePopInStagger = 0.01f;
    public float CellPaintDuration = 0.15f;
    public float BulletFlightDuration = 0.12f;
    public float PigLapDuration = 6f;
    public float LevelRevealDuration = 2f;
    public float LevelTransitionDuration = 0.6f;

    [Header("Ammo")]
    public int AmmoLow = 10;
    public int AmmoHigh = 20;

    [Header("Perimeter")]
    public float PerimeterOffset = 0.75f;
    public float PerimeterHeight = 0.3f;
}
