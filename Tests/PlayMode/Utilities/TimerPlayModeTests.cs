using System;
using System.Collections;
using AK.Kernel.Timing;
using AK.Utilities;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AK.Tests.Utilities
{
	/// <summary>
	/// Timers on Unity's player loop, in a running game paused at time scale zero: game time
	/// stands still while the wall clock runs on to a deadline.
	/// </summary>
	public class TimerPlayModeTests
	{
		private const float WaitLimitSeconds = 10f;

		private float _timeScale;

		[SetUp]
		public void SetUp()
		{
			_timeScale = Time.timeScale;
			Time.timeScale = 0f;
		}

		[TearDown]
		public void TearDown()
		{
			Time.timeScale = _timeScale;
		}

		[UnityTest]
		public IEnumerator AWallClockCountdown_CompletesAtItsDeadline_WhileGameTimeStandsStill()
		{
			var wall = new Timer();
			var game = new Timer();
			try
			{
				TimeSpan duration = TimeSpan.FromSeconds(0.5);
				DateTime earliest = DateTime.UtcNow + duration;
				DateTime? completedAt = null;
				int ticks = 0;
				TimeSpan lastLeft = TimeSpan.MaxValue;

				wall.StartCountdown(duration, (left, _) =>
				{
					ticks++;
					lastLeft = left;
				}, () => completedAt = DateTime.UtcNow, TimeSpan.FromSeconds(0.1), TimeBase.Wall);
				game.StartCountdown(duration, tickInterval: TimeSpan.FromSeconds(0.1));

				float limit = Time.realtimeSinceStartup + WaitLimitSeconds;
				while (completedAt == null && Time.realtimeSinceStartup < limit)
				{
					yield return null;
				}

				Assert.IsNotNull(completedAt, "the wall-clock countdown completed");
				Assert.GreaterOrEqual(completedAt.Value, earliest, "never before its deadline");
				Assert.Less(completedAt.Value - earliest, TimeSpan.FromSeconds(0.5), "within a few frames of it");
				Assert.AreEqual(TimeSpan.Zero, lastLeft, "the last tick shows zero");
				Assert.That(ticks, Is.InRange(2, 6), "the start, the end, and a tick per tenth that a frame reached");
				Assert.AreEqual(TimerState.Completed, wall.State);

				Assert.AreEqual(TimerState.Running, game.State);
				Assert.AreEqual(duration, game.RemainingTime, "game time stands still at time scale zero");
			}
			finally
			{
				wall.Dispose();
				game.Dispose();
			}
		}

		[UnityTest]
		public IEnumerator DestroyingTheOwner_StopsTheTimer()
		{
			var owner = new GameObject("Timer owner");
			var timer = new Timer(owner.GetCancellationTokenOnDestroy());
			int ticks = 0;
			TimeSpan tickInterval = TimeSpan.FromMilliseconds(1);
			timer.StartCountUp(onTick: (_, _) => ticks++, tickInterval: tickInterval, timeBase: TimeBase.Wall);

			// A frame can be shorter than the tick interval, as in batch mode, so wait on the wall clock.
			float limit = Time.realtimeSinceStartup + WaitLimitSeconds;
			while (ticks < 2 && Time.realtimeSinceStartup < limit)
			{
				yield return null;
			}

			Assert.Greater(ticks, 1, "the player loop steps the timer");

			Object.Destroy(owner);
			yield return null;

			Assert.AreEqual(TimerState.Idle, timer.State);

			// Twenty tick intervals: a timer still running would tick again, however short the frames.
			int ticksWhenDestroyed = ticks;
			float quietUntil = Time.realtimeSinceStartup + 20 * (float)tickInterval.TotalSeconds;
			while (Time.realtimeSinceStartup < quietUntil)
			{
				yield return null;
			}

			Assert.AreEqual(ticksWhenDestroyed, ticks, "no ticks once the owner is gone");
		}
	}
}
