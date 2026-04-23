using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private GameConfig _config;
    [SerializeField] private LevelLibrary _library;
    [SerializeField] private LevelLoader _levelLoader;
    [SerializeField] private GridRenderer _gridRenderer;
    [SerializeField] private PigFactory _pigFactory;
    [SerializeField] private PigPathService _pigPathService;
    [SerializeField] private BulletFactory _bulletFactory;
    [SerializeField] private ShootingService _shootingService;
    [SerializeField] private InputService _inputService;
    [SerializeField] private GameFlowController _flowController;
    [SerializeField] private VfxService _vfxService;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<GameEventBus>(Lifetime.Singleton);
        builder.Register<GridModel>(Lifetime.Singleton);
        builder.Register<LevelProgress>(Lifetime.Singleton);
        builder.Register<QueueService>(Lifetime.Singleton);
        builder.Register(_ => new ShelfService(_config.ShelfSlotCount), Lifetime.Singleton);

        builder.RegisterEntryPoint<WinConditionService>(Lifetime.Singleton);
        builder.RegisterEntryPoint<FailConditionService>(Lifetime.Singleton);

        builder.RegisterInstance(_config);
        builder.RegisterInstance(_library);

        builder.RegisterComponent(_levelLoader);
        builder.RegisterComponent(_gridRenderer);
        builder.RegisterComponent(_pigFactory);
        builder.RegisterComponent(_pigPathService);
        builder.RegisterComponent(_bulletFactory);
        builder.RegisterComponent(_shootingService);
        builder.RegisterComponent(_inputService);
        builder.RegisterComponent(_flowController);
        builder.RegisterComponent(_vfxService);
    }
}
