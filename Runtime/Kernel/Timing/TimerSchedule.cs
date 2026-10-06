using System;

namespace AK.Kernel.Timing
{
	/// <summary>What a <see cref="TimerSchedule"/> run does.</summary>
	public enum TimerKind : byte
	{
		/// <summary>Counts a duration down to zero.</summary>
		Countdown = 0,

		/// <summary>Counts up from zero, to a duration or without end.</summary>
		CountUp = 1,

		/// <summary>Passes an interval again and again, a number of times or without end.</summary>
		Interval = 2,
	}

	/// <summary>Where a timer is in its run.</summary>
	public enum TimerState : byte
	{
		/// <summary>Not started, or stopped.</summary>
		Idle = 0,

		/// <summary>Counting.</summary>
		Running = 1,

		/// <summary>Holding its time until it resumes.</summary>
		Paused = 2,

		/// <summary>At its end. Starting it again begins a new run.</summary>
		Completed = 3,
	}

	/// <summary>What a <see cref="TimerEvent"/> reports.</summary>
	public enum TimerEventKind : byte
	{
		/// <summary>No event: the default value.</summary>
		None = 0,

		/// <summary>The time a countdown or count-up shows should update.</summary>
		Tick = 1,

		/// <summary>An interval passed.</summary>
		Interval = 2,

		/// <summary>The run reached its end. Always the run's last event.</summary>
		Complete = 3,
	}

	/// <summary>An event that <see cref="TimerSchedule.TryNext"/> hands out.</summary>
	public readonly struct TimerEvent : IEquatable<TimerEvent>
	{
		public static readonly TimerEvent Tick     = new(TimerEventKind.Tick, 0);
		public static readonly TimerEvent Complete = new(TimerEventKind.Complete, 0);

		public readonly TimerEventKind Kind;

		/// <summary>
		/// For <see cref="TimerEventKind.Interval"/>: the intervals passed so far, this one
		/// included, so 1 for the first. Zero for the other events.
		/// </summary>
		public readonly int Interval;

		private TimerEvent(TimerEventKind kind, int interval)
		{
			Kind     = kind;
			Interval = interval;
		}

		/// <summary>The interval numbered <paramref name="count"/>, from 1, passed.</summary>
		public static TimerEvent IntervalPassed(int count) => new(TimerEventKind.Interval, count);

		public bool Equals(TimerEvent other) => Kind == other.Kind && Interval == other.Interval;

		public override bool Equals(object obj) => obj is TimerEvent other && Equals(other);

		public override int GetHashCode() => (int)Kind * 397 ^ Interval;

		public override string ToString() => Kind == TimerEventKind.Interval ? $"Interval {Interval}" : Kind.ToString();
	}

	/// <summary>
	/// When one timer ticks, passes its intervals and completes, on a clock its owner reads.
	/// The owner steps it with a reading, typically once a frame, and raises each event that
	/// <see cref="TryNext"/> hands out.
	///
	/// <para><b>Time</b> adds up how far the clock moved forward between readings. A reading
	/// earlier than the last one adds nothing, so a clock set back neither rewinds the timer nor
	/// holds it up, and no rounding builds up from step to step. <see cref="TryNext"/> and
	/// <see cref="Pause"/> take a reading in; the queries only look at one.</para>
	///
	/// <para><b>Ticks</b> mark when the time shown should change. A countdown ticks at its start,
	/// each time its remaining time reaches a multiple of the tick length, and at zero: with a
	/// one-second tick, a countdown of 2.5 seconds ticks with 2.5, 2, 1 and 0 seconds left, which
	/// is when its remaining time rounded up changes. A count-up ticks at its start, at each
	/// multiple of the tick length, and at its end if it has one. A step that covers several
	/// ticks hands out one.</para>
	///
	/// <para><b>Intervals</b> pass one by one, in order: a step that covers three hands out three
	/// events. An interval run doesn't tick.</para>
	///
	/// <para>Each run ends with <see cref="TimerEventKind.Complete"/>, right after its last tick
	/// or interval, in the same step. A run without end never completes: its time stops at
	/// <see cref="TimeSpan.MaxValue"/>, and an interval run stops counting at
	/// <see cref="int.MaxValue"/> intervals.</para>
	///
	/// No allocations. Not thread-safe.
	/// </summary>
	public sealed class TimerSchedule
	{
		private long _length;       // the duration, or the interval for an interval run
		private long _tick;         // the tick length, or the interval for an interval run
		private long _end;          // elapsed ticks where the run completes, or its time stops without end
		private long _elapsed;
		private long _lastReading;
		private long _lastTick;     // the last tick handed out, numbered from 0; -1 before the first
		private int  _intervals;    // intervals handed out
		private int  _maxIntervals;

