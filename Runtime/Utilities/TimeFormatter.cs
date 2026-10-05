using System;
using System.Globalization;
using AK.Kernel.Formatting;

namespace AK.Utilities
{
	/// <summary>How <see cref="TimeFormatter"/> writes a duration.</summary>
	public enum TimeFormat
	{
		/// <summary>A clock: "01:30" below an hour, "01:01:30" below a day, then "01:01:01:30" with days.</summary>
		Digital = 0,

		/// <summary>Units with spaces: "1h 30m".</summary>
		Abbreviated = 1,

		/// <summary>Units in words: "1 Hour 30 Minutes".</summary>
		Full = 2,

		/// <summary>Units without spaces: "1h30m".</summary>
		Compact = 3,

		/// <summary>Total minutes, seconds and hundredths: "01:30.45".</summary>
		Stopwatch = 4,
	}

	/// <summary>How <see cref="TimeFormatter"/> rounds to the smallest unit it shows.</summary>
	public enum TimeRounding
	{
		/// <summary>Down, so the text never shows more than the time: for time played.</summary>
		Floor = 0,

		/// <summary>Up, so the text never shows less: for cooldowns, which then show zero only at their end.</summary>
		Ceil = 1,

		/// <summary>To the nearest; halves go up.</summary>
		Nearest = 2,
	}

	/// <summary>
	/// Durations in seconds as text for UI, and back: a facade over the kernel's
	/// <see cref="DurationText"/>.
	///
	/// <para>A duration is rounded to the smallest unit shown before it is split into units, so
	/// 59.5 seconds rounded up is "01:00", and a duration below zero shows as zero. A float is
	/// read to seven significant digits, so 90.45f is 90.45 seconds. The text is the same on
	/// every device, and <see cref="ParseTime"/> reads back whatever the formats write.</para>
	/// </summary>
	public static class TimeFormatter
	{
		/// <summary>The longest text <see cref="TryFormatDuration(float, Span{char}, out int, TimeFormat, int, TimeRounding)"/> writes.</summary>
		public const int MaxDurationLength = DurationText.MaxLength;

		// ------------------------------------------------------------------ durations

		/// <summary>Formats a number of seconds.</summary>
		/// <param name="max">For the unit formats, the most units shown.</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The seconds are NaN or more than a <see cref="TimeSpan"/> holds, or <paramref name="max"/> is below 1 for a unit format.
		/// </exception>
		public static string FormatDuration(this float s, TimeFormat f = TimeFormat.Digital, int max = 3, TimeRounding r = TimeRounding.Floor) =>
			Format(s <= 0f ? TimeSpan.Zero : DurationText.FromSeconds(s), f, max, r);

		/// <inheritdoc cref="FormatDuration(float, TimeFormat, int, TimeRounding)"/>
		public static string FormatDuration(this int s, TimeFormat f = TimeFormat.Digital, int max = 3, TimeRounding r = TimeRounding.Floor) =>
			Format(s <= 0 ? TimeSpan.Zero : new TimeSpan(s * TimeSpan.TicksPerSecond), f, max, r);

		/// <inheritdoc cref="FormatDuration(float, TimeFormat, int, TimeRounding)"/>
		public static string FormatDuration(this double s, TimeFormat f = TimeFormat.Digital, int max = 3, TimeRounding r = TimeRounding.Floor) =>
			Format(s <= 0d ? TimeSpan.Zero : DurationText.FromSeconds(s), f, max, r);

		/// <summary>
		/// <see cref="FormatDuration(float, TimeFormat, int, TimeRounding)"/> into
		/// <paramref name="destination"/>, allocating nothing: for text redrawn every frame. False,
		/// with nothing written, when the text doesn't fit; <see cref="MaxDurationLength"/>
		/// characters always hold it.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The seconds are NaN or more than a <see cref="TimeSpan"/> holds, or <paramref name="max"/> is below 1 for a unit format.
		/// </exception>
		public static bool TryFormatDuration(this float s, Span<char> destination, out int charsWritten, TimeFormat f = TimeFormat.Digital,
		                                     int max = 3, TimeRounding r = TimeRounding.Floor) =>
			TryFormat(s <= 0f ? TimeSpan.Zero : DurationText.FromSeconds(s), f, max, r, destination, out charsWritten);

		/// <inheritdoc cref="TryFormatDuration(float, Span{char}, out int, TimeFormat, int, TimeRounding)"/>
		public static bool TryFormatDuration(this double s, Span<char> destination, out int charsWritten, TimeFormat f = TimeFormat.Digital,
		                                     int max = 3, TimeRounding r = TimeRounding.Floor) =>
			TryFormat(s <= 0d ? TimeSpan.Zero : DurationText.FromSeconds(s), f, max, r, destination, out charsWritten);

