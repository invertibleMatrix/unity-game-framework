using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using AK.Kernel.Timing;
using AK.Tests.Support;
using AK.Utilities;
using NUnit.Framework;
using Timer = AK.Utilities.Timer;

namespace AK.Tests.Utilities
{
	/// <summary>
	/// The timer on clocks set by hand, stepped as frames of the player loop would step it. The
	/// timers also join Unity's real player loop; they are disposed after each test, so they
	/// leave it.
	/// </summary>
	public class TimerTests
	{
		private ManualTimerClock _clock;
		private List<string>     _log;
		private List<Timer>      _timers;

		[SetUp]
		public void SetUp()
		{
			_clock  = new ManualTimerClock();
			_log    = new List<string>();
			_timers = new List<Timer>();
		}

		[TearDown]
		public void TearDown()
		{
			foreach (Timer timer in _timers)
			{
				timer.Dispose();
			}
		}

		private Timer NewTimer(CancellationToken owner = default)
		{
			var timer = new Timer(_clock, owner);
			_timers.Add(timer);
			return timer;
		}

		/// <summary>Sets the clocks to <paramref name="seconds"/> and steps the timer.</summary>
		private void FrameAt(Timer timer, double seconds)
		{
			_clock.Set(seconds);
			timer.Step();
		}

		private static TimeSpan S(double seconds) => new((long)Math.Round(seconds * TimeSpan.TicksPerSecond));

		private static string Seconds(TimeSpan time) => time.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture);

		// ------------------------------------------------------------------ countdowns

		[Test]
		public void Countdown_TicksAtItsStart_OnEachWholeSecondLeft_AndAtZero_ThenCompletes()
		{
			Timer timer = NewTimer();
			timer.StartCountdown(S(2.5d), (left, _) => _log.Add("T" + Seconds(left)), () => _log.Add("C"), S(1d));

			CollectionAssert.AreEqual(new[] { "T2.5" }, _log, "the first tick comes before the start returns");

			FrameAt(timer, 0.4d);
			FrameAt(timer, 0.5d);
			FrameAt(timer, 1.5d);
			FrameAt(timer, 2.49d);
			FrameAt(timer, 2.5d);
			FrameAt(timer, 3d);

			CollectionAssert.AreEqual(new[] { "T2.5", "T2", "T1", "T0", "C" }, _log);
			Assert.AreEqual(TimerState.Completed, timer.State);
			Assert.AreEqual(1f, timer.Progress);
		}

		[Test]
		public void Countdown_OfZero_TicksAndCompletes_BeforeStartReturns()
		{
			Timer timer = NewTimer();
			timer.StartCountdown(TimeSpan.Zero, (left, _) => _log.Add("T" + Seconds(left)), () => _log.Add("C"));

			CollectionAssert.AreEqual(new[] { "T0", "C" }, _log);
			Assert.AreEqual(TimerState.Completed, timer.State);
		}

		[Test]
		public void EachTimeBase_CountsItsOwnClock()
		{
			Timer scaled   = NewTimer();
			Timer unscaled = NewTimer();
			Timer wall     = NewTimer();
			scaled.StartCountdown(S(10d), tickInterval: S(1d));
			unscaled.StartCountdown(S(10d), tickInterval: S(1d), timeBase: TimeBase.Unscaled);
			wall.StartCountdown(S(10d), tickInterval: S(1d), timeBase: TimeBase.Wall);

			_clock.GameTime += 0.25d;
			Assert.AreEqual(S(9.75d), scaled.RemainingTime);
			Assert.AreEqual(S(10d), unscaled.RemainingTime, "the time scale doesn't reach unscaled time");
			Assert.AreEqual(S(10d), wall.RemainingTime, "the time scale doesn't reach the wall clock");

			_clock.RealTime += 0.25d;
			Assert.AreEqual(S(9.75d), scaled.RemainingTime, "a paused game counts no real time");
			Assert.AreEqual(S(9.75d), unscaled.RemainingTime);
			Assert.AreEqual(S(10d), wall.RemainingTime);

			_clock.UtcNow += S(3d);
			Assert.AreEqual(S(9.75d), scaled.RemainingTime, "a paused game doesn't count wall time");
			Assert.AreEqual(S(9.75d), unscaled.RemainingTime, "unscaled time isn't the wall clock");
			Assert.AreEqual(S(7d), wall.RemainingTime);

			Assert.AreEqual(TimeBase.Scaled, scaled.TimeBase);
			Assert.AreEqual(TimeBase.Unscaled, unscaled.TimeBase);
			Assert.AreEqual(TimeBase.Wall, wall.TimeBase);
		}

