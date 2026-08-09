using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems
{
	public abstract class AnimationStrategy : ScriptableObject, IAnimationStrategy
	{
		[SerializeField]
		protected float EntryDuration = 0.3f;

		[SerializeField] protected Ease  EntryEase    = Ease.OutCubic;
		[SerializeField] protected float ExitDuration = 0.3f;
		[SerializeField] protected Ease  ExitEase     = Ease.InCubic;

		public abstract Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default);
		public abstract Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup);

		public UniTask PlayShowAsync(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default,
		                             CancellationToken ct = default)
		{
			return PlayShowAnimation(target, canvasGroup, entryPos).ToUniTask(cancellationToken: ct);
		}

		public UniTask PlayHideAsync(RectTransform target, CanvasGroup canvasGroup,
		                             CancellationToken ct = default)
		{
			return PlayHideAnimation(target, canvasGroup).ToUniTask(cancellationToken: ct);
		}
	}
}