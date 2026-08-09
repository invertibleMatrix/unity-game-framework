using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
	[CreateAssetMenu(fileName = "FadeAnimation", menuName = "AK/UI/Animations/Fade Animation")]
	public class FadeAnimationStrategy : AnimationStrategy
	{
		[SerializeField]
		private bool _slideWhileFading;

		[SerializeField]
		private UIUtility.SlideDirection _entryDirection = UIUtility.SlideDirection.FromBottom;

		[SerializeField]
		private bool _overrideExitDirection;

		[SerializeField]
		private UIUtility.SlideDirection _exitDirection = UIUtility.SlideDirection.FromBottom;

		[SerializeField] [Tooltip("Add this offset to axial direction to compensate in and out tween")]
		private Vector2 _edgesOffset = new Vector2(250, 250);

		public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
		{
			// NOTE: do NOT set alpha here - the view pipeline already set it to 0 in
			// PrepareForShowAnimation. Setting alpha=1 before DOFade(1, ...) makes the
			// fade a no-op (1 -> 1) and the view pops in instantly.
			var sequence = DOTween.Sequence();
			sequence.Join(canvasGroup.DOFade(1, EntryDuration).SetEase(EntryEase));

			if (_slideWhileFading)
			{
				var startPosition = UIUtility.GetOffScreenPosition(target, _entryDirection, _edgesOffset);
				target.anchoredPosition = startPosition;
				sequence.Join(target.DOAnchorPos(Vector2.zero, EntryDuration).SetEase(EntryEase));
			}

			return sequence.Play();
		}

		public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
		{
			var sequence = DOTween.Sequence();
			sequence.Join(canvasGroup.DOFade(0, ExitDuration).SetEase(ExitEase));

			if (_slideWhileFading)
			{
				var exitDir = _overrideExitDirection ? _exitDirection : UIUtility.GetOppositeDirection(_entryDirection);
				var endPosition = UIUtility.GetOffScreenPosition(target, exitDir, _edgesOffset);
				sequence.Join(target.DOAnchorPos(endPosition, ExitDuration).SetEase(ExitEase));
			}

			return sequence.Play();
		}
	}
}