		[Test]
		public void Unscaled_CountsAStall_AsOneShortFrame()
		{
			_clock.RealTime = 1000d;
			Timer timer = NewTimer();
			timer.StartCountdown(S(10d), tickInterval: S(1d), timeBase: TimeBase.Unscaled);
			Assert.AreEqual(S(10d), timer.RemainingTime, "the real time before the start doesn't count");

			_clock.RealTime += 60d; // a minute in the background
			Assert.AreEqual(S(10d - ForegroundTime.MaxFrameSeconds), timer.RemainingTime);

			_clock.RealTime += 0.25d;
			Assert.AreEqual(S(10d - ForegroundTime.MaxFrameSeconds - 0.25d), timer.RemainingTime);
		}

		[Test]
		public void Unscaled_TicksAndCompletes_WhileTheGameIsPaused()
		{
			Timer timer = NewTimer();
			timer.StartCountdown(S(1d), (left, _) => _log.Add("T" + Seconds(left)), () => _log.Add("C"), S(0.5d), TimeBase.Unscaled);

			for (int frame = 0; frame < 4; frame++)
			{
				_clock.RealTime += 0.25d;
				timer.Step();
			}

			CollectionAssert.AreEqual(new[] { "T1", "T0.5", "T0", "C" }, _log);
			Assert.AreEqual(0d, _clock.GameTime, "game time never moved");
		}

		[Test]
		public void Properties_ReadTheClock_BetweenSteps()
		{
			Timer timer = NewTimer();
			timer.StartCountdown(S(10d), tickInterval: S(1d));

			_clock.Set(2.5d);

			Assert.AreEqual(S(7.5d), timer.RemainingTime);
			Assert.AreEqual(S(2.5d), timer.ElapsedTime);
			Assert.AreEqual(0.25f, timer.Progress);
			Assert.AreEqual(S(10d), timer.Duration);
			Assert.AreEqual(S(1d), timer.TickInterval);
			Assert.AreEqual(TimerKind.Countdown, timer.Kind);
		}

		// ------------------------------------------------------------------ count-ups and intervals

		[Test]
		public void CountUp_TicksWithTheTimeCounted_AndCompletesAtItsEnd()
		{
			Timer timer = NewTimer();
			timer.StartCountUp(S(2d), (counted, progress) => _log.Add($"T{Seconds(counted)}@{progress.ToString(CultureInfo.InvariantCulture)}"),
			                   () => _log.Add("C"), S(1d));

			FrameAt(timer, 1d);
			FrameAt(timer, 3d);

			CollectionAssert.AreEqual(new[] { "T0@0", "T1@0.5", "T2@1", "C" }, _log);
		}

		[Test]
		public void CountUp_WithoutEnd_RunsOn()
		{
			Timer timer = NewTimer();
			timer.StartCountUp(onTick: (counted, progress) => _log.Add("T" + Seconds(counted)), tickInterval: S(1d));

			FrameAt(timer, 1.5d);
			FrameAt(timer, 100d);

			CollectionAssert.AreEqual(new[] { "T0", "T1.5", "T100" }, _log);
			Assert.AreEqual(TimerState.Running, timer.State);
			Assert.AreEqual(0f, timer.Progress);
			Assert.AreEqual(TimeSpan.Zero, timer.RemainingTime);
		}

		[Test]
		public void Interval_RaisesEachIntervalInOrder_ThenCompletes()
		{
			Timer timer = NewTimer();
			timer.OnInterval += count => _log.Add("timer " + count);
			timer.StartInterval(S(1d), 3, count => _log.Add("run " + count), () => _log.Add("C"));

			FrameAt(timer, 2.5d);
			FrameAt(timer, 3d);

			CollectionAssert.AreEqual(new[] { "timer 1", "run 1", "timer 2", "run 2", "timer 3", "run 3", "C" }, _log);
		}

		// ------------------------------------------------------------------ runs and handlers

