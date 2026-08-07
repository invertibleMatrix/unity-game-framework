using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Component-based animation strategy. Lives on a GameObject next to its view, so unlike
	/// the SO-based AnimationStrategy it can hold per-instance scene references.
	/// Rule of thumb: SO strategies are shared and tuning-only; component strategies are
	/// per-instance and may serialize scene geometry.
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

		public virtual UniTask PlayShowAsync(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default,
		                                     CancellationToken ct = default)
		{
			return PlayShowAnimation(target, canvasGroup, entryPos).ToUniTask(cancellationToken: ct);
		}

		public virtual UniTask PlayHideAsync(RectTransform target, CanvasGroup canvasGroup,
		                                     CancellationToken ct = default)
		{
			return PlayHideAnimation(target, canvasGroup).ToUniTask(cancellationToken: ct);
		}
	}
}
