using System;
using System.Collections.Generic;
using System.Globalization;
using AK.Kernel.Timing;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	/// <summary>
	/// Scripts are readings in seconds, each with the events due by then: "0=T 0.5= 1=T,C" reads
	/// the clock at 0 and expects a tick, at 0.5 and expects nothing, and at 1 and expects a tick
	/// then completion. T is a tick, C completion, and I3 the third interval.
	/// </summary>
	public class TimerScheduleTests
	{
		private static TimeSpan S(double seconds) => new((long)Math.Round(seconds * TimeSpan.TicksPerSecond));

		// ------------------------------------------------------------------ countdowns

		[TestCase(2.5d, 1d, "0=T 0.4= 0.5=T 1.49= 1.5=T 2.49= 2.5=T,C 3=", Description = "ticks when the time left reaches a whole second")]
		[TestCase(3d, 1d, "0=T 1=T 2=T 3=T,C")]
		[TestCase(0d, 1d, "0=T,C")]
		[TestCase(-5d, 1d, "0=T,C", Description = "a duration below zero counts as zero")]
		[TestCase(10d, 1d, "0=T 0.5= 7.2=T 10=T,C", Description = "a step over many ticks raises one")]
		[TestCase(10d, 1d, "0=T 25=T,C")]
		[TestCase(3d, 1d, "0=T 2=T 1= 1.5= 3=T,C", Description = "a clock set back neither rewinds nor holds up the timer")]
		[TestCase(1d, 0.25d, "0=T 0.25=T 0.5=T 0.75=T 1=T,C")]
		public void Countdown_FollowsItsScript(double duration, double tick, string script)
		{
			var schedule = new TimerSchedule();
			schedule.StartCountdown(S(0d), S(duration), S(tick));
			Play(schedule, script);
		}

		// ------------------------------------------------------------------ count-ups

		[TestCase(2.5d, 1d, "0=T 1=T 2=T 2.5=T,C 3=")]
		[TestCase(3d, 1d, "0=T 1=T 2=T 3=T,C")]
		[TestCase(0d, 1d, "0=T,C")]
		[TestCase(2.5d, 1d, "0=T 1.9=T 2.6=T,C", Description = "a step over a tick and the end raises one tick")]
		public void CountUp_FollowsItsScript(double duration, double tick, string script)
		{
			var schedule = new TimerSchedule();
			schedule.StartCountUp(S(0d), S(duration), S(tick));
			Play(schedule, script);
		}

		[Test]
		public void CountUp_WithoutEnd_NeverCompletes()
		{
			var schedule = new TimerSchedule();
			schedule.StartCountUp(S(0d), null, S(1d));

			Play(schedule, "0=T 0.9= 1=T 2.5=T 1000000=T");
			Assert.IsTrue(schedule.IsEndless);
			Assert.AreEqual(TimeSpan.Zero, schedule.Duration);
			Assert.AreEqual(TimeSpan.Zero, schedule.RemainingAt(S(1_000_001d)));
			Assert.AreEqual(0d, schedule.ProgressAt(S(1_000_001d)));
			Assert.AreEqual(S(1_000_001d), schedule.ElapsedAt(S(1_000_001d)));
		}

		// ------------------------------------------------------------------ intervals

		[TestCase(1d, 3, "0= 0.9= 1=I1 2.5=I2 3=I3,C 4=")]
		[TestCase(1d, 3, "0= 3.5=I1,I2,I3,C", Description = "a step over several intervals raises each, in order")]
		[TestCase(1d, 0, "0=C")]
		[TestCase(1d, -1, "0= 1=I1 5=I2,I3,I4,I5 5.5=")]
		[TestCase(0.5d, 2, "0= 0.5=I1 0.75= 1=I2,C")]
		public void Interval_FollowsItsScript(double interval, int repeatCount, string script)
		{
			var schedule = new TimerSchedule();
			schedule.StartInterval(S(0d), S(interval), repeatCount);
			Play(schedule, script);
		}

		[Test]
		public void Interval_ReportsItsCounts()
		{
			var schedule = new TimerSchedule();
			schedule.StartInterval(S(0d), S(1d), -7);
			Play(schedule, "2.25=I1,I2");

			Assert.AreEqual(TimerKind.Interval, schedule.Kind);
			Assert.IsTrue(schedule.IsEndless);
			Assert.AreEqual(-1, schedule.RepeatCount);
			Assert.AreEqual(2, schedule.IntervalsPassed);
			Assert.AreEqual(S(1d), schedule.Duration);
			Assert.AreEqual(S(0.75d), schedule.RemainingAt(S(2.25d)), "to the next interval");
			Assert.AreEqual(0.25d, schedule.ProgressAt(S(2.25d)), 1e-12, "through the current interval");
		}

		// ------------------------------------------------------------------ pausing

		[Test]
		public void Pause_HoldsTheTime_UntilResume()
		{
			var schedule = new TimerSchedule();
			schedule.StartCountdown(S(0d), S(10d), S(1d));
			Play(schedule, "0=T 3=T");

			Assert.IsTrue(schedule.Pause(S(3.5d)));
			Assert.AreEqual(TimerState.Paused, schedule.State);
			Play(schedule, "5= 100=");
			Assert.AreEqual(S(6.5d), schedule.RemainingAt(S(100d)));

			Assert.IsTrue(schedule.Resume(S(20d)));
			Assert.AreEqual(TimerState.Running, schedule.State);
			Play(schedule, "20= 21=T 26.5=T,C");
		}

		[Test]
		public void PauseAndResume_ReturnFalse_OutOfTurn()
		{
			var schedule = new TimerSchedule();
			Assert.IsFalse(schedule.Pause(S(0d)), "idle");
			Assert.IsFalse(schedule.Resume(S(0d)), "idle");

			schedule.StartCountdown(S(0d), S(1d), S(1d));
			Assert.IsFalse(schedule.Resume(S(0d)), "running");
			Assert.IsTrue(schedule.Pause(S(0d)));
			Assert.IsFalse(schedule.Pause(S(0d)), "paused");

			Assert.IsTrue(schedule.Resume(S(0d)));
			Play(schedule, "0=T 1=T,C");
			Assert.IsFalse(schedule.Pause(S(1d)), "completed");
			Assert.IsFalse(schedule.Resume(S(1d)), "completed");
		}

		// ------------------------------------------------------------------ stopping and restarting

		[Test]
		public void Stop_EndsTheRun_WithoutAnEvent()
		{
			var schedule = new TimerSchedule();
			schedule.StartInterval(S(0d), S(1d), 5);
			Play(schedule, "2=I1,I2");

			schedule.Stop();

			Assert.AreEqual(TimerState.Idle, schedule.State);
			Play(schedule, "10=");
			Assert.AreEqual(TimeSpan.Zero, schedule.ElapsedAt(S(10d)));
			Assert.AreEqual(TimeSpan.Zero, schedule.RemainingAt(S(10d)));
			Assert.AreEqual(0d, schedule.ProgressAt(S(10d)));
			Assert.AreEqual(0, schedule.IntervalsPassed);
		}

		[Test]
		public void Start_ReplacesTheRun()
		{
			var schedule = new TimerSchedule();
			schedule.StartCountdown(S(0d), S(10d), S(1d));
			Play(schedule, "0=T 4=T");

			schedule.StartCountUp(S(5d), S(2d), S(1d));

			Assert.AreEqual(TimerKind.CountUp, schedule.Kind);
			Assert.AreEqual(S(2d), schedule.Duration);
			Assert.IsFalse(schedule.IsEndless);
			Play(schedule, "5=T 6=T 7=T,C");
		}

		[Test]
		public void Start_Throws_ForTicksAndIntervalsNotAboveZero_AndKeepsTheRun()
		{
			var schedule = new TimerSchedule();
			schedule.StartCountdown(S(0d), S(10d), S(1d));

			Assert.Throws<ArgumentOutOfRangeException>(() => schedule.StartCountdown(S(0d), S(5d), TimeSpan.Zero));
			Assert.Throws<ArgumentOutOfRangeException>(() => schedule.StartCountUp(S(0d), null, S(-1d)));
			Assert.Throws<ArgumentOutOfRangeException>(() => schedule.StartCountUp(S(0d), S(-1d), S(1d)));
			Assert.Throws<ArgumentOutOfRangeException>(() => schedule.StartInterval(S(0d), TimeSpan.Zero, 3));

			Assert.AreEqual(TimerKind.Countdown, schedule.Kind);
			Assert.AreEqual(S(10d), schedule.Duration);
			Play(schedule, "0=T 10=T,C");
		}

		// ------------------------------------------------------------------ queries

		[Test]
		public void Queries_LookAtTheClock_WithoutTakingItIn()
		{
			var schedule = new TimerSchedule();
			schedule.StartCountdown(S(0d), S(10d), S(1d));
			Play(schedule, "0=T");

			Assert.AreEqual(S(2.5d), schedule.ElapsedAt(S(2.5d)));
			Assert.AreEqual(S(7.5d), schedule.RemainingAt(S(2.5d)));
			Assert.AreEqual(0.25d, schedule.ProgressAt(S(2.5d)), 1e-12);

			// Had the query taken 5 in, the earlier reading after it would add nothing.
			Assert.AreEqual(S(5d), schedule.RemainingAt(S(5d)));
			Play(schedule, "0.5= 1=T");
		}

		[Test]
		public void Queries_StopAtTheEnd()
		{
			var schedule = new TimerSchedule();
			schedule.StartCountdown(S(0d), S(2d), S(1d));

			Assert.AreEqual(S(2d), schedule.ElapsedAt(S(50d)), "while running");
			Assert.AreEqual(TimeSpan.Zero, schedule.RemainingAt(S(50d)));
			Assert.AreEqual(1d, schedule.ProgressAt(S(50d)));

			Play(schedule, "50=T,C");
			Assert.AreEqual(TimerState.Completed, schedule.State);
			Assert.AreEqual(S(2d), schedule.ElapsedAt(S(60d)));
			Assert.AreEqual(TimeSpan.Zero, schedule.RemainingAt(S(60d)));
			Assert.AreEqual(1d, schedule.ProgressAt(S(60d)));
		}

		[Test]
		public void ZeroCountdown_IsAllTheWayThrough()
		{
			var schedule = new TimerSchedule();
			schedule.StartCountdown(S(0d), TimeSpan.Zero, S(1d));

			Assert.AreEqual(1d, schedule.ProgressAt(S(0d)));
			Assert.AreEqual(TimeSpan.Zero, schedule.RemainingAt(S(0d)));
		}

		// ------------------------------------------------------------------ extremes

		[Test]
		public void Readings_AtTheEdgesOfTime_Saturate()
		{
			var schedule = new TimerSchedule();
			schedule.StartCountdown(TimeSpan.MinValue, S(10d), S(1d));

			Assert.IsTrue(schedule.TryNext(TimeSpan.MinValue, out TimerEvent first));
			Assert.AreEqual(TimerEvent.Tick, first);
			Assert.AreEqual(S(4d), schedule.ElapsedAt(TimeSpan.MinValue + S(4d)));

			Assert.IsTrue(schedule.TryNext(TimeSpan.MaxValue, out TimerEvent last));
			Assert.AreEqual(TimerEvent.Tick, last);
			Assert.IsTrue(schedule.TryNext(TimeSpan.MaxValue, out TimerEvent end));
			Assert.AreEqual(TimerEvent.Complete, end);

			// Three intervals of TimeSpan.MaxValue end past it, so the end comes in to it.
			schedule.StartInterval(S(0d), TimeSpan.MaxValue, 3);
			Play(schedule, "0= 1000000=");
			Assert.AreEqual("I1,I2,I3,C", Events(schedule, TimeSpan.MaxValue), "every interval passes before the end");
		}

		[Test]
		public void RunsWithoutEnd_NeverComplete_EvenAtTheEdgeOfTime()
		{
			var schedule = new TimerSchedule();
			schedule.StartCountUp(TimeSpan.MinValue, null, S(1d));

			Assert.AreEqual("T", Events(schedule, TimeSpan.MinValue));
			Assert.AreEqual("T", Events(schedule, TimeSpan.MaxValue), "the time stops at the most a TimeSpan holds");
			Assert.AreEqual("", Events(schedule, TimeSpan.MaxValue));
			Assert.AreEqual(TimerState.Running, schedule.State);
			Assert.AreEqual(TimeSpan.MaxValue, schedule.ElapsedAt(TimeSpan.MaxValue));

			schedule.StartInterval(S(0d), new TimeSpan(long.MaxValue / 2), -1);
			Assert.AreEqual("I1,I2", Events(schedule, TimeSpan.MaxValue));
			Assert.AreEqual("", Events(schedule, TimeSpan.MaxValue));
			Assert.AreEqual(TimerState.Running, schedule.State);
		}

		// ------------------------------------------------------------------ events

		[Test]
		public void Events_CompareByValue()
		{
			Assert.AreEqual(TimerEvent.IntervalPassed(3), TimerEvent.IntervalPassed(3));
			Assert.AreNotEqual(TimerEvent.IntervalPassed(3), TimerEvent.IntervalPassed(4));
			Assert.AreNotEqual(TimerEvent.Tick, TimerEvent.Complete);
			Assert.AreEqual(TimerEventKind.None, default(TimerEvent).Kind);
			Assert.AreEqual("Interval 3", TimerEvent.IntervalPassed(3).ToString());
		}

		// ------------------------------------------------------------------ allocations

		[Test]
		public void Stepping_DoesNotAllocate()
		{
			var schedule = new TimerSchedule();
			Action exercise = () => Exercise(schedule);

			// The first run JIT-compiles the body, which allocates; the second is measured.
			GcAllocations.Count(exercise);
			int allocations = GcAllocations.Count(exercise);

			Assert.AreEqual(0, allocations);
		}

		private static void Exercise(TimerSchedule schedule)
		{
			schedule.StartCountdown(S(0d), S(100d), S(0.5d));
			for (int frame = 0; frame <= 6000; frame++)
			{
				TimeSpan now = S(frame / 60d);
				while (schedule.TryNext(now, out _)) { }
				schedule.RemainingAt(now);
				schedule.ProgressAt(now);
			}

			schedule.StartInterval(S(0d), S(0.1d), 500);
			while (schedule.TryNext(S(100d), out _)) { }
		}

		// ------------------------------------------------------------------ scripts

		/// <summary>Takes each reading of the script in turn and checks the events it hands out.</summary>
		private static void Play(TimerSchedule schedule, string script)
		{
			foreach (string step in script.Split(' '))
			{
				int equals = step.IndexOf('=');
				double seconds = double.Parse(step.Substring(0, equals), CultureInfo.InvariantCulture);
				Assert.AreEqual(step.Substring(equals + 1), Events(schedule, S(seconds)), $"at {seconds}s");
			}
		}

		/// <summary>Takes in one reading and names the events it hands out, as a script does: "T,C".</summary>
		private static string Events(TimerSchedule schedule, TimeSpan now)
		{
			var events = new List<string>();
			while (schedule.TryNext(now, out TimerEvent next))
			{
				events.Add(Name(next));
				Assert.Less(events.Count, 100, $"at {now}: the events never end");
			}

			return string.Join(",", events);
		}

		private static string Name(TimerEvent next)
		{
			switch (next.Kind)
			{
				case TimerEventKind.Tick:     return "T";
				case TimerEventKind.Complete: return "C";
				case TimerEventKind.Interval: return "I" + next.Interval.ToString(CultureInfo.InvariantCulture);
				default:                      return next.Kind.ToString();
			}
		}
	}
}
