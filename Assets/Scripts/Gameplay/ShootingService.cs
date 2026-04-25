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

        var result = _model.TryHit(target, pig.ColorIndex, out var blockId, out var remainingHealth);
        if (result == HitResult.Missed) return false;

        pig.ConsumeAmmo();

        var bullet = _bulletFactory.Get();
        Vector3 from = pig.transform.position;
        Vector3 to = _renderer.GetCellWorldPos(target);
        bullet.Setup(from, pig.TintColor);

        byte colorIndex = pig.ColorIndex;
        int capturedBlockId = blockId;
        int capturedHp = remainingHealth;

        if (capturedBlockId >= 0)
        {
            if (result == HitResult.Destroyed)
            {
                bullet.FlyTo(to, _config.BulletFlightDuration, () =>
                {
                    _bulletFactory.Release(bullet);
                    _eventBus.Raise(new BlockPainted { BlockId = capturedBlockId, ColorIndex = colorIndex });
                });
            }
            else
            {
                bullet.FlyTo(to, _config.BulletFlightDuration, () =>
                {
                    _bulletFactory.Release(bullet);
                    _eventBus.Raise(new BlockDamaged { BlockId = capturedBlockId, RemainingHealth = capturedHp });
                });
            }
        }
        else
        {
            bullet.FlyTo(to, _config.BulletFlightDuration, () =>
            {
                _bulletFactory.Release(bullet);
                _eventBus.Raise(new CellPainted { Cell = target, ColorIndex = colorIndex });
            });
        }

        _eventBus.Raise(new ShotFired { PigId = pig.Id, Target = target, ColorIndex = colorIndex });
        return true;
    }
}
