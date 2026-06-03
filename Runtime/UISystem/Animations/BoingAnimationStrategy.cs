using DG.Tweening;
using UnityEngine;

namespace AK.UISystem.Animations
{
	[CreateAssetMenu(fileName = "BoingAnimation", menuName = "AK/UI/Animations/Boing Animation")]
	public class BoingAnimationStrategy : AnimationStrategy
	{
		[SerializeField] [Tooltip("Add this offset to axial direction to compensate in and out tween")]
		private Vector2 _edgesOffset = new Vector2(250, 250);
		
		public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
		{
			var sequence = DOTween.Sequence();

			// Start off-screen from the bottom
			var startPosition = UIUtility.GetOffScreenPosition(target, UIUtility.SlideDirection.FromBottom, _edgesOffset);
			target.anchoredPosition = startPosition;
			canvasGroup.alpha = 1; // Boing looks better without a fade

			// Use OutElastic for the cheesy boing effect
			sequence.Append(target.DOAnchorPos(Vector2.zero, EntryDuration).SetEase(Ease.OutElastic));

			return sequence.Play();
		}

		public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
		{
			var sequence = DOTween.Sequence();

			// Exit to the bottom
			var endPosition = UIUtility.GetOffScreenPosition(target, UIUtility.SlideDirection.FromBottom, _edgesOffset);

			// Use a simple ease for the exit
			sequence.Append(target.DOAnchorPos(endPosition, ExitDuration).SetEase(Ease.InBack));

			return sequence.Play();
		}
	}
}