using UnityEngine;
using VContainer;

public class ShootingService : MonoBehaviour
{
    [Inject] private GameEventBus _eventBus;
    [Inject] private GridModel _model;
    [Inject] private GridRenderer _renderer;
    [Inject] private GameConfig _config;
    [Inject] private BulletFactory _bulletFactory;

    public bool TryFire(PigEntity pig, CellAddress lineStart, Vector2Int direction)
    {
        if (!pig.HasAmmo) return false;
        if (!_model.TryFindFirstMatch(lineStart, direction, pig.ColorIndex, out var target)) return false;

        pig.ConsumeAmmo();
        _model.TryPaint(target, pig.ColorIndex);

        var bullet = _bulletFactory.Get();
        Vector3 from = pig.transform.position;
        Vector3 to = _renderer.GetCellWorldPos(target);
        bullet.Setup(from, pig.TintColor);
        bullet.FlyTo(to, _config.BulletFlightDuration, () =>
        {
            _bulletFactory.Release(bullet);
            _eventBus.Raise(new CellPainted { Cell = target, ColorIndex = pig.ColorIndex });
        });

        _eventBus.Raise(new ShotFired { PigId = pig.Id, Target = target, ColorIndex = pig.ColorIndex });
        return true;
    }
}
