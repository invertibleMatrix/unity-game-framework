using AK.Kernel.Timing;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Asset-based animation strategy: shared between every view that references it, so it
	/// holds tuning only, never scene references. Build the entrance and exit as tweens on the
	/// given <see cref="TimeDomain"/>; the system links and awaits them.
	/// </summary>
	public abstract class AnimationStrategy : ScriptableObject, IAnimationStrategy
	{
		[SerializeField]
		protected float EntryDuration = 0.3f;

		[SerializeField] protected Ease  EntryEase    = Ease.OutCubic;
		[SerializeField] protected float ExitDuration = 0.3f;
		[SerializeField] protected Ease  ExitEase     = Ease.InCubic;

		public abstract Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time, Vector2 entryPos = default);
		public abstract Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time);
	}
}
