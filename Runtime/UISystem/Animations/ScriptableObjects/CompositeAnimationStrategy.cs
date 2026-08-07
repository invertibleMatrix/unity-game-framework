using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "CompositeAnimation", menuName = "AK/UI/Animations/Composite Animation")]
    public class CompositeAnimationStrategy : AnimationStrategy
    {
        [SerializeField] private List<AnimationStrategy> _strategies = new List<AnimationStrategy>();

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            // Child strategies return already-playing tweens and DOTween cannot nest a started
            // tween into a Sequence - so instead of Join() we play all children in parallel and
            // return the longest one as the completion marker for the awaiting pipeline.
            // (A child with infinite loops would never complete - don't compose those.)
            Tween longest = null;

            foreach (var strategy in _strategies)
            {
                if (strategy == null) continue;

                var tween = strategy.PlayShowAnimation(target, canvasGroup, entryPos);
                if (tween != null && (longest == null || tween.Duration() > longest.Duration()))
                {
                    longest = tween;
                }
            }

            return longest ?? DOTween.Sequence();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            Tween longest = null;

            foreach (var strategy in _strategies)
            {
                if (strategy == null) continue;

                var tween = strategy.PlayHideAnimation(target, canvasGroup);
                if (tween != null && (longest == null || tween.Duration() > longest.Duration()))
                {
                    longest = tween;
                }
            }

            return longest ?? DOTween.Sequence();
        }
    }
}
