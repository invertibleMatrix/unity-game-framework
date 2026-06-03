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
            var sequence = DOTween.Sequence();
            foreach (var strategy in _strategies)
            {
                sequence.Join(strategy.PlayShowAnimation(target, canvasGroup));
            }
            return sequence;
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            foreach (var strategy in _strategies)
            {
                sequence.Join(strategy.PlayHideAnimation(target, canvasGroup));
            }
            return sequence;
        }
    }
}