using System;
using System.Collections.Generic;
using System.Threading;
using AK.Kernel.Timing;
using AK.Utilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
#if UGFW_TMP
using TMPro;
#endif
using Object = UnityEngine.Object;
using Timer = AK.Utilities.Timer;

namespace AK.Tests.Utilities
{
	public class TimerExtensionsTests
	{
		private static TimeSpan Seconds(double seconds) => TimeSpan.FromTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));

		// ------------------------------------------------------------------ formatting

		[TestCase(0d, "00:00")]
		[TestCase(59.9d, "00:59")]
		[TestCase(90d, "01:30")]
		[TestCase(65 * 60d, "65:00", Description = "minutes past the hour count on")]
		[TestCase(-90d, "-01:30")]
		public void ToMMSS_ShowsTotalMinutes(double seconds, string expected)
		{
			Assert.AreEqual(expected, Seconds(seconds).ToMMSS());
		}

		[TestCase(0d, "00:00:00")]
		[TestCase(3661d, "01:01:01")]
		[TestCase(25 * 3600d, "25:00:00", Description = "hours past the day count on")]
		[TestCase(-1d, "-00:00:01")]
		public void ToHHMMSS_ShowsTotalHours(double seconds, string expected)
		{
			Assert.AreEqual(expected, Seconds(seconds).ToHHMMSS());
		}

		[TestCase(25 * 3600d, "1d 1h")]
		[TestCase(2.5 * 3600d, "2h 30m")]
		[TestCase(45d, "45s")]
		[TestCase(5 * 60d, "5m")]
		[TestCase(0d, "0s")]
		[TestCase(-90d, "-1m 30s")]
		public void ToCompactFormat_ShowsTheTwoLargestUnits(double seconds, string expected)
		{
			Assert.AreEqual(expected, Seconds(seconds).ToCompactFormat());
		}

		[TestCase((2 * 24 + 5) * 3600d + 30 * 60, "2d 05:30:00")]
		[TestCase(5 * 3600d + 30 * 60, "05:30:00")]
		[TestCase(-(5 * 3600d + 30 * 60), "-05:30:00")]
		public void ToDurationFormat_ShowsDaysBeforeTheClock(double seconds, string expected)
		{
			Assert.AreEqual(expected, Seconds(seconds).ToDurationFormat());
		}

		[Test]
		public void Formatting_HandlesTheLargestTimeSpans()
		{
			Assert.AreEqual("-15372286728:05", TimeSpan.MinValue.ToMMSS());
			Assert.AreEqual("256204778:48:05", TimeSpan.MaxValue.ToHHMMSS());
			Assert.AreEqual("10675199d 02:48:05", TimeSpan.MaxValue.ToDurationFormat());
		}

		[TestCase(59.2d, TimeRounding.Ceil, "00:01:00")]
		[TestCase(0.2d, TimeRounding.Ceil, "00:00:01")]
		[TestCase(3599.5d, TimeRounding.Nearest, "01:00:00")]
		[TestCase(-0.4d, TimeRounding.Floor, "00:00:00", Description = "a time below zero that shows as zero has no sign")]
		[TestCase(-0.4d, TimeRounding.Ceil, "-00:00:01", Description = "a negative time is rounded by its size")]
		public void ToHHMMSS_RoundsAsAsked(double seconds, TimeRounding rounding, string expected)
		{
			Assert.AreEqual(expected, Seconds(seconds).ToHHMMSS(rounding));
		}

		[TestCase(59.1d, TimeRounding.Ceil, "01:00")]
		[TestCase(59.9d, TimeRounding.Floor, "00:59")]
		[TestCase(-0.5d, TimeRounding.Nearest, "-00:01")]
		public void ToMMSS_RoundsAsAsked(double seconds, TimeRounding rounding, string expected)
		{
			Assert.AreEqual(expected, Seconds(seconds).ToMMSS(rounding));
		}

		[Test]
		public void ACountdownShownRoundedUp_ReadsZero_OnlyAsItCompletes()
		{
			var clock = new ManualTimerClock();
			var shown = new List<string>();
			string completedWith = null;

			using var timer = new Timer(clock);
			timer.StartCountdown(Seconds(3.4d), (left, _) => shown.Add(left.ToHHMMSS(TimeRounding.Ceil)),
			                     () => completedWith = shown[shown.Count - 1], TimeSpan.FromSeconds(1));

			for (int frame = 1; timer.State == TimerState.Running; frame++)
			{
				clock.Set(frame / 60d);
				timer.Step();
				Assert.IsTrue(timer.State != TimerState.Running || !shown.Contains("00:00:00"), $"frame {frame}: zero shown while running");
			}

			CollectionAssert.AreEqual(new[] { "00:00:04", "00:00:03", "00:00:02", "00:00:01", "00:00:00" }, shown);
			Assert.AreEqual("00:00:00", completedWith);
			Assert.AreEqual(3.4d, clock.GameTime, 1d / 60d, "completes on the frame of the deadline");
		}

		// ------------------------------------------------------------------ factories

		[Test]
		public void DailyReset_CountsDownToTheNextReset()
		{
			var clock = new ManualTimerClock();
			TimeSpan day = TimeSpan.FromDays(1);

			using Timer fromThePast = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).CreateDailyResetTimer(day, clock: clock);
			Assert.AreEqual(TimeSpan.FromHours(12), fromThePast.RemainingTime);
			Assert.AreEqual(TimeBase.Wall, fromThePast.TimeBase);

			using Timer fromTheFuture = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc).CreateDailyResetTimer(day, clock: clock);
			Assert.AreEqual(TimeSpan.FromHours(12), fromTheFuture.RemainingTime);

			using Timer rightNow = ManualTimerClock.Origin.CreateDailyResetTimer(day, clock: clock);
			Assert.AreEqual(day, rightNow.RemainingTime, "a reset right now leaves a whole period to the next");

			using Timer unspecified = new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Unspecified).CreateDailyResetTimer(day, clock: clock);
			Assert.AreEqual(TimeSpan.FromHours(6), unspecified.RemainingTime, "a time of unspecified kind is taken as UTC");

			DateTime local = new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc).ToLocalTime();
			using Timer fromLocal = local.CreateDailyResetTimer(day, clock: clock);
			Assert.AreEqual(TimeSpan.FromHours(6), fromLocal.RemainingTime, "a local time is converted");

			Assert.Throws<ArgumentOutOfRangeException>(() => ManualTimerClock.Origin.CreateDailyResetTimer(TimeSpan.Zero, clock: clock));
		}

		[Test]
		public void Cooldown_CountsDownWhatIsLeft()
		{
			var clock = new ManualTimerClock();

			using Timer running = TimeSpan.FromMinutes(10).CreateCooldownTimer(TimeSpan.FromMinutes(4), clock: clock);
			Assert.AreEqual(TimeSpan.FromMinutes(6), running.RemainingTime);
			Assert.AreEqual(TimerState.Running, running.State);
			Assert.AreEqual(TimeBase.Wall, running.TimeBase);

			var events = new List<string>();
			using Timer over = TimeSpan.FromMinutes(10).CreateCooldownTimer(TimeSpan.FromMinutes(12),
				(left, _) => events.Add("tick " + left.ToMMSS()), () => events.Add("complete"), clock: clock);

			Assert.AreEqual(TimerState.Completed, over.State);
			CollectionAssert.AreEqual(new[] { "tick 00:00", "complete" }, events, "a cooldown that is over reaches its callbacks");
		}

		[Test]
		public void Factories_PassTheTickIntervalAndTheOwnerOn()
		{
			var clock = new ManualTimerClock();
			var ticks = new List<TimeSpan>();
			using var owner = new CancellationTokenSource();

			using Timer timer = ManualTimerClock.Origin.AddMinutes(1).CreateDailyResetTimer(TimeSpan.FromDays(1), (left, _) => ticks.Add(left),
				tickInterval: TimeSpan.FromSeconds(30), cancellationToken: owner.Token, clock: clock);

			clock.Set(29d);
			timer.Step();
			clock.Set(30d);
			timer.Step();
			CollectionAssert.AreEqual(new[] { TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30) }, ticks);

			owner.Cancel();
			Assert.AreEqual(TimerState.Idle, timer.State, "the owner's token stops it");
		}

		// ------------------------------------------------------------------ bindings

		[Test]
		public void Bindings_ShowTheTimer_NowAndOnEveryTick_UntilDisposed()
		{
			var clock = new ManualTimerClock();
			var root = new GameObject("Timer bindings");
			try
			{
				Text text = AddChild<Text>(root);
				Text destroyed = AddChild<Text>(root);
				Image fill = AddChild<Image>(root);
				Image empty = AddChild<Image>(root);
				Slider slider = AddChild<Slider>(root);
				slider.minValue = 0f;
				slider.maxValue = 10f;

				using var timer = new Timer(clock);
				timer.StartCountdown(TimeSpan.FromSeconds(90), tickInterval: TimeSpan.FromSeconds(1));

				IDisposable textBinding = timer.BindToText(text);
				timer.BindToText(destroyed, time => time.ToHHMMSS());
				timer.BindToFill(fill);
				timer.BindToFillInverse(empty);
				timer.BindToSlider(slider);

				Assert.AreEqual("01:30", text.text);
				Assert.AreEqual("00:01:30", destroyed.text);
				Assert.AreEqual(0f, fill.fillAmount);
				Assert.AreEqual(1f, empty.fillAmount);
				Assert.AreEqual(0f, slider.value);

				clock.Set(45d);
				timer.Step();

				Assert.AreEqual("00:45", text.text);
				Assert.AreEqual(0.5f, fill.fillAmount);
				Assert.AreEqual(0.5f, empty.fillAmount);
				Assert.AreEqual(5f, slider.value);

				textBinding.Dispose();
				textBinding.Dispose();
				Object.DestroyImmediate(destroyed.gameObject);
				clock.Set(60d);
				timer.Step();

				Assert.AreEqual("00:45", text.text, "a disposed binding stops");
				Assert.AreEqual(60f / 90f, fill.fillAmount, 1e-6f, "the others go on, past a destroyed target");
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

#if UGFW_TMP
		[Test]
		public void TextMeshProBinding_ShowsTheTimer()
		{
			var clock = new ManualTimerClock();
			var root = new GameObject("Timer binding");
			try
			{
				TextMeshProUGUI text = AddChild<TextMeshProUGUI>(root);

				using var timer = new Timer(clock);
				timer.StartCountUp(tickInterval: TimeSpan.FromSeconds(1));
				using IDisposable binding = timer.BindToText(text, time => time.ToHHMMSS());

				Assert.AreEqual("00:00:00", text.text);

				clock.Set(3725d);
				timer.Step();

				Assert.AreEqual("01:02:05", text.text);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
#endif

		private static T AddChild<T>(GameObject root) where T : Component
		{
			var child = new GameObject(typeof(T).Name, typeof(RectTransform));
			child.transform.SetParent(root.transform, false);
			return child.AddComponent<T>();
		}
	}
}
