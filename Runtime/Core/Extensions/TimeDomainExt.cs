using System;
using System.Threading;
using AK.Kernel.Timing;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Core.Extensions
{
	/// <summary>
	/// <see cref="TimeDomain"/> on Unity's clocks: the time a frame adds to a domain, the domain's
	/// clock, and waits on it.
	/// </summary>
	public static class TimeDomainExt
	{
		// TimeSpan.FromSeconds throws past TimeSpan.MaxValue; a scaled wait that long waits for ever.
		private static readonly double MaxSpanSeconds = TimeSpan.MaxValue.TotalSeconds - 1d;

		/// <summary>
		/// The time this frame adds to <paramref name="domain"/>, in seconds:
		/// <see cref="Time.deltaTime"/> for scaled time, which Unity caps at
		/// <see cref="Time.maximumDeltaTime"/>, and <see cref="Time.unscaledDeltaTime"/> for
		/// unscaled time, capped at <see cref="ForegroundTime.MaxFrameSeconds"/>.
		/// </summary>
		public static float DeltaTime(this TimeDomain domain)
		{
			return domain == TimeDomain.Unscaled
				? (float)ForegroundTime.FrameStep(Time.unscaledDeltaTime)
				: Time.deltaTime;
		}

		/// <summary>
		/// The domain's clock at the start of this frame, in seconds: <see cref="Time.timeAsDouble"/>
		/// for scaled time and <see cref="Time.unscaledTimeAsDouble"/> for unscaled time. For telling
		/// how long ago something happened, where a stall may count in full.
		/// </summary>
		public static double Now(this TimeDomain domain)
		{
			return domain == TimeDomain.Unscaled ? Time.unscaledTimeAsDouble : Time.timeAsDouble;
		}

		/// <summary>
		/// The <see cref="AnimatorUpdateMode"/> that runs an Animator on <paramref name="domain"/>:
		/// <see cref="AnimatorUpdateMode.Normal"/> for scaled time and
		/// <see cref="AnimatorUpdateMode.UnscaledTime"/> for unscaled time.
		/// </summary>
		public static AnimatorUpdateMode ToAnimatorUpdateMode(this TimeDomain domain)
		{
			return domain == TimeDomain.Unscaled ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
		}

		/// <summary>
		/// Completes after <paramref name="seconds"/> of the domain's time, checked at each Update;
		/// at once for zero, less or NaN, and never for positive infinity.
		/// <list type="bullet">
		/// <item>Scaled time adds up <see cref="Time.deltaTime"/> from the next frame on, as UniTask's
		/// <see cref="DelayType.DeltaTime"/> delay does. Outside Play mode, where the game clock
		/// doesn't run, it counts real time.</item>
		/// <item>Unscaled time counts real time from now on, each frame for at most
		/// <see cref="ForegroundTime.MaxFrameSeconds"/>, so time spent in the background doesn't count.</item>
		/// </list>
		/// Cancelling ends it as cancelled: at the next Update, or at once with
		/// <paramref name="cancelImmediately"/>.
		/// </summary>
		public static UniTask Delay(this TimeDomain domain, double seconds, CancellationToken cancellationToken = default,
		                            bool cancelImmediately = false)
		{
			if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled(cancellationToken);
			if (!(seconds > 0d)) return UniTask.CompletedTask;

			if (domain == TimeDomain.Unscaled)
			{
				IUniTaskSource source = ForegroundDelayPromise.Create(seconds, cancellationToken, cancelImmediately, out short token);
				return new UniTask(source, token);
			}

			TimeSpan span = seconds < MaxSpanSeconds ? TimeSpan.FromSeconds(seconds) : TimeSpan.MaxValue;
			return UniTask.Delay(span, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken, cancelImmediately);
		}

		/// <summary>
		/// An unscaled wait, pooled the way UniTask pools its own delays. Each Update it reads
		/// <see cref="Time.realtimeSinceStartupAsDouble"/> and counts the step since the last
		/// reading through <see cref="ForegroundTime.FrameStep"/>. Reading the real-time clock
		/// rather than adding up frame deltas keeps it exact from the moment it starts, and right
		/// outside Play mode, where Unity's frame deltas don't follow real time.
		/// </summary>
		private sealed class ForegroundDelayPromise : IUniTaskSource, IPlayerLoopItem, ITaskPoolNode<ForegroundDelayPromise>
		{
			private static TaskPool<ForegroundDelayPromise> s_pool;

			private ForegroundDelayPromise                 _nextNode;
			private double                                 _remaining;
			private double                                 _lastReading;
			private CancellationToken                      _cancellationToken;
			private CancellationTokenRegistration          _cancellationRegistration;
			private bool                                   _cancelImmediately;
			private UniTaskCompletionSourceCore<AsyncUnit> _core;

			static ForegroundDelayPromise()
			{
				TaskPool.RegisterSizeGetter(typeof(ForegroundDelayPromise), () => s_pool.Size);
			}

			private ForegroundDelayPromise()
			{
			}

			public ref ForegroundDelayPromise NextNode => ref _nextNode;

			public static IUniTaskSource Create(double seconds, CancellationToken cancellationToken, bool cancelImmediately, out short token)
			{
				if (!s_pool.TryPop(out ForegroundDelayPromise promise)) promise = new ForegroundDelayPromise();

				promise._remaining         = seconds;
				promise._lastReading       = Time.realtimeSinceStartupAsDouble;
				promise._cancellationToken = cancellationToken;
				promise._cancelImmediately = cancelImmediately;

				if (cancelImmediately && cancellationToken.CanBeCanceled)
				{
					promise._cancellationRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(static state =>
					{
						var self = (ForegroundDelayPromise)state;
						self._core.TrySetCanceled(self._cancellationToken);
					}, promise);
				}

				TaskTracker.TrackActiveTask(promise, 3);
				PlayerLoopHelper.AddAction(PlayerLoopTiming.Update, promise);

				token = promise._core.Version;
				return promise;
			}

			public void GetResult(short token)
			{
				try
				{
					_core.GetResult(token);
				}
				finally
				{
					// Cancelled at once, it stays in the player loop until its next MoveNext, so it
					// can't be handed out again yet: it is left to the garbage collector.
					if (_cancelImmediately && _cancellationToken.IsCancellationRequested)
					{
						TaskTracker.RemoveTracking(this);
					}
					else
					{
						Return();
					}
				}
			}

			public UniTaskStatus GetStatus(short token) => _core.GetStatus(token);

			public UniTaskStatus UnsafeGetStatus() => _core.UnsafeGetStatus();

			public void OnCompleted(Action<object> continuation, object state, short token) =>
				_core.OnCompleted(continuation, state, token);

			public bool MoveNext()
			{
				if (_cancellationToken.IsCancellationRequested)
				{
					_core.TrySetCanceled(_cancellationToken);
					return false;
				}

				double reading = Time.realtimeSinceStartupAsDouble;
				_remaining  -= ForegroundTime.FrameStep(reading - _lastReading);
				_lastReading = reading;

				if (_remaining > 0d) return true;

				_core.TrySetResult(AsyncUnit.Default);
				return false;
			}

			private void Return()
			{
				TaskTracker.RemoveTracking(this);
				_core.Reset();
				_cancellationToken = default;
				_cancellationRegistration.Dispose();
				_cancellationRegistration = default;
				_cancelImmediately = false;
				s_pool.TryPush(this);
			}
		}
	}
}