		[Test]
		public void Events_ReachTheTimersHandlers_ThenTheRunsCallback()
		{
			Timer timer = NewTimer();
			timer.OnTick     += (_, _) => _log.Add("timer tick");
			timer.OnComplete += () => _log.Add("timer complete");
			timer.StartCountdown(S(1d), (_, _) => _log.Add("run tick"), () => _log.Add("run complete"), S(1d));

			FrameAt(timer, 1d);

			CollectionAssert.AreEqual(new[] { "timer tick", "run tick", "timer tick", "run tick", "timer complete", "run complete" }, _log);
		}

		[Test]
		public void RunCallbacks_BelongToTheirRunOnly()
		{
			Timer timer = NewTimer();
			timer.StartCountdown(S(1d), onComplete: () => _log.Add("first"), tickInterval: S(1d));
			timer.StartCountdown(S(1d), onComplete: () => _log.Add("second"), tickInterval: S(1d));

			FrameAt(timer, 1d);

			CollectionAssert.AreEqual(new[] { "second" }, _log);
		}

		[Test]
		public void Restarting_FromTheCompletionCallback_RunsTheNewRun()
		{
			Timer timer = NewTimer();
			int runs = 0;

			void StartRun() => timer.StartCountdown(S(1d), onComplete: () =>
			{
				_log.Add("done " + ++runs);
				if (runs < 3) StartRun();
			}, tickInterval: S(1d));

			StartRun();
			FrameAt(timer, 1d);
			FrameAt(timer, 2d);
			FrameAt(timer, 3d);

			CollectionAssert.AreEqual(new[] { "done 1", "done 2", "done 3" }, _log);
			Assert.AreEqual(TimerState.Completed, timer.State);
		}

		[Test]
		public void Restarting_FromATickHandler_DropsTheOldRunsTick()
		{
			Timer timer = NewTimer();
			timer.OnTick += (left, _) =>
			{
				if (left == S(1d)) timer.StartCountdown(S(5d), (l, _) => _log.Add("new " + Seconds(l)), tickInterval: S(1d));
			};
			timer.StartCountdown(S(2d), (left, _) => _log.Add("old " + Seconds(left)), tickInterval: S(1d));

			FrameAt(timer, 1d);

			CollectionAssert.AreEqual(new[] { "old 2", "new 5" }, _log);
			Assert.AreEqual(S(5d), timer.Duration);
		}

		[Test]
		public void AHandlerThatThrows_IsLogged_AndTheTimerCarriesOn()
		{
			Timer timer = NewTimer();
			timer.OnTick += (_, _) => throw new InvalidOperationException("tick handler failed");

			using (ExpectedLog.Exception("tick handler failed", 2))
			{
				timer.StartCountdown(S(1d), (_, _) => _log.Add("tick"), () => _log.Add("complete"), S(1d));
				FrameAt(timer, 1d);
			}

			CollectionAssert.AreEqual(new[] { "tick", "tick", "complete" }, _log);
		}

		[Test]
		public void AStartWithABadTick_Throws_AndKeepsTheRun()
		{
			Timer timer = NewTimer();
			timer.StartCountdown(S(10d), onComplete: () => _log.Add("C"), tickInterval: S(1d));

			Assert.Throws<ArgumentOutOfRangeException>(() => timer.StartCountdown(S(5d), tickInterval: TimeSpan.Zero));
			Assert.Throws<ArgumentOutOfRangeException>(() => timer.StartInterval(TimeSpan.Zero));

			Assert.AreEqual(S(10d), timer.Duration);
			FrameAt(timer, 10d);
			CollectionAssert.AreEqual(new[] { "C" }, _log);
		}

		// ------------------------------------------------------------------ pausing, stopping, disposing

		[Test]
		public void Pause_HoldsTheTime_UntilResume()
		{
			Timer timer = NewTimer();
			timer.OnPause  += () => _log.Add("pause");
			timer.OnResume += () => _log.Add("resume");
			timer.StartCountdown(S(10d), tickInterval: S(1d));

			FrameAt(timer, 3d);
			timer.Pause();
			timer.Pause();
			FrameAt(timer, 100d);

			Assert.AreEqual(TimerState.Paused, timer.State);
			Assert.AreEqual(S(7d), timer.RemainingTime);

			timer.Resume();
			timer.Resume();
			FrameAt(timer, 102d);

			Assert.AreEqual(S(5d), timer.RemainingTime);
			CollectionAssert.AreEqual(new[] { "pause", "resume" }, _log);
		}

