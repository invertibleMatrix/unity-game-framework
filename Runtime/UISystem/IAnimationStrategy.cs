using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// An entrance and an exit for a view's animatable content, each as one DOTween tween
	/// (a single tweener or a sequence). Return null for "nothing to animate". The system
	/// links every returned tween to the content's GameObject and awaits it through
	/// <see cref="AnimationStrategyExtensions"/>; a strategy never awaits its own tweens.
	/// </summary>
	public interface IAnimationStrategy
	{
		Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default);
		Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup);
	}

	/// <summary>
	/// A strategy that drives its own timing — an Animator, a Timeline, a hand-rolled task —
	/// instead of returning a tween. The system awaits these directly; the tween methods are
	/// still there for previews and for callers that want a tween.
	/// </summary>
	public interface IAsyncAnimationStrategy : IAnimationStrategy
	{
		UniTask PlayShowAsync(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default, CancellationToken ct = default);
		UniTask PlayHideAsync(RectTransform target, CanvasGroup canvasGroup, CancellationToken ct = default);
	}

	public static class AnimationStrategyExtensions
	{
		/// <summary>Plays the entrance to completion. Cancelling kills the tween and throws <see cref="System.OperationCanceledException"/>.</summary>
		public static UniTask PlayShowAsync(this IAnimationStrategy strategy, RectTransform target, CanvasGroup canvasGroup,
		                                    Vector2 entryPos = default, CancellationToken ct = default)
		{
			if (strategy is IAsyncAnimationStrategy own)
				return own.PlayShowAsync(target, canvasGroup, entryPos, ct);

			return Await(strategy.PlayShowAnimation(target, canvasGroup, entryPos), target, ct);
		}

		/// <summary>Plays the exit to completion. Cancelling kills the tween and throws <see cref="System.OperationCanceledException"/>.</summary>
		public static UniTask PlayHideAsync(this IAnimationStrategy strategy, RectTransform target, CanvasGroup canvasGroup,
		                                    CancellationToken ct = default)
		{
			if (strategy is IAsyncAnimationStrategy own)
				return own.PlayHideAsync(target, canvasGroup, ct);

			return Await(strategy.PlayHideAnimation(target, canvasGroup), target, ct);
		}

		/// <summary>
		/// The tween dies with its content: disabled or destroyed, it is killed instead of
		/// finishing on an object that is no longer presenting. Killed that way, the await
		/// completes normally — the caller's own state, not the tween, decides what happened.
		/// </summary>
		private static UniTask Await(Tween tween, RectTransform target, CancellationToken ct)
		{
			if (tween == null || !tween.IsActive()) return UniTask.CompletedTask;

			if (target != null) tween.SetLink(target.gameObject, LinkBehaviour.KillOnDisable);

			return tween.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
		}
	}
}
