using UnityEngine;

public class PerimeterTrack
{
    public const int FiringSegmentCount = 4;

    public Vector3[] Corners { get; private set; } = new Vector3[4];
    public Quaternion[] SegmentRotations { get; private set; } = new Quaternion[FiringSegmentCount];
    public Vector3 FinishPoint { get; private set; }

    private Vector2Int _gridSize;

    private const int BackLeft = 0;
    private const int BackRight = 1;
    private const int TopRight = 2;
    private const int TopLeft = 3;

    public void Rebuild(Vector2Int gridSize, Vector3 backLeft, Vector3 backRight, Vector3 topRight, Vector3 topLeft, Vector3 finish)
    {
        _gridSize = gridSize;

        Corners[BackLeft] = backLeft;
        Corners[BackRight] = backRight;
        Corners[TopRight] = topRight;
        Corners[TopLeft] = topLeft;
        FinishPoint = finish;

        SegmentRotations[0] = Quaternion.Euler(0, 90, 0);
        SegmentRotations[1] = Quaternion.Euler(0, 0, 0);
        SegmentRotations[2] = Quaternion.Euler(0, 270, 0);
        SegmentRotations[3] = Quaternion.Euler(0, 180, 0);
    }

    public Vector3 GetWorldPosition(int segment, float progress)
    {
        Vector3 start, end;
        switch (segment)
        {
            case 0: start = Corners[BackLeft]; end = Corners[BackRight]; break;
            case 1: start = Corners[BackRight]; end = Corners[TopRight]; break;
            case 2: start = Corners[TopRight]; end = Corners[TopLeft]; break;
            case 3: start = Corners[TopLeft]; end = FinishPoint; break;
            default: return Vector3.zero;
        }
        return Vector3.Lerp(start, end, progress);
    }

    public Vector3 GetEntryPoint() => Corners[BackLeft];

    public bool GetLineOfSight(int segment, float progress, out CellAddress start, out Vector2Int direction)
    {
        switch (segment)
        {
            case 0:
            {
                int col = Mathf.Clamp(Mathf.FloorToInt(progress * _gridSize.x), 0, _gridSize.x - 1);
                start = new CellAddress(col, 0);
                direction = new Vector2Int(0, 1);
                return true;
            }
            case 1:
            {
                int row = Mathf.Clamp(Mathf.FloorToInt(progress * _gridSize.y), 0, _gridSize.y - 1);
                start = new CellAddress(_gridSize.x - 1, row);
                direction = new Vector2Int(-1, 0);
                return true;
            }
            case 2:
            {
                int col = _gridSize.x - 1 - Mathf.Clamp(Mathf.FloorToInt(progress * _gridSize.x), 0, _gridSize.x - 1);
                start = new CellAddress(col, _gridSize.y - 1);
                direction = new Vector2Int(0, -1);
                return true;
            }
            case 3:
            {
                int row = _gridSize.y - 1 - Mathf.Clamp(Mathf.FloorToInt(progress * _gridSize.y), 0, _gridSize.y - 1);
                start = new CellAddress(0, row);
                direction = new Vector2Int(1, 0);
                return true;
            }
        }
        start = default;
        direction = default;
        return false;
    }
}