		[Test]
		public void Stop_EndsTheRun_AndRaisesNothing()
		{
			Timer timer = NewTimer();
			timer.OnCancel   += () => _log.Add("cancel");
			timer.OnComplete += () => _log.Add("complete");
			timer.StartCountdown(S(1d), (_, _) => _log.Add("tick"), () => _log.Add("run complete"), S(1d));
			_log.Clear();

			timer.Stop();
			FrameAt(timer, 5d);

			CollectionAssert.IsEmpty(_log);
			Assert.AreEqual(TimerState.Idle, timer.State);
			Assert.AreEqual(TimeSpan.Zero, timer.RemainingTime);
		}

		[Test]
		public void Dispose_RaisesOnCancelOnce_ThenStartsThrow()
		{
			Timer timer = NewTimer();
			timer.OnCancel += () =>
			{
				_log.Add("cancel");
				timer.Dispose();
			};
			timer.StartCountdown(S(5d));

			timer.Dispose();
			timer.Dispose();

			CollectionAssert.AreEqual(new[] { "cancel" }, _log);
			Assert.AreEqual(TimerState.Idle, timer.State);
			Assert.Throws<ObjectDisposedException>(() => timer.StartCountdown(S(1d)));
			Assert.Throws<ObjectDisposedException>(() => timer.StartCountUp());
			Assert.Throws<ObjectDisposedException>(() => timer.StartInterval(S(1d)));
			Assert.DoesNotThrow(() =>
			{
				timer.Pause();
				timer.Resume();
				timer.Stop();
			});
		}

		[Test]
		public void TheOwnersToken_StopsTheTimerSilently_AndLaterStartsDoNothing()
		{
			using var owner = new CancellationTokenSource();
			Timer timer = NewTimer(owner.Token);
			timer.OnCancel   += () => _log.Add("cancel");
			timer.OnComplete += () => _log.Add("complete");
			timer.StartCountdown(S(5d), (_, _) => _log.Add("tick"), () => _log.Add("run complete"), S(1d));
			_log.Clear();

			owner.Cancel();

			Assert.AreEqual(TimerState.Idle, timer.State);
			FrameAt(timer, 10d);
			timer.StartCountdown(S(1d), (_, _) => _log.Add("tick"), () => _log.Add("run complete"));
			timer.StartInterval(S(1d), 1, _ => _log.Add("interval"));
			FrameAt(timer, 20d);

			CollectionAssert.IsEmpty(_log);
			Assert.AreEqual(TimerState.Idle, timer.State);
		}

		[Test]
		public void AnOwnerAlreadyGone_LeavesTheTimerInert()
		{
			Timer timer = NewTimer(new CancellationToken(true));

			timer.StartCountdown(TimeSpan.Zero, (_, _) => _log.Add("tick"), () => _log.Add("complete"));

			CollectionAssert.IsEmpty(_log);
			Assert.AreEqual(TimerState.Idle, timer.State);
		}

		[Test]
		public void Constructor_Throws_WithoutAClock()
		{
			Assert.Throws<ArgumentNullException>(() => new Timer(null));
		}

		// ------------------------------------------------------------------ allocations

		[Test]
		public void Ticking_DoesNotAllocate()
		{
			Timer timer = NewTimer();
			int ticks = 0;
			timer.OnTick += (_, _) => ticks++;

			Action exercise = () =>
			{
				_clock.Set(0d);
				timer.StartCountdown(S(100d), tickInterval: S(0.5d));
				for (int frame = 1; frame <= 6000; frame++)
				{
					_clock.Set(frame / 60d);
					timer.Step();
				}
			};

			// The first run JIT-compiles the body and joins the player loop, which allocates; the second is measured.
			GcAllocations.Count(exercise);
			ticks = 0;
			int allocations = GcAllocations.Count(exercise);

			Assert.AreEqual(0, allocations);
			Assert.AreEqual(201, ticks, "the start, then one tick per half second");
			Assert.AreEqual(TimerState.Completed, timer.State);
		}
	}
}
