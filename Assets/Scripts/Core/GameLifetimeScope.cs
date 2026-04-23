using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private GameConfig _config;
    [SerializeField] private LevelLibrary _library;
    [SerializeField] private LevelLoader _levelLoader;
    [SerializeField] private GridRenderer _gridRenderer;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<GameEventBus>(Lifetime.Singleton);
        builder.Register<GridModel>(Lifetime.Singleton);
        builder.Register<LevelProgress>(Lifetime.Singleton);

        builder.RegisterInstance(_config);
        builder.RegisterInstance(_library);

        builder.RegisterComponent(_levelLoader);
        builder.RegisterComponent(_gridRenderer);
    }
}
