using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
    public interface IAnimationStrategy
    {
        Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default);
        Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup);
        
        UniTask PlayShowAsync(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default,
                              CancellationToken ct = default);

        UniTask PlayHideAsync(RectTransform target, CanvasGroup canvasGroup,
                              CancellationToken ct = default);
    }
}