using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
	[CreateAssetMenu(fileName = "ScaleAnimation", menuName = "Gameplay/UI/Animations/Scale Animation")]
	public class ScaleAnimationStrategy : AnimationStrategy
	{
		[Title("Scale Settings")] [SerializeField]
		private Vector3 _startScale = new Vector3(0.8f, 0.8f, 0.8f);

		[SerializeField] private bool _addPunch;

		[ShowIf("_addPunch")] [SerializeField]
		private Vector3 _punchAmount = new Vector3(0.1f, 0.1f, 0.1f);

		[ShowIf("_addPunch")] [SerializeField]
		private int _punchVibrato = 5;

		[ShowIf("_addPunch")] [SerializeField]
		private float _punchElasticity = 0.5f;

		[Title("Optional Slide")] [SerializeField]
		private bool _slideWhileScaling;

		[ShowIf("_slideWhileScaling")] [SerializeField]
		private UIUtility.SlideDirection _entryDirection = UIUtility.SlideDirection.FromBottom;

		[ShowIf("_slideWhileScaling")] [SerializeField]
		private bool _overrideExitDirection;

		[ShowIf("_slideWhileScaling"), ShowIf("_overrideExitDirection")] [SerializeField]
		private UIUtility.SlideDirection _exitDirection = UIUtility.SlideDirection.FromBottom;

		[ShowIf("_slideWhileFading")] [SerializeField] [Tooltip("Add this offset to axial direction to compensate in and out tween")]
		private Vector2 _edgesOffset = new Vector2(250, 250);

		public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
		{
			canvasGroup.alpha = 1f;
			var sequence = DOTween.Sequence();
			target.localScale = _startScale;
			var scaleTween = target.DOScale(Vector3.one, EntryDuration).SetEase(EntryEase);

			if (_addPunch)
			{
				scaleTween.OnComplete(() => target.DOPunchScale(_punchAmount, EntryDuration / 2, _punchVibrato, _punchElasticity));
			}

			sequence.Join(scaleTween);

			if (_slideWhileScaling)
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
			sequence.Join(target.DOScale(_startScale, ExitDuration).SetEase(ExitEase));

			if (_slideWhileScaling)
			{
				var exitDir = _overrideExitDirection ? _exitDirection : UIUtility.GetOppositeDirection(_entryDirection);
				var endPosition = UIUtility.GetOffScreenPosition(target, exitDir, _edgesOffset);
				sequence.Join(target.DOAnchorPos(endPosition, ExitDuration).SetEase(ExitEase));
			}

			return sequence.Play();
		}
	}
}