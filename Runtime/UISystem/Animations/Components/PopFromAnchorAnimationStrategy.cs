using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
	/// <summary>
	/// Pops the target out of a source rect (e.g. a button) and flies it into its resting
	/// position with an elastic settle. Without a source it degrades to an in-place elastic pop.
	/// </summary>
	public class PopFromAnchorAnimationStrategy : AnimationStrategyComponent
	{
		[SerializeField, Tooltip("Rect the target pops out from (e.g. a Next button). Optional — without it the target pops in place.")]
		private RectTransform _source;

		[SerializeField, Tooltip("Scale at the start of the pop.")]
		private Vector3 _startScale = new Vector3(0.3f, 0.3f, 0.3f);

		[SerializeField, Tooltip("Ease for the flight into the resting position. Keep non-overshooting (OutCubic/OutQuad) — elastic eases swing the target past the destination.")]
		private Ease _flightEase = Ease.OutCubic;

		[SerializeField, Tooltip("Ease for scaling up to full size during the flight.")]
		private Ease _scaleEase = Ease.OutCubic;

		[SerializeField, Tooltip("Scale punch played in place after landing.")]
		private Vector3 _landingPunch = new Vector3(0.25f, 0.25f, 0.25f);

		[SerializeField, Tooltip("Duration of the landing punch.")]
		private float _landingPunchDuration = 0.35f;

		[SerializeField, Tooltip("Elasticity of the landing punch (higher = bouncier).")]
		private float _landingElasticity = 1f;

		public void SetSource(RectTransform source)
		{
			_source = source;
		}

		public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
		{
			var sequence = DOTween.Sequence();

			target.anchoredPosition = entryPos + GetSourceOffset(target);
			target.localScale = _startScale;
			canvasGroup.alpha = 0f;

			sequence.Append(canvasGroup.DOFade(1f, EntryDuration * 0.25f));
			sequence.Join(target.DOAnchorPos(entryPos, EntryDuration).SetEase(_flightEase));
			sequence.Join(target.DOScale(Vector3.one, EntryDuration).SetEase(_scaleEase));
			sequence.Append(target.DOPunchScale(_landingPunch, _landingPunchDuration, elasticity: _landingElasticity));

			return sequence.Play();
		}

		public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
		{
			var sequence = DOTween.Sequence();

			sequence.Append(target.DOAnchorPos(target.anchoredPosition + GetSourceOffset(target), ExitDuration).SetEase(ExitEase));
			sequence.Join(target.DOScale(_startScale, ExitDuration).SetEase(ExitEase));
			sequence.Join(canvasGroup.DOFade(0f, ExitDuration * 0.6f).SetEase(Ease.InQuad));

			return sequence.Play();
		}

		private Vector2 GetSourceOffset(RectTransform target)
		{
			if (_source == null) return Vector2.zero;

			var parent = target.parent;
			if (parent == null) return Vector2.zero;

			Vector3 worldOffset = _source.position - target.position;
			return parent.InverseTransformVector(worldOffset);
		}
	}
}
