using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Plays a view's entrance and exit through its <see cref="IAnimationStrategy"/>. Owns the
	/// two cancellation sources a view needs — one for the object's lifetime, one for the run
	/// in flight — so the view never links tokens or catches
	/// <see cref="OperationCanceledException"/>: a run reports <c>false</c> when it was cut
	/// short and the caller settles state either way.
	/// </summary>
	internal sealed class ViewAnimator
	{
		private RectTransform           _content;
		private CanvasGroup             _group;
		private CancellationTokenSource _lifetime;
		private CancellationTokenSource _run;

		public void Bind(RectTransform content, CanvasGroup group)
		{
			_content = content;
			_group = group;
			EnsureLifetime();
		}

		/// <summary>A fresh lifetime after the previous one ended (pool reuse).</summary>
		public void EnsureLifetime()
		{
			if (_lifetime == null || _lifetime.IsCancellationRequested)
			{
				_lifetime?.Dispose();
				_lifetime = new CancellationTokenSource();
			}
		}

		/// <summary>True when the entrance finished; false when it was cancelled.</summary>
		public async UniTask<bool> PlayShowAsync(IAnimationStrategy strategy, Vector2 entryPosition, CancellationToken ct)
		{
			CancellationToken token = Begin(ct);

			try
			{
				await strategy.PlayShowAsync(_content, _group, entryPosition, token);
				return true;
			}
			catch (OperationCanceledException)
			{
				return false;
			}
		}

		/// <summary>True when the exit finished; false when it was cancelled.</summary>
		public async UniTask<bool> PlayHideAsync(IAnimationStrategy strategy, CancellationToken ct)
		{
			CancellationToken token = Begin(ct);

			try
			{
				await strategy.PlayHideAsync(_content, _group, token);
				return true;
			}
			catch (OperationCanceledException)
			{
				return false;
			}
		}

		/// <summary>Cancels the run in flight, if any. Its awaiter resumes synchronously with <c>false</c>.</summary>
		public void Cancel()
		{
			if (_run == null) return;

			var run = _run;
			_run = null;
			run.Cancel();
			run.Dispose();
		}

		/// <summary>Kills every tween still targeting the animated content or the canvas group.</summary>
		public void KillTweens()
		{
			if (_content != null) _content.DOKill();
			if (_group != null) _group.DOKill();
		}

		/// <summary>The object is going away: ends the lifetime so nothing in flight touches it again.</summary>
		public void Dispose()
		{
			Cancel();
			_lifetime?.Cancel();
			_lifetime?.Dispose();
			_lifetime = null;
		}

		private CancellationToken Begin(CancellationToken ct)
		{
			Cancel();

			_run = _lifetime != null && !_lifetime.IsCancellationRequested
				? CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token)
				: CancellationTokenSource.CreateLinkedTokenSource(ct);

			return _run.Token;
		}
	}
}