		/// <summary>Formats "current / total", such as "01:30 / 02:00".</summary>
		public static string FormatProgress(this float current, float total, TimeFormat f = TimeFormat.Digital) =>
			current.FormatDuration(f) + " / " + total.FormatDuration(f);

		/// <summary>Seconds as a clock from a minute on, rounded down, and below a minute with one decimal: "01:15", "12.3".</summary>
		public static string FormatDynamic(this float s) =>
			s >= 60f ? s.FormatDuration() : s.ToString("0.0", CultureInfo.InvariantCulture);

		/// <summary>
		/// Reads a number of seconds: a clock such as "01:30" or "1:30:00", units such as "1h 30m",
		/// "1.5h" or "2 days 1 sec", or seconds alone such as "90". Returns 0 for anything else.
		/// <see cref="DurationText"/> has the rules.
		/// </summary>
		public static double ParseTime(string s) =>
			DurationText.TryParse(s, out TimeSpan duration) ? duration.TotalSeconds : 0d;

		// ------------------------------------------------------------------ dates

		/// <summary>The local time <paramref name="s"/> seconds from now, in the device's format: "5:30 PM" by default.</summary>
		public static string GetArrivalTime(this float s, string fmt = "t") => DateTime.Now.AddSeconds(s).ToString(fmt);

		/// <summary>When <paramref name="s"/> seconds from now is, in local time and the device's format: "Today at 5:30 PM", "Tomorrow at 9:00 AM" or "Oct 07 at 9:00 AM".</summary>
		public static string GetArrivalDescription(this float s)
		{
			DateTime now = DateTime.Now;
			DateTime arrival = now.AddSeconds(s);

			if (arrival.Date == now.Date) return $"Today at {arrival:t}";
			if (arrival.Date == now.Date.AddDays(1)) return $"Tomorrow at {arrival:t}";
			return $"{arrival:MMM dd} at {arrival:t}";
		}

		/// <summary>
		/// How long ago a time was, rounded down: "Just now" under a minute, then "5m ago", "2h ago"
		/// and "3d ago". A time ahead is "Just now". A local time is converted to UTC; a time of
		/// unspecified kind is taken as UTC.
		/// </summary>
		public static string ToRelativeTime(this DateTime dt)
		{
			DateTime utc = dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : dt;
			double seconds = (DateTime.UtcNow - utc).TotalSeconds;

			if (seconds < 60d) return "Just now";
			if (seconds < 3600d) return Ago(seconds / 60d, "m ago");
			if (seconds < 86400d) return Ago(seconds / 3600d, "h ago");
			return Ago(seconds / 86400d, "d ago");
		}

		// ------------------------------------------------------------------ internals

		/// <summary>The kernel's rounding for a <see cref="TimeRounding"/>.</summary>
		internal static NumberRounding ToNumberRounding(TimeRounding rounding)
		{
			switch (rounding)
			{
				case TimeRounding.Floor:   return NumberRounding.Down;
				case TimeRounding.Ceil:    return NumberRounding.Up;
				case TimeRounding.Nearest: return NumberRounding.Nearest;
				default:                   throw new ArgumentOutOfRangeException(nameof(rounding), rounding, null);
			}
		}

		private static string Format(TimeSpan duration, TimeFormat format, int maxUnits, TimeRounding rounding)
		{
			Span<char> text = stackalloc char[MaxDurationLength];
			TryFormat(duration, format, maxUnits, rounding, text, out int written);
			return new string(text.Slice(0, written));
		}

		private static bool TryFormat(TimeSpan duration, TimeFormat format, int maxUnits, TimeRounding rounding, Span<char> destination,
		                              out int charsWritten)
		{
			NumberRounding numberRounding = ToNumberRounding(rounding);
			switch (format)
			{
				case TimeFormat.Digital:
					return DurationText.TryFormatClock(duration, ClockLayout.Adaptive, numberRounding, destination, out charsWritten);
				case TimeFormat.Stopwatch:
					return DurationText.TryFormatStopwatch(duration, numberRounding, destination, out charsWritten);
				case TimeFormat.Abbreviated:
					return DurationText.TryFormatUnits(duration, UnitStyle.Abbreviated, maxUnits, numberRounding, destination, out charsWritten);
				case TimeFormat.Compact:
					return DurationText.TryFormatUnits(duration, UnitStyle.Compact, maxUnits, numberRounding, destination, out charsWritten);
				case TimeFormat.Full:
					return DurationText.TryFormatUnits(duration, UnitStyle.Full, maxUnits, numberRounding, destination, out charsWritten);
				default:
					throw new ArgumentOutOfRangeException(nameof(format), format, null);
			}
		}

		private static string Ago(double amount, string unit) =>
			((long)amount).ToString(CultureInfo.InvariantCulture) + unit;
	}
}