		public TimerKind Kind { get; private set; }

		public TimerState State { get; private set; }

		/// <summary>The duration of a countdown or count-up, zero for a count-up without end. The interval of an interval run.</summary>
		public TimeSpan Duration => new(_length);

		/// <summary>How often a countdown or count-up ticks. The interval of an interval run.</summary>
		public TimeSpan TickLength => new(_tick);

		/// <summary>A count-up without a duration, or an interval run without a repeat count.</summary>
		public bool IsEndless { get; private set; }

		/// <summary>How many intervals an interval run passes; -1 without end. Zero for other runs.</summary>
		public int RepeatCount { get; private set; }

		/// <summary>The intervals an interval run has handed out.</summary>
		public int IntervalsPassed => _intervals;

		// ------------------------------------------------------------------ starting

		/// <summary>Starts counting <paramref name="duration"/> down. A duration below zero counts as zero.</summary>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> isn't above zero.</exception>
		public void StartCountdown(TimeSpan now, TimeSpan duration, TimeSpan tick)
		{
			RequirePositive(tick, nameof(tick));

			long length = Math.Max(duration.Ticks, 0L);
			Begin(TimerKind.Countdown, now, length, tick.Ticks, length);
		}

		/// <summary>Starts counting up, to <paramref name="duration"/>, or without end when it is null.</summary>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is below zero, or <paramref name="tick"/> isn't above zero.</exception>
		public void StartCountUp(TimeSpan now, TimeSpan? duration, TimeSpan tick)
		{
			RequirePositive(tick, nameof(tick));

			if (duration.HasValue && duration.Value.Ticks < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(duration), duration.Value, "A count-up runs to a duration of zero or more, or without end.");
			}

			long length = duration?.Ticks ?? 0L;
			Begin(TimerKind.CountUp, now, length, tick.Ticks, duration.HasValue ? length : long.MaxValue);
			IsEndless = !duration.HasValue;
		}

		/// <summary>
		/// Starts passing <paramref name="interval"/> <paramref name="repeatCount"/> times: below
		/// zero for no end, where intervals past <see cref="int.MaxValue"/> pass uncounted, and zero
		/// to complete at once.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> isn't above zero.</exception>
		public void StartInterval(TimeSpan now, TimeSpan interval, int repeatCount)
		{
			RequirePositive(interval, nameof(interval));

			bool endless = repeatCount < 0;
			int maxIntervals = endless ? int.MaxValue : repeatCount;
			Begin(TimerKind.Interval, now, interval.Ticks, interval.Ticks, endless ? long.MaxValue : MultiplySaturating(maxIntervals, interval.Ticks));
			_maxIntervals = maxIntervals;
			RepeatCount   = endless ? -1 : repeatCount;
			IsEndless     = endless;
		}

		/// <summary>Holds the time where it is at <paramref name="now"/>. False unless running.</summary>
		public bool Pause(TimeSpan now)
		{
			if (State != TimerState.Running) return false;

			Advance(now.Ticks);
			State = TimerState.Paused;
			return true;
		}

		/// <summary>Counts on from <paramref name="now"/>, where the pause held the time. False unless paused.</summary>
		public bool Resume(TimeSpan now)
		{
			if (State != TimerState.Paused) return false;

			_lastReading = now.Ticks;
			State = TimerState.Running;
			return true;
		}

		/// <summary>Ends the run without completing it.</summary>
		public void Stop()
		{
			State      = TimerState.Idle;
			_elapsed   = 0;
			_intervals = 0;
		}

		// ------------------------------------------------------------------ stepping

		/// <summary>
		/// Takes in the reading <paramref name="now"/> and hands out the next event due by then:
		/// true with it, or false when none is left. Call it until it returns false, raising each
		/// event in turn; raising one may pause, stop or restart the run, and the next call
		/// follows that.
		/// </summary>
		public bool TryNext(TimeSpan now, out TimerEvent next)
		{
			next = default;
			if (State != TimerState.Running) return false;

			Advance(now.Ticks);
			long elapsed = Math.Min(_elapsed, _end);
			bool atEnd = elapsed == _end && !IsEndless;

			if (Kind == TimerKind.Interval)
			{
				// At the end every interval is due, also when the end was brought in to TimeSpan.MaxValue.
				long due = atEnd ? _maxIntervals : Math.Min(elapsed / _length, _maxIntervals);
				if (_intervals < due)
				{
					next = TimerEvent.IntervalPassed(++_intervals);
					return true;
				}
			}
			else
			{
				long tick = TickAt(elapsed, atEnd);
				if (tick > _lastTick)
				{
					_lastTick = tick;
					next = TimerEvent.Tick;
					return true;
				}
			}

			if (!atEnd) return false;

			State = TimerState.Completed;
			next = TimerEvent.Complete;
			return true;
		}

