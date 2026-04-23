using UnityEngine;
using VContainer;

public class LevelLoader : MonoBehaviour
{
    [Inject] private GameEventBus _eventBus;
    [Inject] private GameConfig _config;
    [Inject] private LevelLibrary _library;
    [Inject] private LevelProgress _progress;
    [Inject] private GridModel _model;

    private void Start()
    {
        LoadCurrent();
    }

    public void LoadCurrent()
    {
        if (_library == null || _library.Levels == null || _library.Levels.Length == 0) return;

        int index = Mathf.Clamp(_progress.CurrentIndex, 0, _library.Levels.Length - 1);
        var data = _library.Levels[index];

        _model.Initialize(data);
        _eventBus.Raise(new LevelLoadRequested { LevelIndex = index });
        _eventBus.Raise(new LevelLoaded { Data = data });
    }
}
