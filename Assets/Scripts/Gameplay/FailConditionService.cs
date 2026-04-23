using VContainer;
using VContainer.Unity;

public class FailConditionService : IStartable, System.IDisposable
{
    private readonly GameEventBus _eventBus;

    [Inject]
    public FailConditionService(GameEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void Start()
    {
        _eventBus.SubscribeTo<ShelfOverflowFail>(OnShelfOverflow);
    }

    public void Dispose()
    {
        _eventBus.UnsubscribeFrom<ShelfOverflowFail>(OnShelfOverflow);
    }

    private void OnShelfOverflow(ref ShelfOverflowFail e)
    {
        _eventBus.Raise(new LevelFailed { Reason = FailReason.ShelfOverflow });
    }
}
