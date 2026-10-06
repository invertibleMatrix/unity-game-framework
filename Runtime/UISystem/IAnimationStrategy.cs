using System.Threading;
using AK.Core.Extensions;
using AK.Kernel.Timing;
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
	///
	/// Every tween runs on the <see cref="TimeDomain"/> it is given, the UI system's
	/// (<see cref="IUISystem.TimeDomain"/>): set it with <see cref="TweenExt.SetTimeDomain{T}"/>
	/// on the returned tween and on any tween started apart from it, such as an idle loop begun
	/// from a callback. A tween nested in the returned sequence follows the sequence.
	///
	/// The view kills every tween on its content and canvas group before each entrance and exit,
	/// so a tween started apart from the returned one must target the content to stop there: a
	/// tweener made from it (<c>target.DOScale</c>) does by itself; give a sequence
	/// <c>SetTarget(target)</c>. Killing the content's tweens from inside a run would also stop
	/// the ones a composite's other children started.
	/// </summary>
	public interface IAnimationStrategy
	{
		Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time, Vector2 entryPos = default);
		Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time);
	}

	/// <summary>
	/// A strategy that drives its own timing — an Animator, a Timeline, a hand-rolled task —
	/// instead of returning a tween. The system awaits these directly; the tween methods are
	/// still there for previews and for callers that want a tween. Its timing follows the
	/// <see cref="TimeDomain"/> it is given, as a tween's would.
	/// </summary>
	public interface IAsyncAnimationStrategy : IAnimationStrategy
	{
		UniTask PlayShowAsync(RectTransform target, CanvasGroup canvasGroup, TimeDomain time, Vector2 entryPos = default,
		                      CancellationToken ct = default);

		UniTask PlayHideAsync(RectTransform target, CanvasGroup canvasGroup, TimeDomain time, CancellationToken ct = default);
	}

	public static class AnimationStrategyExtensions
	{
		/// <summary>Plays the entrance to completion on <paramref name="time"/>. Cancelling kills the tween and throws <see cref="System.OperationCanceledException"/>.</summary>
		public static UniTask PlayShowAsync(this IAnimationStrategy strategy, RectTransform target, CanvasGroup canvasGroup,
		                                    TimeDomain time, Vector2 entryPos = default, CancellationToken ct = default)
		{
			if (strategy is IAsyncAnimationStrategy own)
				return own.PlayShowAsync(target, canvasGroup, time, entryPos, ct);

			return Await(strategy.PlayShowAnimation(target, canvasGroup, time, entryPos), target, time, ct);
		}

		/// <summary>Plays the exit to completion on <paramref name="time"/>. Cancelling kills the tween and throws <see cref="System.OperationCanceledException"/>.</summary>
		public static UniTask PlayHideAsync(this IAnimationStrategy strategy, RectTransform target, CanvasGroup canvasGroup,
		                                    TimeDomain time, CancellationToken ct = default)
		{
			if (strategy is IAsyncAnimationStrategy own)
				return own.PlayHideAsync(target, canvasGroup, time, ct);

			return Await(strategy.PlayHideAnimation(target, canvasGroup, time), target, time, ct);
		}

		/// <summary>
		/// The tween runs on <paramref name="time"/> even if its strategy left it on DOTween's
		/// default, and dies with its content: disabled or destroyed, it is killed instead of
		/// finishing on an object that is no longer presenting. Killed that way, the await
		/// completes normally — the caller's own state, not the tween, decides what happened.
		/// </summary>
		private static UniTask Await(Tween tween, RectTransform target, TimeDomain time, CancellationToken ct)
		{
			if (tween == null || !tween.IsActive()) return UniTask.CompletedTask;

			tween.SetTimeDomain(time);
			if (target != null) tween.SetLink(target.gameObject, LinkBehaviour.KillOnDisable);

			return tween.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
		}
	}
}
