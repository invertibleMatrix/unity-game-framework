using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "RotateInAnimation", menuName = "AK/UI/Animations/Rotate In Animation")]
    public class RotateInAnimationStrategy : AnimationStrategy
    {
        [SerializeField] private Vector3 _startRotation = new Vector3(0, 90, 0);
        [SerializeField] private Vector3 _startScale = new Vector3(0.7f, 0.7f, 0.7f);

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            target.localEulerAngles = _startRotation;
            target.localScale = _startScale;
            canvasGroup.alpha = 0;

            sequence.Join(target.DOLocalRotate(Vector3.zero, EntryDuration).SetEase(EntryEase));
            sequence.Join(target.DOScale(Vector3.one, EntryDuration).SetEase(EntryEase));
            sequence.Join(canvasGroup.DOFade(1, EntryDuration * 0.8f).SetEase(Ease.InQuad)); // Fade in slightly faster

            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            sequence.Join(target.DOLocalRotate(_startRotation, ExitDuration).SetEase(ExitEase));
            sequence.Join(target.DOScale(_startScale, ExitDuration).SetEase(ExitEase));
            sequence.Join(canvasGroup.DOFade(0, ExitDuration * 0.8f).SetEase(Ease.OutQuad));

            return sequence.Play();
        }
    }
}