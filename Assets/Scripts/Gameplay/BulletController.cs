using DG.Tweening;
using UnityEngine;

public class BulletController : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private MeshRenderer _renderer;
    [SerializeField] private TrailRenderer _trail;

    private MaterialPropertyBlock _mpb;

    public void Setup(Vector3 from, Color32 color)
    {
        transform.position = from;

        _mpb ??= new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, color);
        _renderer.SetPropertyBlock(_mpb);

        if (_trail != null)
        {
            _trail.startColor = color;
            _trail.endColor = new Color(color.r / 255f, color.g / 255f, color.b / 255f, 0f);
            _trail.Clear();
        }
    }

    public Tween FlyTo(Vector3 target, float duration, TweenCallback onComplete)
    {
        return transform.DOMove(target, duration).SetEase(Ease.Linear).OnComplete(onComplete);
    }
}
