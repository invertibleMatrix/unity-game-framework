using System;
using AK.Tests.Support;
using AK.Utilities;
using NUnit.Framework;

namespace AK.Tests.Utilities
{
	public class TimeFormatterTests
	{
		// ------------------------------------------------------------------ Digital

		[TestCase(0f, "00:00")]
		[TestCase(59.9f, "00:59")]
		[TestCase(90f, "01:30")]
		[TestCase(3599f, "59:59")]
		[TestCase(3661f, "01:01:01")]
		[TestCase(90_000f, "01:01:00:00")]
		[TestCase(-5f, "00:00", Description = "durations below zero format as zero")]
		public void Digital_ShowsMinutesThenHoursThenDays(float seconds, string expected)
		{
			Assert.AreEqual(expected, seconds.FormatDuration());
		}

		[TestCase(59.9f, TimeRounding.Ceil, "01:00")]
		[TestCase(59.4f, TimeRounding.Nearest, "00:59")]
		[TestCase(3599.5f, TimeRounding.Nearest, "01:00:00")]
		[TestCase(0.2f, TimeRounding.Ceil, "00:01")]
		[TestCase(3.0000002f, TimeRounding.Ceil, "00:03", Description = "a float is read to seven significant digits")]
		public void Digital_RoundsToTheSecond(float seconds, TimeRounding rounding, string expected)
		{
			Assert.AreEqual(expected, seconds.FormatDuration(r: rounding));
		}

		// ------------------------------------------------------------------ Stopwatch

		[TestCase(90.45f, TimeRounding.Floor, "01:30.45")]
		[TestCase(0f, TimeRounding.Floor, "00:00.00")]
		[TestCase(3665.25f, TimeRounding.Floor, "61:05.25")]
		[TestCase(59.999f, TimeRounding.Floor, "00:59.99")]
		[TestCase(59.999f, TimeRounding.Nearest, "01:00.00")]
		[TestCase(1.001f, TimeRounding.Ceil, "00:01.01")]
		public void Stopwatch_ShowsHundredths(float seconds, TimeRounding rounding, string expected)
		{
			Assert.AreEqual(expected, seconds.FormatDuration(TimeFormat.Stopwatch, r: rounding));
		}

		// ------------------------------------------------------------------ units

		[TestCase(5400f, 3, TimeRounding.Floor, "1h 30m")]
		[TestCase(3630f, 3, TimeRounding.Floor, "1h 30s", Description = "units at zero are left out")]
		[TestCase(3630f, 2, TimeRounding.Floor, "1h 30s")]
		[TestCase(3630f, 1, TimeRounding.Floor, "1h")]
		[TestCase(90f, 1, TimeRounding.Ceil, "2m", Description = "rounding applies to the smallest unit shown")]
		[TestCase(60f, 1, TimeRounding.Ceil, "1m")]
		[TestCase(59.5f, 1, TimeRounding.Ceil, "1m")]
		[TestCase(59f, 1, TimeRounding.Ceil, "59s")]
		[TestCase(0.3f, 1, TimeRounding.Ceil, "1s")]
		[TestCase(0.3f, 1, TimeRounding.Floor, "0s")]
		[TestCase(0f, 3, TimeRounding.Floor, "0s")]
		[TestCase(89.5f, 1, TimeRounding.Nearest, "1m")]
		[TestCase(90f, 1, TimeRounding.Nearest, "2m")]
		[TestCase(90_061f, 3, TimeRounding.Floor, "1d 1h 1m")]
		[TestCase(90_061f, 2, TimeRounding.Ceil, "1d 2h")]
		[TestCase(3599.5f, 2, TimeRounding.Ceil, "1h", Description = "a carry merges units")]
		[TestCase(-30f, 2, TimeRounding.Floor, "0s")]
		public void Abbreviated_ShowsTheLargestUnits(float seconds, int maxUnits, TimeRounding rounding, string expected)
		{
			Assert.AreEqual(expected, seconds.FormatDuration(TimeFormat.Abbreviated, maxUnits, rounding));
		}

