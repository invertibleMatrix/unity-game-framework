using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace AK.Utilities
{
	/// <summary>
	/// A robust, self-contained timer utility using UniTask.
	/// Each instance is owned by its creator - no centralized manager required.
	/// Supports countdown, count-up, pausing, and rich callbacks.
	/// </summary>
	public class Timer
	{
		#region Events

		/// <summary>
		/// Called every tick with remaining/elapsed time and normalized progress (0-1).
		/// </summary>
		public event Action<TimeSpan, float> OnTick;

		/// <summary>
		/// Called when timer completes naturally.
		/// </summary>
		public event Action OnComplete;

		/// <summary>
		/// Called when timer is paused.
		/// </summary>
		public event Action OnPause;

		/// <summary>
		/// Called when timer is resumed from pause.
		/// </summary>
		public event Action OnResume;

		/// <summary>
		/// Called when timer is cancelled/disposed.
		/// </summary>
		public event Action OnCancel;

		#endregion

		#region Properties

		/// <summary>
		/// Current state of the timer.
		/// </summary>
		public TimerState State { get; private set; } = TimerState.Idle;

		/// <summary>
		/// Total duration for countdown mode.
		/// </summary>
		public TimeSpan Duration { get; private set; }

		/// <summary>
		/// Remaining time for countdown timers.
		/// </summary>
		public TimeSpan RemainingTime { get; private set; }

		/// <summary>
		/// Elapsed time for count-up timers.
		/// </summary>
		public TimeSpan ElapsedTime { get; private set; }

		/// <summary>
		/// Normalized progress from 0 (start) to 1 (complete) for countdown timers.
		/// </summary>
		public float Progress => Duration > TimeSpan.Zero
			? 1f - (float)(RemainingTime.TotalSeconds / Duration.TotalSeconds)
			: 0f;

		/// <summary>
		/// Whether timer uses real-time (ignores Time.timeScale).
		/// </summary>
		public bool UseRealTime { get; private set; }

		/// <summary>
		/// Tick interval for OnTick callbacks. Default 100ms.
		/// </summary>
		public TimeSpan TickInterval { get; private set; } = TimeSpan.FromMilliseconds(100);

		#endregion

		#region Private Fields

		private CancellationTokenSource _cts;
		private CancellationToken       _cancellationToken;
		private UniTask                 _timerTask;
		private bool                    _isDisposed;
		private TimeSpan                _pauseRemainingTime;

		#endregion

		#region Public Methods

		public Timer(CancellationToken cancellationToken = default)
		{
			_cancellationToken = cancellationToken;
		}

		/// <summary>
		/// Starts a countdown timer from the specified duration.
		/// </summary>
		public void StartCountdown(
			TimeSpan duration,
			Action<TimeSpan, float> onTick = null,
			Action onComplete = null,
			TimeSpan? tickInterval = null,
			bool useRealTime = false)
		{
			if (_isDisposed)
				throw new ObjectDisposedException(nameof(Timer));

			// if (duration <= TimeSpan.Zero)
			// {
			// 	State = TimerState.Completed;
			// 	OnComplete?.Invoke();
			// 	Dispose();
			// 	return;
			// }

			Stop();

			Duration = duration;
			RemainingTime = duration;
			UseRealTime = useRealTime;
			TickInterval = tickInterval ?? TimeSpan.FromMilliseconds(100);

			if (onTick != null)
				OnTick += onTick;
			if (onComplete != null)
				OnComplete += onComplete;

			if (_cancellationToken != CancellationToken.None)
			{
				_cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
			}
			else
			{
				_cts = new CancellationTokenSource();
			}
			_timerTask = RunCountdownAsync(_cts.Token);
		}

		/// <summary>
		/// Starts a count-up timer (stopwatch mode).
		/// </summary>
		public void StartCountUp(
			TimeSpan? duration = null,
			Action<TimeSpan, float> onTick = null,
			Action onComplete = null,
			TimeSpan? tickInterval = null,
			bool useRealTime = false)
		{
			if (_isDisposed)
				throw new ObjectDisposedException(nameof(Timer));

			Stop();

			Duration = duration ?? TimeSpan.Zero;
			ElapsedTime = TimeSpan.Zero;
			UseRealTime = useRealTime;
			TickInterval = tickInterval ?? TimeSpan.FromMilliseconds(100);

			if (onTick != null)
				OnTick += onTick;
			if (onComplete != null)
				OnComplete += onComplete;

			if (_cancellationToken != CancellationToken.None)
			{
				_cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
			}
			else
			{
				_cts = new CancellationTokenSource();
			}
			
			_timerTask = RunCountUpAsync(_cts.Token);
		}

		/// <summary>
		/// Starts a repeating interval timer.
		/// </summary>
		public void StartInterval(
			TimeSpan interval,
			int repeatCount = -1,
			Action<int> onInterval = null,
			Action onComplete = null,
			bool useRealTime = false)
		{
			if (_isDisposed)
				throw new ObjectDisposedException(nameof(Timer));

			if (interval <= TimeSpan.Zero)
				throw new ArgumentException("Interval must be greater than zero", nameof(interval));

			Stop();

			Duration = interval;
			UseRealTime = useRealTime;
			TickInterval = interval;

			if (_cancellationToken != CancellationToken.None)
			{
				_cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
			}
			else
			{
				_cts = new CancellationTokenSource();
			}
			
			_timerTask = RunIntervalAsync(interval, repeatCount, onInterval, onComplete, _cts.Token);
		}

		/// <summary>
		/// Pauses the timer. Can be resumed with Resume().
		/// </summary>
		public void Pause()
		{
			if (_isDisposed || State != TimerState.Running)
				return;

			_pauseRemainingTime = RemainingTime;
			_cts?.Cancel();
			State = TimerState.Paused;
			OnPause?.Invoke();
		}

		/// <summary>
		/// Resumes a paused timer.
		/// </summary>
		public void Resume()
		{
			if (_isDisposed || State != TimerState.Paused)
				return;

			RemainingTime = _pauseRemainingTime;
			_cts = new CancellationTokenSource();
			_timerTask = RunCountdownAsync(_cts.Token);
			OnResume?.Invoke();
		}

		/// <summary>
		/// Stops the timer without completing it. Clears all state.
		/// </summary>
		public void Stop()
		{
			if (_isDisposed)
				return;

			_cts?.Cancel();
			_cts?.Dispose();
			_cts = null;

			State = TimerState.Idle;
			RemainingTime = TimeSpan.Zero;
			ElapsedTime = TimeSpan.Zero;
		}

		/// <summary>
		/// Stops and cleans up the timer. Instance cannot be reused after disposal.
		/// </summary>
		public void Dispose()
		{
			if (_isDisposed)
				return;

			Stop();
			OnCancel?.Invoke();

			// Clear all event subscriptions
			OnTick = null;
			OnComplete = null;
			OnPause = null;
			OnResume = null;
			OnCancel = null;

			_isDisposed = true;
		}

		/// <summary>
		/// Returns formatted string MM:SS from remaining time.
		/// </summary>
		public string GetFormattedRemaining(string format = "hh\\:mm\\:ss") => RemainingTime.ToString(format);

		/// <summary>
		/// Returns formatted string MM:SS from elapsed time.
		/// </summary>
		public string GetFormattedElapsed(string format = "hh\\:mm\\:ss") => ElapsedTime.ToString(format);

		#endregion

		#region Private Methods

		private async UniTask RunCountdownAsync(CancellationToken token)
		{
			State = TimerState.Running;
			var startTime = UseRealTime ? DateTime.UtcNow : DateTime.MinValue;

			try
			{
				while (RemainingTime > TimeSpan.Zero)
				{
					if (token.IsCancellationRequested)
						return;

					// Calculate remaining
					if (UseRealTime)
					{
						var elapsed = DateTime.UtcNow - startTime;
						RemainingTime = Duration - elapsed;
					}
					else
					{
						// Use frame-based timing for game time
						await UniTask.Delay(TickInterval, cancellationToken: token);
						if (token.IsCancellationRequested) return;

						RemainingTime -= TimeSpan.FromSeconds((float)TickInterval.TotalSeconds * UnityEngine.Time.timeScale);
					}

					if (RemainingTime < TimeSpan.Zero)
						RemainingTime = TimeSpan.Zero;

					// Invoke tick
					OnTick?.Invoke(RemainingTime, Progress);

					// Real-time delay
					if (UseRealTime)
					{
						await UniTask.Delay(TickInterval, cancellationToken: token);
					}
				}

				if (!token.IsCancellationRequested && State == TimerState.Running)
				{
					State = TimerState.Completed;
					OnComplete?.Invoke();
				}
			}
			catch (OperationCanceledException)
			{
				// Expected on cancellation
			}
		}

		private async UniTask RunCountUpAsync(CancellationToken token)
		{
			State = TimerState.Running;
			var startTime = UseRealTime ? DateTime.UtcNow : DateTime.MinValue;

			try
			{
				while (Duration == TimeSpan.Zero || ElapsedTime < Duration)
				{
					if (token.IsCancellationRequested)
						return;

					if (UseRealTime)
					{
						ElapsedTime = DateTime.UtcNow - startTime;
					}
					else
					{
						await UniTask.Delay(TickInterval, cancellationToken: token);
						if (token.IsCancellationRequested) return;

						ElapsedTime += TimeSpan.FromSeconds((float)TickInterval.TotalSeconds * UnityEngine.Time.timeScale);
					}

					var progress = Duration > TimeSpan.Zero
						? (float)(ElapsedTime.TotalSeconds / Duration.TotalSeconds)
						: 0f;

					OnTick?.Invoke(ElapsedTime, progress);

					if (UseRealTime)
					{
						await UniTask.Delay(TickInterval, cancellationToken: token);
					}
				}

				if (!token.IsCancellationRequested && Duration > TimeSpan.Zero && State == TimerState.Running)
				{
					State = TimerState.Completed;
					OnComplete?.Invoke();
				}
			}
			catch (OperationCanceledException)
			{
				// Expected
			}
		}

		private async UniTask RunIntervalAsync(
			TimeSpan interval,
			int repeatCount,
			Action<int> onInterval,
			Action onComplete,
			CancellationToken token)
		{
			State = TimerState.Running;
			int currentCount = 0;

			try
			{
				while (repeatCount < 0 || currentCount < repeatCount)
				{
					if (token.IsCancellationRequested)
						return;

					await UniTask.Delay(interval, cancellationToken: token);
					if (token.IsCancellationRequested) return;

					currentCount++;
					onInterval?.Invoke(currentCount);
				}

				if (!token.IsCancellationRequested)
				{
					State = TimerState.Completed;
					onComplete?.Invoke();
				}
			}
			catch (OperationCanceledException)
			{
				// Expected
			}
		}

		#endregion
	}

	public enum TimerState
	{
		Idle,
		Running,
		Paused,
		Completed
	}
}