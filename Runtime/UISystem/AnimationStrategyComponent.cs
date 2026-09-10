using DG.Tweening;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Component-based animation strategy. Lives on a GameObject next to its view, so unlike
	/// the asset-based <see cref="AnimationStrategy"/> it can hold per-instance scene
	/// references. Rule of thumb: asset strategies are shared and tuning-only; component
	/// strategies are per-instance and may serialize scene geometry. Build the entrance and
	/// exit as tweens; the system links and awaits them. A component that drives its own
	/// timing implements <see cref="IAsyncAnimationStrategy"/> on top.
	/// </summary>
	public abstract class AnimationStrategyComponent : MonoBehaviour, IAnimationStrategy
	{
		[SerializeField, Tooltip("Show animation duration in seconds.")]
		protected float EntryDuration = 0.3f;

		[SerializeField] protected Ease EntryEase = Ease.OutCubic;

		[SerializeField, Tooltip("Hide animation duration in seconds.")]
		protected float ExitDuration = 0.3f;

		[SerializeField] protected Ease ExitEase = Ease.InCubic;

		public abstract Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default);
		public abstract Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup);
	}
}
