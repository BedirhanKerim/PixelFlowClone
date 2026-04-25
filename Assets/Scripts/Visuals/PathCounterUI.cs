using DG.Tweening;
using TMPro;
using UnityEngine;
using VContainer;

public class PathCounterUI : MonoBehaviour
{
    [Inject] private GameEventBus _eventBus;
    [Inject] private GameConfig _config;

    [SerializeField] private TextMeshPro _text;
    [SerializeField] private float _bounceStrength = 0.25f;
    [SerializeField] private float _bounceDuration = 0.25f;

    private Vector3 _baseScale;
    private bool _baseScaleCached;

    private void Awake()
    {
        if (_text != null)
        {
            _baseScale = _text.transform.localScale;
            _baseScaleCached = true;
        }
    }

    private void OnEnable()
    {
        if (_eventBus == null) return;
        _eventBus.SubscribeTo<ActivePigCountChanged>(OnCountChanged);
        UpdateText(0, false);
    }

    private void OnDisable()
    {
        if (_eventBus == null) return;
        _eventBus.UnsubscribeFrom<ActivePigCountChanged>(OnCountChanged);
    }

    private void OnCountChanged(ref ActivePigCountChanged e)
    {
        UpdateText(e.Count, true);
    }

    private void UpdateText(int activeCount, bool bounce)
    {
        if (_text == null) return;
        int max = _config.MaxSimultaneousPigsOnPath;
        int remaining = Mathf.Max(0, max - activeCount);
        _text.text = $"{remaining}/{max}";
        if (bounce && _baseScaleCached) PlayBounce();
    }

    private void PlayBounce()
    {
        var tr = _text.transform;
        tr.DOKill();
        tr.localScale = _baseScale;
        tr.DOPunchScale(_baseScale * _bounceStrength, _bounceDuration, 1, 0.5f);
    }
}
