using System;
using AK.Kernel.Formatting;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class DurationTextTests
	{
		private const NumberRounding Nearest = NumberRounding.Nearest;
		private const NumberRounding Down    = NumberRounding.Down;
		private const NumberRounding Up      = NumberRounding.Up;

		private const ClockLayout Adaptive     = ClockLayout.Adaptive;
		private const ClockLayout TotalMinutes = ClockLayout.TotalMinutes;
		private const ClockLayout TotalHours   = ClockLayout.TotalHours;
		private const ClockLayout DaysAndClock = ClockLayout.DaysAndClock;

		private const UnitStyle Abbreviated = UnitStyle.Abbreviated;
		private const UnitStyle Compact     = UnitStyle.Compact;
		private const UnitStyle Full        = UnitStyle.Full;

		/// <summary>The largest whole number of seconds a <see cref="TimeSpan"/> holds.</summary>
		private const long MaxWholeSeconds = 922_337_203_685L;

		private static TimeSpan Seconds(double seconds) => new((long)Math.Round(seconds * TimeSpan.TicksPerSecond));

		// ------------------------------------------------------------------ seconds

		[TestCase(0d, 0L)]
		[TestCase(1.5d, 15_000_000L)]
		[TestCase(-1.5d, -15_000_000L)]
		[TestCase(90.45d, 904_500_000L)]
		[TestCase(900_000_000_000d, 9_000_000_000_000_000_000L)]
		public void FromSeconds_TakesADoubleToTheNearestTick(double seconds, long ticks)
		{
			Assert.AreEqual(ticks, DurationText.FromSeconds(seconds).Ticks);
		}

		[TestCase(90.45f, 904_500_000L, Description = "the float is 90.4499969")]
		[TestCase(3.0000002f, 30_000_000L)]
		[TestCase(0.1f, 1_000_000L)]
		[TestCase(-90.45f, -904_500_000L)]
		public void FromSeconds_TakesAFloatToSevenDigits(float seconds, long ticks)
		{
			Assert.AreEqual(ticks, DurationText.FromSeconds(seconds).Ticks);
		}

		[TestCase(double.NaN), TestCase(double.PositiveInfinity), TestCase(double.NegativeInfinity), TestCase(1e12d), TestCase(-1e12d)]
		public void FromSeconds_Throws_ForNaNAndWhatATimeSpanCantHold(double seconds)
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => DurationText.FromSeconds(seconds));
			Assert.Throws<ArgumentOutOfRangeException>(() => DurationText.FromSeconds((float)seconds));
		}

		// ------------------------------------------------------------------ clocks

		[TestCase(0d, Adaptive, Down, "00:00")]
		[TestCase(59.9d, Adaptive, Down, "00:59")]
		[TestCase(59.9d, Adaptive, Up, "01:00", Description = "a carry reaches the minutes")]
		[TestCase(59.5d, Adaptive, Nearest, "01:00")]
		[TestCase(59.49d, Adaptive, Nearest, "00:59")]
		[TestCase(3599.5d, Adaptive, Nearest, "01:00:00", Description = "the layout follows the rounded time")]
		[TestCase(3661d, Adaptive, Down, "01:01:01")]
		[TestCase(86_399d, Adaptive, Down, "23:59:59")]
		[TestCase(86_399.5d, Adaptive, Up, "01:00:00:00")]
		[TestCase(90_061d, Adaptive, Down, "01:01:01:01")]
		[TestCase(8_640_000d, Adaptive, Down, "100:00:00:00")]
		[TestCase(3900d, TotalMinutes, Down, "65:00")]
		[TestCase(90_000d, TotalHours, Down, "25:00:00")]
		[TestCase(0d, TotalHours, Down, "00:00:00")]
		[TestCase(19_800d, DaysAndClock, Down, "05:30:00")]
		[TestCase(192_600d, DaysAndClock, Down, "2d 05:30:00")]
		[TestCase(86_400d, DaysAndClock, Down, "1d 00:00:00")]
		[TestCase(86_399.2d, DaysAndClock, Up, "1d 00:00:00")]
		public void Clock_RoundsToTheSecond(double seconds, ClockLayout layout, NumberRounding rounding, string expected)
		{
			Assert.AreEqual(expected, DurationText.FormatClock(Seconds(seconds), layout, rounding));
		}

		[TestCase(Adaptive, Down, "10675199:02:48:05")]
		[TestCase(Adaptive, Up, "10675199:02:48:05", Description = "nothing rounds past the largest TimeSpan")]
		[TestCase(TotalMinutes, Down, "15372286728:05")]
		[TestCase(TotalHours, Down, "256204778:48:05")]
		[TestCase(DaysAndClock, Down, "10675199d 02:48:05")]
		public void Clock_HoldsTheLargestTimeSpan(ClockLayout layout, NumberRounding rounding, string expected)
		{
			Assert.AreEqual(expected, DurationText.FormatClock(TimeSpan.MaxValue, layout, rounding));
		}

		// ------------------------------------------------------------------ stopwatch

		[TestCase(0d, Down, "00:00.00")]
		[TestCase(90.45d, Down, "01:30.45")]
		[TestCase(3665.25d, Down, "61:05.25")]
		[TestCase(59.999d, Down, "00:59.99")]
		[TestCase(59.999d, Nearest, "01:00.00")]
		[TestCase(59.995d, Nearest, "01:00.00")]
		[TestCase(59.9949d, Nearest, "00:59.99")]
		[TestCase(0.001d, Up, "00:00.01")]
		[TestCase(0.001d, Down, "00:00.00")]
		public void Stopwatch_RoundsToTheHundredth(double seconds, NumberRounding rounding, string expected)
		{
			Assert.AreEqual(expected, DurationText.FormatStopwatch(Seconds(seconds), rounding));
		}

		[Test]
		public void Stopwatch_HoldsTheLargestTimeSpan()
		{
			Assert.AreEqual("15372286728:05.47", DurationText.FormatStopwatch(TimeSpan.MaxValue));
		}

		// ------------------------------------------------------------------ units

		[TestCase(5400d, Abbreviated, 3, Down, "1h 30m")]
		[TestCase(5400d, Compact, 3, Down, "1h30m")]
		[TestCase(5400d, Full, 3, Down, "1 Hour 30 Minutes")]
		[TestCase(3630d, Abbreviated, 2, Down, "1h 30s", Description = "units at zero are left out")]
		[TestCase(3630d, Abbreviated, 1, Down, "1h")]
		[TestCase(3661d, Full, 3, Down, "1 Hour 1 Minute 1 Second")]
		[TestCase(0d, Abbreviated, 1, Down, "0s")]
		[TestCase(0d, Full, 1, Down, "0 Seconds")]
		[TestCase(1d, Full, 1, Down, "1 Second")]
		[TestCase(172_800d, Full, 2, Down, "2 Days")]
		[TestCase(0.4d, Abbreviated, 1, Down, "0s")]
		[TestCase(0.4d, Abbreviated, 1, Up, "1s", Description = "a part of a second counts as seconds")]
		[TestCase(90d, Abbreviated, 1, Up, "2m", Description = "rounding is to the smallest unit shown")]
		[TestCase(90d, Abbreviated, 1, Nearest, "2m")]
		[TestCase(89.9d, Abbreviated, 1, Nearest, "1m")]
		[TestCase(59.5d, Abbreviated, 1, Up, "1m")]
		[TestCase(3599.5d, Abbreviated, 2, Up, "1h", Description = "a carry merges units")]
		[TestCase(90_061d, Abbreviated, 2, Up, "1d 2h")]
		[TestCase(90_061d, Compact, 4, Down, "1d1h1m1s")]
		[TestCase(259_259d, Abbreviated, 2, Down, "3d 59s")]
		public void Units_ShowTheLargestUnitsThatArentZero(double seconds, UnitStyle style, int maxUnits, NumberRounding rounding, string expected)
		{
			Assert.AreEqual(expected, DurationText.FormatUnits(Seconds(seconds), style, maxUnits, rounding));
		}

		[Test]
		public void Units_HoldTheLargestTimeSpan()
		{
			string text = DurationText.FormatUnits(TimeSpan.MaxValue, Full, 4);

			Assert.AreEqual("10675199 Days 2 Hours 48 Minutes 5 Seconds", text);
			Assert.LessOrEqual(text.Length, DurationText.MaxLength);
		}

		// ------------------------------------------------------------------ contracts

		[Test]
		public void Formatting_Throws_ForANegativeDuration()
		{
			TimeSpan negative = TimeSpan.FromTicks(-1);

			Assert.Throws<ArgumentOutOfRangeException>(() => DurationText.FormatClock(negative, Adaptive));
			Assert.Throws<ArgumentOutOfRangeException>(() => DurationText.FormatStopwatch(negative));
			Assert.Throws<ArgumentOutOfRangeException>(() => DurationText.FormatUnits(negative, Abbreviated, 1));
		}

		[Test]
		public void Formatting_Throws_ForArgumentsOutOfRange()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => DurationText.FormatUnits(TimeSpan.Zero, Abbreviated, 0));
			Assert.Throws<ArgumentOutOfRangeException>(() => DurationText.FormatUnits(TimeSpan.Zero, (UnitStyle)3, 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => DurationText.FormatClock(TimeSpan.Zero, (ClockLayout)4));
			Assert.Throws<ArgumentOutOfRangeException>(() => DurationText.FormatClock(Seconds(1.5d), Adaptive, (NumberRounding)3));
		}

		[Test]
		public void TryFormat_FailsWithoutWriting_WhenTheDestinationIsShort()
		{
			Span<char> text = stackalloc char[5];
			text.Fill('#');

			Assert.IsFalse(DurationText.TryFormatClock(Seconds(90d), Adaptive, Down, text.Slice(0, 4), out int written));
			Assert.AreEqual(0, written);
			Assert.AreEqual("#####", new string(text));

			Assert.IsTrue(DurationText.TryFormatClock(Seconds(90d), Adaptive, Down, text, out written));
			Assert.AreEqual("01:30", new string(text.Slice(0, written)));
		}

		// ------------------------------------------------------------------ parsing

		[TestCase("01:30", 900_000_000L)]
		[TestCase("1:30", 900_000_000L)]
		[TestCase("0:05", 50_000_000L)]
		[TestCase("90:00", 54_000_000_000L)]
		[TestCase("1:30:00", 54_000_000_000L)]
		[TestCase("100:00:00", 3_600_000_000_000L)]
		[TestCase("01:01:00:00", 900_000_000_000L)]
		[TestCase("1:23:59:59", 1_727_990_000_000L)]
		[TestCase("2d 05:30:00", 1_926_000_000_000L)]
		[TestCase("1D 00:00:00", 864_000_000_000L)]
		[TestCase("01:30.45", 904_500_000L)]
		[TestCase("01:30.4567891", 904_567_891L)]
		[TestCase("01:30.45678919", 904_567_891L, Description = "a part of a tick is dropped")]
		[TestCase("61:05.25", 36_652_500_000L)]
		[TestCase("-01:30", -900_000_000L)]
		[TestCase("+01:30", 900_000_000L)]
		[TestCase(" 01:30 ", 900_000_000L)]
		public void Parse_ReadsClocks(string text, long ticks)
		{
			Assert.IsTrue(DurationText.TryParse(text, out TimeSpan duration));
			Assert.AreEqual(ticks, duration.Ticks);
		}

		[TestCase("1h 30m", 54_000_000_000L)]
		[TestCase("1h30m", 54_000_000_000L)]
		[TestCase("90m", 54_000_000_000L)]
		[TestCase("1.5h", 54_000_000_000L)]
		[TestCase(".5h", 18_000_000_000L)]
		[TestCase("2d 3h", 1_836_000_000_000L)]
		[TestCase("1 Hour 30 Minutes", 54_000_000_000L)]
		[TestCase("2 days 1 sec", 1_728_010_000_000L)]
		[TestCase("1H 30M", 54_000_000_000L)]
		[TestCase("1hr 5mins", 39_000_000_000L)]
		[TestCase("1d1h1m1s", 900_610_000_000L)]
		[TestCase("0.0000001s", 1L)]
		[TestCase("1.123456789d", 970_666_665_696L, Description = "fractions are exact to the tick")]
		[TestCase("10675199d", 9_223_371_936_000_000_000L)]
		[TestCase("90", 900_000_000L)]
		[TestCase("1.5", 15_000_000L)]
		[TestCase("-90", -900_000_000L)]
		[TestCase("0", 0L)]
		public void Parse_ReadsUnitsAndSeconds(string text, long ticks)
		{
			Assert.IsTrue(DurationText.TryParse(text, out TimeSpan duration));
			Assert.AreEqual(ticks, duration.Ticks);
		}

		[TestCase(""), TestCase("   "), TestCase("-"), TestCase("+"), TestCase("- 90"), TestCase("abc"), TestCase(".")]
		[TestCase("1:75"), TestCase("1:30:75"), TestCase("1:25:00:00"), TestCase("1:2"), TestCase("1:002"), TestCase("1:"), TestCase(":30")]
		[TestCase("1::30"), TestCase("1:2:3:4:5"), TestCase("01:30."), TestCase("01:30.4x"), TestCase("01.5:30")]
		[TestCase("2d05:30:00"), TestCase("2d 5:30:00"), TestCase("2d 24:00:00"), TestCase("2d 05:30")]
		[TestCase("30m 1h"), TestCase("1h 1h"), TestCase("1h foo"), TestCase("1,5h"), TestCase("1h30"), TestCase("1 2"), TestCase("5 m s")]
		[TestCase("1x"), TestCase("1.5.5"), TestCase("1.5 h 30"), TestCase("99999999999999999999"), TestCase("10675200d")]
		public void Parse_RefusesAnythingElse(string text)
		{
			Assert.IsFalse(DurationText.TryParse(text, out TimeSpan duration));
			Assert.AreEqual(TimeSpan.Zero, duration);
		}

		[TestCase(0L), TestCase(59L), TestCase(90L), TestCase(3599L), TestCase(3661L), TestCase(90_000L), TestCase(1_234_567L), TestCase(MaxWholeSeconds)]
		public void Parse_ReadsBackEveryFormat(long seconds)
		{
			var duration = new TimeSpan(seconds * TimeSpan.TicksPerSecond);

			foreach (ClockLayout layout in new[] { Adaptive, TotalMinutes, TotalHours, DaysAndClock })
			{
				AssertReadsBack(duration, DurationText.FormatClock(duration, layout));
			}

			foreach (UnitStyle style in new[] { Abbreviated, Compact, Full })
			{
				AssertReadsBack(duration, DurationText.FormatUnits(duration, style, 4));
			}

			TimeSpan withHundredths = duration + TimeSpan.FromMilliseconds(250);
			AssertReadsBack(withHundredths, DurationText.FormatStopwatch(withHundredths));
		}

		private static void AssertReadsBack(TimeSpan expected, string text)
		{
			Assert.IsTrue(DurationText.TryParse(text, out TimeSpan read), text);
			Assert.AreEqual(expected, read, text);
		}

		// ------------------------------------------------------------------ allocations

		[Test]
		public void FormattingIntoASpanAndParsing_DoNotAllocate()
		{
			Action exercise = Exercise;

			// The first run JIT-compiles the body, which allocates; the second is measured.
			GcAllocations.Count(exercise);
			int allocations = GcAllocations.Count(exercise);

			Assert.AreEqual(0, allocations);
		}

		private static void Exercise()
		{
			Span<char> text = stackalloc char[DurationText.MaxLength];
			for (int i = 0; i < 100; i++)
			{
				TimeSpan duration = DurationText.FromSeconds(3661.25f * i);
				DurationText.TryFormatClock(duration, Adaptive, Up, text, out _);
				DurationText.TryFormatStopwatch(duration, Nearest, text, out _);
				DurationText.TryFormatUnits(duration, Full, 4, Down, text, out _);
				DurationText.TryParse("2d 05:30:00".AsSpan(), out _);
				DurationText.TryParse("1 Hour 30.5 Minutes".AsSpan(), out _);
			}
		}
	}
}
