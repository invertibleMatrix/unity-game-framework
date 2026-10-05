using System;
using System.Threading;
using AK.Kernel.Timing;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Utilities
{
	/// <summary>The clocks a <see cref="Timer"/> reads.</summary>
	public interface ITimerClock
	{
		/// <summary>Game time in seconds: it follows the time scale, and stops while the time scale is zero.</summary>
		double GameTime { get; }

		/// <summary>
		/// Real time in seconds from a fixed start: it ignores the time scale, and may run on while
		/// the app is in the background. An unscaled run reads it every frame, and counts the time
		/// between two readings for at most <see cref="ForegroundTime.MaxFrameSeconds"/>.
		/// </summary>
		double RealTime { get; }

		/// <summary>The wall clock in UTC: it ignores the time scale and runs on while the app is in the background.</summary>
		DateTime UtcNow { get; }
	}

	/// <summary>
	/// Unity's clocks: <see cref="Time.timeAsDouble"/>, <see cref="Time.realtimeSinceStartupAsDouble"/>
	/// and <see cref="DateTime.UtcNow"/>.
	/// </summary>
	public sealed class UnityTimerClock : ITimerClock
	{
		public static readonly UnityTimerClock Instance = new();

		private UnityTimerClock() { }

		public double GameTime => Time.timeAsDouble;

		public double RealTime => Time.realtimeSinceStartupAsDouble;

		public DateTime UtcNow => DateTime.UtcNow;
	}

	/// <summary>
	/// Counts down, counts up or passes intervals, and raises its events on the main thread from
	/// Unity's player loop. A timer belongs to whoever creates it; no manager holds it.
	///
	/// <para>A run counts a <see cref="AK.Kernel.Timing.TimeBase"/>. <b>Scaled</b> game time
	/// follows the time scale and stops at zero. <b>Unscaled</b> time ignores the time scale and,
	/// like every unscaled wait, counts a stall such as time in the background as one short frame.
	/// The <b>wall</b> clock ignores the time scale and counts the time the app spends in the
	/// background, so a countdown to a deadline completes at the deadline.</para>
	///
	/// <para><b>Ticks</b> come when a run starts, whenever its time reaches a multiple of the tick
	/// interval, and at its end, so a countdown shown rounded up changes on its ticks and its
	/// last tick shows zero just before it completes. A frame that covers several tick intervals
	/// raises one tick. Intervals each raise their own event, in order.
	/// <see cref="TimerSchedule"/> has the rules.</para>
	///
	/// <para>Callbacks passed to a start belong to that run only. Each event goes to the timer's
	/// own handlers first, then to the run's callback. A handler that throws is logged and the
	/// timer carries on. Once the owner's token is cancelled, the timer stops without raising
	/// anything and ignores later starts.</para>
	///
	/// Use it on the main thread. Ticks allocate nothing.
	/// </summary>
	public sealed class Timer : IDisposable
	{
		public static readonly TimeSpan DefaultTickInterval = TimeSpan.FromMilliseconds(100);

		private readonly TimerSchedule _schedule = new();
		private readonly ITimerClock   _clock;

		private CancellationTokenRegistration _ownerRegistration;
		private volatile bool _ownerGone;
		private bool _disposed;

		private Runner _runner;
		private bool   _registered;

		// Foreground time for unscaled runs, moved on by each reading of the real-time clock.
		// NaN until the first reading, whose step FrameStep counts as nothing.
		private double _foregroundSeconds;
		private double _lastRealTime = double.NaN;

		// Bumped by every start, stop and dispose, so events of a replaced run stop at once.
		private int _run;

		private Action<TimeSpan, float> _runOnTick;
		private Action<int>             _runOnInterval;
		private Action                  _runOnComplete;

		/// <summary>
		/// A countdown or count-up ticked: with the time left, or for a count-up the time counted,
		/// and the progress from 0 to 1.
		/// </summary>
		public event Action<TimeSpan, float> OnTick;

		/// <summary>An interval passed: with how many have, this one included.</summary>
		public event Action<int> OnInterval;

		/// <summary>The run reached its end.</summary>
		public event Action OnComplete;

		public event Action OnPause;

		public event Action OnResume;

		/// <summary>The timer was disposed.</summary>
		public event Action OnCancel;

		/// <param name="cancellationToken">The owner's lifetime, such as <c>GetCancellationTokenOnDestroy()</c>.</param>
		public Timer(CancellationToken cancellationToken = default) : this(UnityTimerClock.Instance, cancellationToken) { }

		/// <param name="clock">The clocks to read, such as a manual clock in tests.</param>
		/// <param name="cancellationToken">The owner's lifetime, such as <c>GetCancellationTokenOnDestroy()</c>.</param>
		public Timer(ITimerClock clock, CancellationToken cancellationToken = default)
		{
			_clock = clock ?? throw new ArgumentNullException(nameof(clock));

			if (cancellationToken.CanBeCanceled)
			{
				// The token may be cancelled on any thread: only mark it, and stop on the main thread.
				_ownerRegistration = cancellationToken.Register(timer => ((Timer)timer)._ownerGone = true, this);
			}
		}

		// ------------------------------------------------------------------ state

		public TimerState State
		{
			get
			{
				SyncOwner();
				return _schedule.State;
			}
		}

		public TimerKind Kind => _schedule.Kind;

		/// <summary>The duration of a countdown or count-up, zero for a count-up without end. The interval of an interval run.</summary>
		public TimeSpan Duration => _schedule.Duration;

		/// <summary>How often a countdown or count-up ticks. The interval of an interval run.</summary>
		public TimeSpan TickInterval => _schedule.TickLength;

		/// <summary>The time the run counts.</summary>
		public TimeBase TimeBase { get; private set; }

		/// <summary>The time left to the end, or for an interval run to the next interval. Zero for a count-up without end.</summary>
		public TimeSpan RemainingTime
		{
			get
			{
				SyncOwner();
				return _schedule.RemainingAt(Now);
			}
		}

		/// <summary>How long the run has counted.</summary>
		public TimeSpan ElapsedTime
		{
			get
			{
				SyncOwner();
				return _schedule.ElapsedAt(Now);
			}
		}

		/// <summary>From 0 to 1, how far through the run, or for an interval run through the current interval.</summary>
		public float Progress
		{
			get
			{
				SyncOwner();
				return (float)_schedule.ProgressAt(Now);
			}
		}

		/// <summary>What a tick would carry now: the time counted for a count-up, otherwise the time left.</summary>
		internal TimeSpan TickValue => Kind == TimerKind.CountUp ? ElapsedTime : RemainingTime;

		// ------------------------------------------------------------------ control

		/// <summary>
		/// Counts <paramref name="duration"/> down, replacing any run. A duration of zero or less
		/// ticks once and completes before this returns.
		/// </summary>
		/// <param name="onTick">This run's tick callback: the time left and the progress.</param>
		/// <param name="onComplete">This run's completion callback.</param>
		/// <param name="tickInterval">How often to tick; <see cref="DefaultTickInterval"/> when null.</param>
		/// <param name="timeBase">The time to count.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="tickInterval"/> isn't above zero.</exception>
		/// <exception cref="ObjectDisposedException">The timer was disposed.</exception>
		public void StartCountdown(TimeSpan duration, Action<TimeSpan, float> onTick = null, Action onComplete = null,
		                           TimeSpan? tickInterval = null, TimeBase timeBase = TimeBase.Scaled)
		{
			if (!CanStart()) return;

			_schedule.StartCountdown(Read(timeBase), duration, tickInterval ?? DefaultTickInterval);
			Launch(timeBase, onTick, null, onComplete);
		}

		/// <summary>Counts up to <paramref name="duration"/>, or without end when it is null, replacing any run.</summary>
		/// <param name="onTick">This run's tick callback: the time counted and the progress, which stays 0 without an end.</param>
		/// <param name="onComplete">This run's completion callback.</param>
		/// <param name="tickInterval">How often to tick; <see cref="DefaultTickInterval"/> when null.</param>
		/// <param name="timeBase">The time to count.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is below zero, or <paramref name="tickInterval"/> isn't above zero.</exception>
		/// <exception cref="ObjectDisposedException">The timer was disposed.</exception>
		public void StartCountUp(TimeSpan? duration = null, Action<TimeSpan, float> onTick = null, Action onComplete = null,
		                         TimeSpan? tickInterval = null, TimeBase timeBase = TimeBase.Scaled)
		{
			if (!CanStart()) return;

			_schedule.StartCountUp(Read(timeBase), duration, tickInterval ?? DefaultTickInterval);
			Launch(timeBase, onTick, null, onComplete);
		}

		/// <summary>
		/// Passes <paramref name="interval"/> <paramref name="repeatCount"/> times, or without end
		/// when it is below zero, replacing any run. A repeat count of zero completes before this
		/// returns.
		/// </summary>
		/// <param name="onInterval">This run's interval callback: how many intervals have passed, this one included.</param>
		/// <param name="onComplete">This run's completion callback.</param>
		/// <param name="timeBase">The time to count.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> isn't above zero.</exception>
		/// <exception cref="ObjectDisposedException">The timer was disposed.</exception>
		public void StartInterval(TimeSpan interval, int repeatCount = -1, Action<int> onInterval = null, Action onComplete = null,
		                          TimeBase timeBase = TimeBase.Scaled)
		{
			if (!CanStart()) return;

			_schedule.StartInterval(Read(timeBase), interval, repeatCount);
			Launch(timeBase, null, onInterval, onComplete);
		}

		/// <summary>Holds the time until <see cref="Resume"/>. Does nothing unless running.</summary>
		public void Pause()
		{
			if (_disposed) return;

			SyncOwner();
			if (_schedule.Pause(Now)) InvokeSafe(OnPause);
		}

		/// <summary>Counts on from where <see cref="Pause"/> held the time. Does nothing unless paused.</summary>
		public void Resume()
		{
			if (_disposed) return;

			SyncOwner();
			if (!_schedule.Resume(Now)) return;

			Register();
			InvokeSafe(OnResume);
		}

		/// <summary>Ends the run without completing it, and raises nothing.</summary>
		public void Stop()
		{
			if (_disposed) return;

			_run++;
			_schedule.Stop();
			ClearRun();
		}

		/// <summary>Stops the timer for good: raises <see cref="OnCancel"/> once, then drops every handler. Later starts throw.</summary>
		public void Dispose()
		{
			if (_disposed) return;

			_disposed = true;
			_run++;
			_schedule.Stop();
			ClearRun();
			_ownerRegistration.Dispose();

			InvokeSafe(OnCancel);

			OnTick     = null;
			OnInterval = null;
			OnComplete = null;
			OnPause    = null;
			OnResume   = null;
			OnCancel   = null;
		}

		/// <summary>Steps the timer as a frame of the player loop does: for tests that drive time by hand.</summary>
		internal void Step()
		{
			SyncOwner();
			if (_schedule.State == TimerState.Running) Pump();
		}

		// ------------------------------------------------------------------ internals

		private TimeSpan Now => Read(TimeBase);

		private TimeSpan Read(TimeBase timeBase)
		{
			switch (timeBase)
			{
				case TimeBase.Wall:     return new TimeSpan(_clock.UtcNow.Ticks);
				case TimeBase.Unscaled: return FromSeconds(ReadForeground());
				default:                return FromSeconds(_clock.GameTime);
			}
		}

		/// <summary>
		/// Moves the foreground time on by the real time since the last reading, at most
		/// <see cref="ForegroundTime.MaxFrameSeconds"/>, and returns it. A running timer reads it
		/// every frame and whenever it is queried, so a stall between two readings, such as time in
		/// the background, counts for at most that. The gaps while it is paused or idle don't count
		/// towards any run.
		/// </summary>
		private double ReadForeground()
		{
			double reading = _clock.RealTime;
			_foregroundSeconds += ForegroundTime.FrameStep(reading - _lastRealTime);
			_lastRealTime = reading;
			return _foregroundSeconds;
		}

		private static TimeSpan FromSeconds(double seconds) => new((long)Math.Round(seconds * TimeSpan.TicksPerSecond));

		/// <summary>True when a start may go ahead; false once the owner is gone, where starts do nothing.</summary>
		private bool CanStart()
		{
			if (_disposed) throw new ObjectDisposedException(nameof(Timer));

			SyncOwner();
			return !_ownerGone;
		}

		/// <summary>Takes over a run the schedule just started: its callbacks, its first events and the player loop.</summary>
		private void Launch(TimeBase timeBase, Action<TimeSpan, float> onTick, Action<int> onInterval, Action onComplete)
		{
			_run++;
			TimeBase       = timeBase;
			_runOnTick     = onTick;
			_runOnInterval = onInterval;
			_runOnComplete = onComplete;

			int run = _run;
			Pump();

			if (_run == run && _schedule.State == TimerState.Running) Register();
		}

		/// <summary>Raises every event due now, until none is left or a handler replaces or stops the run.</summary>
		private void Pump()
		{
			int run = _run;
			TimeSpan now = Now;

			while (_run == run && _schedule.TryNext(now, out TimerEvent next))
			{
				Raise(next, now, run);
			}
		}

		private void Raise(TimerEvent next, TimeSpan now, int run)
		{
			switch (next.Kind)
			{
				case TimerEventKind.Tick:
				{
					TimeSpan value = _schedule.Kind == TimerKind.CountUp ? _schedule.ElapsedAt(now) : _schedule.RemainingAt(now);
					float progress = (float)_schedule.ProgressAt(now);

					InvokeSafe(OnTick, value, progress);
					if (_run == run) InvokeSafe(_runOnTick, value, progress);
					break;
				}
				case TimerEventKind.Interval:
				{
					InvokeSafe(OnInterval, next.Interval);
					if (_run == run) InvokeSafe(_runOnInterval, next.Interval);
					break;
				}
				case TimerEventKind.Complete:
				{
					// The run is over: let go of its callbacks before anything can start another.
					Action onComplete = _runOnComplete;
					ClearRun();

					InvokeSafe(OnComplete);
					InvokeSafe(onComplete);
					break;
				}
			}
		}

		/// <summary>Stops the run, raising nothing, once the owner's token is cancelled.</summary>
		private void SyncOwner()
		{
			if (!_ownerGone || _schedule.State == TimerState.Idle) return;

			_run++;
			_schedule.Stop();
			ClearRun();
		}

		private void ClearRun()
		{
			_runOnTick     = null;
			_runOnInterval = null;
			_runOnComplete = null;
		}

		private void Register()
		{
			if (_registered) return;

			_runner ??= new Runner(this);
			PlayerLoopHelper.AddAction(PlayerLoopTiming.Update, _runner);
			_registered = true;
		}

		/// <summary>A frame of the player loop: steps the timer, and leaves the loop once it isn't running.</summary>
		private bool OnFrame()
		{
			Step();
			_registered = _schedule.State == TimerState.Running;
			return _registered;
		}

		private static void InvokeSafe(Action handler)
		{
			try { handler?.Invoke(); }
			catch (Exception e) { Debug.LogException(e); }
		}

		private static void InvokeSafe(Action<int> handler, int count)
		{
			try { handler?.Invoke(count); }
			catch (Exception e) { Debug.LogException(e); }
		}

		private static void InvokeSafe(Action<TimeSpan, float> handler, TimeSpan time, float progress)
		{
			try { handler?.Invoke(time, progress); }
			catch (Exception e) { Debug.LogException(e); }
		}

		private sealed class Runner : IPlayerLoopItem
		{
			private readonly Timer _timer;

			public Runner(Timer timer) => _timer = timer;

			public bool MoveNext() => _timer.OnFrame();
		}
	}
}
