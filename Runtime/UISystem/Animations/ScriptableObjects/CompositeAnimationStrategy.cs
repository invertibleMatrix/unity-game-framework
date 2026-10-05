using System.Collections.Generic;
using AK.Core.Extensions;
using AK.Kernel.Timing;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
    /// <summary>
    /// Plays several strategies on one view at once. Their tweens start together inside one
    /// sequence, so the view's entrance or exit lasts as long as the longest of them, and cutting
    /// it short stops them all. A child tween's own delay is kept. Compose only children whose
    /// entrance and exit end: a loop nested in a sequence plays on for good.
    /// </summary>
    [CreateAssetMenu(fileName = "CompositeAnimation", menuName = "AK/UI/Animations/Composite Animation")]
    public class CompositeAnimationStrategy : AnimationStrategy
    {
        [SerializeField] private List<AnimationStrategy> _strategies = new List<AnimationStrategy>();

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time, Vector2 entryPos = default)
        {
            Sequence sequence = DOTween.Sequence();

            foreach (var strategy in _strategies)
            {
                if (strategy != null && strategy != this)
                {
                    Nest(sequence, strategy.PlayShowAnimation(target, canvasGroup, time, entryPos));
                }
            }

            return sequence.SetTimeDomain(time).Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time)
        {
            Sequence sequence = DOTween.Sequence();

            foreach (var strategy in _strategies)
            {
                if (strategy != null && strategy != this)
                {
                    Nest(sequence, strategy.PlayHideAnimation(target, canvasGroup, time));
                }
            }

            return sequence.SetTimeDomain(time).Play();
        }

        /// <summary>
        /// Starts a child's tween with the others. A strategy's tween has not run a frame when it
        /// is returned, so it can still be nested; one that is already gone is left out.
        /// </summary>
        private static void Nest(Sequence sequence, Tween tween)
        {
            if (tween != null && tween.IsActive())
            {
                sequence.Insert(0f, tween);
            }
        }
    }
}
