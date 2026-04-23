using UnityEngine;
using VContainer;

public class GameFlowController : MonoBehaviour
{
    [Inject] private GameEventBus _eventBus;

    public Phase CurrentPhase { get; private set; } = Phase.Loading;

    private void OnEnable()
    {
        _eventBus.SubscribeTo<LevelLoaded>(OnLevelLoaded);
        _eventBus.SubscribeTo<LevelCompleted>(OnLevelCompleted);
        _eventBus.SubscribeTo<LevelFailed>(OnLevelFailed);
    }

    private void OnDisable()
    {
        _eventBus.UnsubscribeFrom<LevelLoaded>(OnLevelLoaded);
        _eventBus.UnsubscribeFrom<LevelCompleted>(OnLevelCompleted);
        _eventBus.UnsubscribeFrom<LevelFailed>(OnLevelFailed);
    }

    public void SetPhase(Phase phase)
    {
        if (CurrentPhase == phase) return;
        var prev = CurrentPhase;
        CurrentPhase = phase;
        _eventBus.Raise(new PhaseChanged { From = prev, To = phase });
    }

    private void OnLevelLoaded(ref LevelLoaded e) => SetPhase(Phase.Playing);
    private void OnLevelCompleted(ref LevelCompleted e) => SetPhase(Phase.Won);
    private void OnLevelFailed(ref LevelFailed e) => SetPhase(Phase.Lost);
}
