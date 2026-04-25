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
        if (bullet != null)
        {
            var payload = new BulletController.Payload
            {
                From = pig.transform.position,
                To = _renderer.GetCellWorldPos(target),
                Color = pig.TintColor,
                TargetCell = target,
                BlockId = blockId,
                ColorIndex = pig.ColorIndex,
                IsBlockDestroyed = blockId >= 0 && result == HitResult.Destroyed,
                RemainingHealth = remainingHealth
            };
            bullet.Launch(payload, _config.BulletFlightDuration);
        }

        _eventBus.Raise(new ShotFired { PigId = pig.Id, Target = target, ColorIndex = pig.ColorIndex });
        return true;
    }
}
