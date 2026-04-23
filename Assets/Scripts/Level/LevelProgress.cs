public class LevelProgress
{
    public int CurrentIndex { get; private set; }

    public void SetIndex(int index) => CurrentIndex = index;
    public void Advance() => CurrentIndex++;
    public void Reset() => CurrentIndex = 0;
}
