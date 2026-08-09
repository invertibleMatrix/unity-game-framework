using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
	[CreateAssetMenu(fileName = "SlideAnimation", menuName = "AK/UI/Animations/Slide Animation")]
	public class SlideAnimationStrategy : AnimationStrategy
	{
		[SerializeField]
		private UIUtility.SlideDirection _entryDirection = UIUtility.SlideDirection.FromBottom;

		[SerializeField] private bool _overrideExitDirection;

		[SerializeField]
		private UIUtility.SlideDirection _exitDirection = UIUtility.SlideDirection.FromBottom;

		[SerializeField] [Tooltip("Add this offset to axial direction to compensate in and out tween")]
		private Vector2 _edgesOffset = new Vector2(250, 250);

		public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
		{
			canvasGroup.alpha = 1f;
			var startPosition = UIUtility.GetOffScreenPosition(target, _entryDirection, _edgesOffset);
			target.anchoredPosition = startPosition;
			return target.DOAnchorPos(entryPos, EntryDuration).SetEase(EntryEase).Play();
		}

		public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
		{
			var exitDir = _overrideExitDirection ? _exitDirection : UIUtility.GetOppositeDirection(_entryDirection);
			var endPosition = UIUtility.GetOffScreenPosition(target, exitDir, _edgesOffset);
			return target.DOAnchorPos(endPosition, ExitDuration).SetEase(ExitEase).Play();
		}
	}
}