		// ------------------------------------------------------------------ queries

		/// <summary>How long the run has counted, up to its end. Zero when idle.</summary>
		public TimeSpan ElapsedAt(TimeSpan now) => new(ElapsedTicks(now.Ticks));

		/// <summary>
		/// The time left to the end, or for an interval run to the next interval. Zero for a
		/// count-up without end, once the run is over, and when idle.
		/// </summary>
		public TimeSpan RemainingAt(TimeSpan now)
		{
			if (State == TimerState.Idle || State == TimerState.Completed) return TimeSpan.Zero;

			long elapsed = ElapsedTicks(now.Ticks);
			if (Kind == TimerKind.Interval) return new TimeSpan(_length - elapsed % _length);

			return IsEndless ? TimeSpan.Zero : new TimeSpan(_end - elapsed);
		}

		/// <summary>
		/// From 0 to 1, how far through the run, or for an interval run through the current
		/// interval. 1 once complete; 0 for a count-up without end and when idle.
		/// </summary>
		public double ProgressAt(TimeSpan now)
		{
			if (State == TimerState.Idle) return 0d;
			if (State == TimerState.Completed) return 1d;

			long elapsed = ElapsedTicks(now.Ticks);
			if (Kind == TimerKind.Interval) return (double)(elapsed % _length) / _length;
			if (IsEndless) return 0d;

			return _end == 0 ? 1d : (double)elapsed / _end;
		}

		// ------------------------------------------------------------------ internals

		private void Begin(TimerKind kind, TimeSpan now, long length, long tick, long end)
		{
			Kind          = kind;
			State         = TimerState.Running;
			IsEndless     = false;
			RepeatCount   = 0;
			_length       = length;
			_tick         = tick;
			_end          = end;
			_elapsed      = 0;
			_lastReading  = now.Ticks;
			_lastTick     = -1;
			_intervals    = 0;
			_maxIntervals = 0;
		}

		/// <summary>
		/// The number of the last tick due by <paramref name="elapsed"/>. A countdown's ticks
		/// count the multiples of the tick length its remaining time has reached; a count-up's,
		/// the multiples its elapsed time has. A count-up's end comes after all of them.
		/// </summary>
		private long TickAt(long elapsed, bool atEnd)
		{
			if (Kind == TimerKind.Countdown) return CeilDiv(_end, _tick) - CeilDiv(_end - elapsed, _tick);

			return atEnd ? long.MaxValue : elapsed / _tick;
		}

		private long ElapsedTicks(long now)
		{
			switch (State)
			{
				case TimerState.Idle:    return 0;
				case TimerState.Running: return Math.Min(AddSaturating(_elapsed, Forward(now)), _end);
				default:                 return Math.Min(_elapsed, _end);
			}
		}

		/// <summary>Adds how far the clock moved forward since the last reading.</summary>
		private void Advance(long now)
		{
			_elapsed     = AddSaturating(_elapsed, Forward(now));
			_lastReading = now;
		}

		/// <summary>How far the clock moved forward since the last reading; zero if it went back.</summary>
		private long Forward(long now)
		{
			if (now <= _lastReading) return 0;

			// The difference of two longs fits a ulong exactly.
			ulong forward = unchecked((ulong)now - (ulong)_lastReading);
			return forward > long.MaxValue ? long.MaxValue : (long)forward;
		}

		private static void RequirePositive(TimeSpan length, string name)
		{
			if (length.Ticks <= 0)
			{
				throw new ArgumentOutOfRangeException(name, length, "A timer's tick and interval are above zero.");
			}
		}

		/// <summary>For a of zero or more and b above zero.</summary>
		private static long CeilDiv(long a, long b) => a / b + (a % b == 0 ? 0 : 1);

		/// <summary>For a and b of zero or more.</summary>
		private static long AddSaturating(long a, long b) => b > long.MaxValue - a ? long.MaxValue : a + b;

		/// <summary>For a and b of zero or more.</summary>
		private static long MultiplySaturating(long a, long b) => a != 0 && b > long.MaxValue / a ? long.MaxValue : a * b;
	}
}
