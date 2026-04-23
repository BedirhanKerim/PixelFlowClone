using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public static class TweenExtensions
{
    public static async UniTask AwaitCompletion(this Tween tween, CancellationToken ct = default)
    {
        if (tween == null) return;
        while (tween.IsActive() && !tween.IsComplete())
        {
            if (ct.IsCancellationRequested)
            {
                tween.Kill();
                return;
            }
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
    }
}