		[TestCase(5400f, "1h30m")]
		[TestCase(3630f, "1h30s")]
		[TestCase(0f, "0s")]
		public void Compact_LeavesOutTheSpaces(float seconds, string expected)
		{
			Assert.AreEqual(expected, seconds.FormatDuration(TimeFormat.Compact));
		}

		[TestCase(5400f, "1 Hour 30 Minutes")]
		[TestCase(3661f, "1 Hour 1 Minute 1 Second")]
		[TestCase(0f, "0 Seconds")]
		[TestCase(2f, "2 Seconds")]
		[TestCase(86_400f, "1 Day")]
		[TestCase(172_800f, "2 Days")]
		public void Full_SpellsOutTheUnits(float seconds, string expected)
		{
			Assert.AreEqual(expected, seconds.FormatDuration(TimeFormat.Full));
		}

		[Test]
		public void IntAndDoubleSeconds_FormatTheSame()
		{
			Assert.AreEqual("01:30", 90.FormatDuration());
			Assert.AreEqual("01:30", 90d.FormatDuration());
			Assert.AreEqual("01:30.45", 90.45d.FormatDuration(TimeFormat.Stopwatch));
		}

		[TestCase(TimeFormat.Abbreviated), TestCase(TimeFormat.Compact), TestCase(TimeFormat.Full)]
		public void Units_Throw_WhenNoUnitMayShow(TimeFormat format)
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => 90f.FormatDuration(format, 0));
		}

		[TestCase(double.NaN), TestCase(double.PositiveInfinity), TestCase(1e300d)]
		public void FormatDuration_Throws_ForNaNAndDurationsATimeSpanCantHold(double seconds)
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => seconds.FormatDuration());
		}

		// ------------------------------------------------------------------ TryFormatDuration

		[TestCase(TimeFormat.Digital), TestCase(TimeFormat.Stopwatch), TestCase(TimeFormat.Abbreviated),
		 TestCase(TimeFormat.Compact), TestCase(TimeFormat.Full)]
		public void TryFormatDuration_WritesWhatFormatDurationReturns(TimeFormat format)
		{
			Span<char> text = stackalloc char[TimeFormatter.MaxDurationLength];

			foreach (float seconds in new[] { -5f, 0f, 0.2f, 59.9f, 90.45f, 3661f, 90_000f })
			{
				Assert.IsTrue(seconds.TryFormatDuration(text, out int written, format, 2, TimeRounding.Ceil));
				Assert.AreEqual(seconds.FormatDuration(format, 2, TimeRounding.Ceil), new string(text.Slice(0, written)));

				double asDouble = seconds;
				Assert.IsTrue(asDouble.TryFormatDuration(text, out written, format, 2, TimeRounding.Nearest));
				Assert.AreEqual(asDouble.FormatDuration(format, 2, TimeRounding.Nearest), new string(text.Slice(0, written)));
			}
		}

		[Test]
		public void TryFormatDuration_WritesNothing_WhenTheTextDoesNotFit()
		{
			Span<char> text = stackalloc char[4];

			Assert.IsFalse(90f.TryFormatDuration(text, out int written));
			Assert.AreEqual(0, written);
		}

		[Test]
		public void TryFormatDuration_AllocatesNothing()
		{
			char[] text = new char[TimeFormatter.MaxDurationLength];

			void FormatSome()
			{
				for (int i = 0; i < 100; i++)
				{
					(i * 37.3f).TryFormatDuration(text, out _, TimeFormat.Abbreviated, 2, TimeRounding.Ceil);
					(i * 37.3d).TryFormatDuration(text, out _);
				}
			}

			FormatSome();
			Assert.AreEqual(0, GcAllocations.Count(FormatSome));
		}

		[Test]
		public void FormatProgress_ShowsBothDurations()
		{
			Assert.AreEqual("01:30 / 02:00", 90f.FormatProgress(120f));
		}

		// ------------------------------------------------------------------ FormatDynamic

		[TestCase(75f, "01:15")]
		[TestCase(1.5f, "1.5")]
		[TestCase(12.34f, "12.3")]
		public void FormatDynamic_ShowsTenthsBelowAMinute(float seconds, string expected)
		{
			Assert.AreEqual(expected, seconds.FormatDynamic());
		}

		// ------------------------------------------------------------------ ParseTime

		[TestCase("01:30", 90d)]
		[TestCase("1:30", 90d)]
		[TestCase("90:00", 5400d)]
		[TestCase("1:30:00", 5400d)]
		[TestCase("01:01:00:00", 90_000d)]
		[TestCase("01:30.45", 90.45d)]
		[TestCase("90", 90d)]
		[TestCase("1.5", 1.5d)]
		[TestCase("1h 30m", 5400d)]
		[TestCase("1h30m", 5400d)]
		[TestCase("90m", 5400d)]
		[TestCase("1.5h", 5400d)]
		[TestCase("2d 3h", 183_600d)]
		[TestCase("1 Hour 30 Minutes", 5400d)]
		[TestCase("2 days 1 sec", 172_801d)]
		[TestCase("1H 30M", 5400d)]
		[TestCase(" 1m ", 60d)]
		[TestCase("-90", -90d)]
		[TestCase("-01:30", -90d)]
		public void ParseTime_ReadsClocksUnitsAndSeconds(string text, double expected)
		{
			Assert.AreEqual(expected, TimeFormatter.ParseTime(text), 1e-9);
		}

		[TestCase((string)null), TestCase(""), TestCase("abc"), TestCase("1:75"), TestCase("1:30:75"), TestCase("1:25:00:00")]
		[TestCase("30m 1h"), TestCase("1h 1h"), TestCase("1h foo"), TestCase("1,5h"), TestCase("1:"), TestCase(":30"), TestCase("1:2:3:4:5")]
		public void ParseTime_ReturnsZero_ForAnythingElse(string text)
		{
			Assert.AreEqual(0d, TimeFormatter.ParseTime(text));
		}

		[TestCase(0f), TestCase(59f), TestCase(90f), TestCase(3599f), TestCase(3661f), TestCase(90_000f), TestCase(1_234_567f)]
		public void ParseTime_ReadsBackWhatFormatDurationWrites(float seconds)
		{
			Assert.AreEqual((double)seconds, TimeFormatter.ParseTime(seconds.FormatDuration()));
			Assert.AreEqual((double)seconds, TimeFormatter.ParseTime(seconds.FormatDuration(TimeFormat.Abbreviated, 4)));
			Assert.AreEqual((double)seconds, TimeFormatter.ParseTime(seconds.FormatDuration(TimeFormat.Full, 4)));
			Assert.AreEqual((double)seconds, TimeFormatter.ParseTime(seconds.FormatDuration(TimeFormat.Stopwatch)), 1e-9);
		}

		// ------------------------------------------------------------------ culture and clocks

		[TestCase("de-DE"), TestCase("fr-FR"), TestCase("ar-SA")]
		public void TextIsTheSame_OnADeviceInAnotherCulture(string culture)
		{
			using (new CultureScope(culture))
			{
				Assert.AreEqual(5400d, TimeFormatter.ParseTime("1.5h"));
				Assert.AreEqual(90.5d, TimeFormatter.ParseTime("01:30.5"));
				Assert.AreEqual("1.5", 1.5f.FormatDynamic());
				Assert.AreEqual("01:30.45", 90.45f.FormatDuration(TimeFormat.Stopwatch));
			}
		}

		[Test]
		public void ToRelativeTime_ComparesUtcTimesWithUtcNow()
		{
			Assert.AreEqual("5m ago", DateTime.UtcNow.AddMinutes(-5.5).ToRelativeTime());
			Assert.AreEqual("2h ago", DateTime.Now.AddHours(-2.5).ToRelativeTime());
			Assert.AreEqual("Just now", DateTime.UtcNow.AddSeconds(-10).ToRelativeTime());
		}
	}
}
