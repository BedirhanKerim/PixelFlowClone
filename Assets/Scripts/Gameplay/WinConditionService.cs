using VContainer;
using VContainer.Unity;

public class WinConditionService : IStartable, System.IDisposable
{
    private readonly GameEventBus _eventBus;
    private readonly GridModel _model;
    private readonly LevelProgress _progress;

    [Inject]
    public WinConditionService(GameEventBus eventBus, GridModel model, LevelProgress progress)
    {
        _eventBus = eventBus;
        _model = model;
        _progress = progress;
    }

    public void Start()
    {
        _eventBus.SubscribeTo<CellPainted>(OnCellPainted);
    }

    public void Dispose()
    {
        _eventBus.UnsubscribeFrom<CellPainted>(OnCellPainted);
    }

    private void OnCellPainted(ref CellPainted e)
    {
        if (_model.IsLevelComplete())
        {
            _eventBus.Raise(new LevelCompleted { LevelIndex = _progress.CurrentIndex });
        }
    }
}
