using System;
using System.Globalization;

namespace AK.Kernel.Formatting
{
	/// <summary>How <see cref="DurationText"/> lays out a duration as a clock.</summary>
	public enum ClockLayout : byte
	{
		/// <summary>"05:30" below an hour, "01:05:30" below a day, then days as a fourth part: "02:01:05:30".</summary>
		Adaptive = 0,

		/// <summary>Total minutes and seconds: "65:30".</summary>
		TotalMinutes = 1,

		/// <summary>Total hours, minutes and seconds: "25:05:30".</summary>
		TotalHours = 2,

		/// <summary>Days before the clock from a day on, "2d 01:05:30"; "01:05:30" below a day.</summary>
		DaysAndClock = 3,
	}

	/// <summary>How <see cref="DurationText"/> writes units.</summary>
	public enum UnitStyle : byte
	{
		/// <summary>"1h 30m".</summary>
		Abbreviated = 0,

		/// <summary>"1h30m".</summary>
		Compact = 1,

		/// <summary>"1 Hour 30 Minutes".</summary>
		Full = 2,
	}

	/// <summary>
	/// Writes durations as clocks, stopwatches and units, and reads them back.
	///
	/// <para><b>Formatting</b> takes a duration of zero or more; a caller that shows a negative
	/// duration writes the sign itself. The duration is rounded to the smallest unit shown before
	/// it is split into units, so a carry reaches the larger units: 59.5 seconds rounded up is
	/// "01:00". The text is the same on every device.</para>
	///
	/// <para><b>Parsing</b> reads everything the formats write:</para>
	/// <list type="bullet">
	/// <item>Clocks: "mm:ss", "hh:mm:ss", "dd:hh:mm:ss" and "2d 05:30:00". The first part takes
	/// any number of digits; the others take two, below 60, or below 24 for hours after days.
	/// Seconds may have a fraction: "01:30.45".</item>
	/// <item>Units: numbers with d, h, m and s or their words, in any case, with or without spaces:
	/// "1h 30m", "1.5h", "2 days 1 sec". Each unit appears once, largest first.</item>
	/// <item>A number of seconds alone: "90", "1.5".</item>
	/// </list>
	/// <para>A leading sign applies to the whole duration. Numbers take a point, on every device.
	/// Parts of a tick are dropped.</para>
	///
	/// <para>Formatting into a span and parsing allocate nothing.</para>
	/// </summary>
	public static class DurationText
	{
		/// <summary>The longest text a format writes, which is for <see cref="TimeSpan.MaxValue"/>.</summary>
		public const int MaxLength = 64;

		private const int HourUnit   = 1;
		private const int SecondUnit = 3;

		private const long   TicksPerHundredth = TimeSpan.TicksPerMillisecond * 10;
		private const double TicksLimit        = 9_223_372_036_854_775_808d; // 2^63
		private const string UnitLetters       = "dhms";

		// Day, hour, minute and second, in that order everywhere.
		private static readonly long[]   UnitTicks = { TimeSpan.TicksPerDay, TimeSpan.TicksPerHour, TimeSpan.TicksPerMinute, TimeSpan.TicksPerSecond };
		private static readonly string[] UnitWords = { " Day", " Hour", " Minute", " Second" };

		private static readonly string[][] UnitNames =
		{
			new[] { "d", "day", "days" },
			new[] { "h", "hr", "hrs", "hour", "hours" },
			new[] { "m", "min", "mins", "minute", "minutes" },
			new[] { "s", "sec", "secs", "second", "seconds" },
		};

		// ------------------------------------------------------------------ seconds

		/// <summary>The duration of <paramref name="seconds"/>, to the nearest tick; halves go away from zero.</summary>
		/// <exception cref="ArgumentOutOfRangeException">NaN, or more seconds than a <see cref="TimeSpan"/> holds either way.</exception>
		public static TimeSpan FromSeconds(double seconds)
		{
			double ticks = Math.Round(seconds * TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero);

			// Every double below 2^63 in size converts to a long exactly. NaN fails the test too.
			if (!(Math.Abs(ticks) < TicksLimit))
			{
				throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "A duration is a number of seconds that a TimeSpan holds.");
			}

			return new TimeSpan((long)ticks);
		}

		/// <summary>
		/// The duration of <paramref name="seconds"/>, read to seven significant digits, which a
		/// float always holds: 90.45f is 90.45 seconds, though the float itself is 90.4499969.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">NaN, or more seconds than a <see cref="TimeSpan"/> holds either way.</exception>
		public static TimeSpan FromSeconds(float seconds)
		{
			if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return FromSeconds((double)seconds);

			// "E6" writes seven significant digits, correctly rounded.
			Span<char> text = stackalloc char[16];
			((double)seconds).TryFormat(text, out int written, "E6", CultureInfo.InvariantCulture);
			double.TryParse(text.Slice(0, written), NumberStyles.Float, CultureInfo.InvariantCulture, out double decimalSeconds);
			return FromSeconds(decimalSeconds);
		}

		// ------------------------------------------------------------------ formatting

		/// <summary>Writes a clock, rounded to the second.</summary>
		/// <exception cref="ArgumentOutOfRangeException">The duration is negative, or an enum is out of range.</exception>
		public static bool TryFormatClock(TimeSpan duration, ClockLayout layout, NumberRounding rounding, Span<char> destination, out int charsWritten)
		{
			long total   = RoundTo(TicksOf(duration), TimeSpan.TicksPerSecond, rounding) / TimeSpan.TicksPerSecond;
			long minutes = total / 60;
			long hours   = total / 3600;
			long days    = total / 86400;

			Span<char> text = stackalloc char[MaxLength];
			int at = 0;

			switch (layout)
			{
				case ClockLayout.Adaptive when total < 3600:
				case ClockLayout.TotalMinutes:
					WriteWhole(minutes, 2, text, ref at);
					break;
				case ClockLayout.Adaptive when total < 86400:
				case ClockLayout.TotalHours:
					WriteWhole(hours, 2, text, ref at);
					WriteClockPart(minutes % 60, text, ref at);
					break;
				case ClockLayout.Adaptive:
					WriteWhole(days, 2, text, ref at);
					WriteClockPart(hours % 24, text, ref at);
					WriteClockPart(minutes % 60, text, ref at);
					break;
				case ClockLayout.DaysAndClock:
					if (days > 0)
					{
						WriteWhole(days, 1, text, ref at);
						text[at++] = 'd';
						text[at++] = ' ';
					}

					WriteWhole(hours % 24, 2, text, ref at);
					WriteClockPart(minutes % 60, text, ref at);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(layout), layout, null);
			}

			WriteClockPart(total % 60, text, ref at);
			return TryCopy(text.Slice(0, at), destination, out charsWritten);
		}

		/// <summary>Writes total minutes, seconds and hundredths, "61:05.25", rounded to the hundredth.</summary>
		/// <exception cref="ArgumentOutOfRangeException">The duration is negative, or the rounding is out of range.</exception>
		public static bool TryFormatStopwatch(TimeSpan duration, NumberRounding rounding, Span<char> destination, out int charsWritten)
		{
			long hundredths = RoundTo(TicksOf(duration), TicksPerHundredth, rounding) / TicksPerHundredth;

			Span<char> text = stackalloc char[MaxLength];
			int at = 0;
			WriteWhole(hundredths / 6000, 2, text, ref at);
			WriteClockPart(hundredths / 100 % 60, text, ref at);
			text[at++] = '.';
			WriteWhole(hundredths % 100, 2, text, ref at);
			return TryCopy(text.Slice(0, at), destination, out charsWritten);
		}

		/// <summary>
		/// Writes the largest units of a duration, at most <paramref name="maxUnits"/> of them, and
		/// leaves out units at zero. The duration is rounded to the smallest unit shown: 1 minute
		/// 30 seconds rounded up to one unit is "2m". A duration that rounds to zero shows zero
		/// seconds.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The duration is negative, <paramref name="maxUnits"/> is below 1, or an enum is out of range.
		/// </exception>
		public static bool TryFormatUnits(TimeSpan duration, UnitStyle style, int maxUnits, NumberRounding rounding, Span<char> destination, out int charsWritten)
		{
			if (maxUnits < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(maxUnits), maxUnits, "At least one unit is shown.");
			}

			if (style > UnitStyle.Full)
			{
				throw new ArgumentOutOfRangeException(nameof(style), style, null);
			}

			long ticks = TicksOf(duration);

			// The smallest unit shown is the last of the first maxUnits units that aren't zero. A
			// part of a second counts toward the seconds.
			int smallest = SecondUnit;
			long rest = ticks;
			for (int unit = 0, shown = 0; unit < UnitTicks.Length; unit++)
			{
				long amount = rest / UnitTicks[unit];
				rest -= amount * UnitTicks[unit];

				bool present = amount > 0 || (unit == SecondUnit && rest > 0);
				if (present && ++shown == maxUnits)
				{
					smallest = unit;
					break;
				}
			}

			rest = RoundTo(ticks, UnitTicks[smallest], rounding);

			Span<char> text = stackalloc char[MaxLength];
			int at = 0;
			for (int unit = 0; unit < UnitTicks.Length; unit++)
			{
				long amount = rest / UnitTicks[unit];
				rest -= amount * UnitTicks[unit];
				if (amount > 0) WriteUnit(amount, unit, style, text, ref at);
			}

			if (at == 0) WriteUnit(0, SecondUnit, style, text, ref at);
			return TryCopy(text.Slice(0, at), destination, out charsWritten);
		}

		/// <inheritdoc cref="TryFormatClock"/>
		public static string FormatClock(TimeSpan duration, ClockLayout layout, NumberRounding rounding = NumberRounding.Down)
		{
			Span<char> text = stackalloc char[MaxLength];
			TryFormatClock(duration, layout, rounding, text, out int written);
			return new string(text.Slice(0, written));
		}

		/// <inheritdoc cref="TryFormatStopwatch"/>
		public static string FormatStopwatch(TimeSpan duration, NumberRounding rounding = NumberRounding.Down)
		{
			Span<char> text = stackalloc char[MaxLength];
			TryFormatStopwatch(duration, rounding, text, out int written);
			return new string(text.Slice(0, written));
		}

		/// <inheritdoc cref="TryFormatUnits"/>
		public static string FormatUnits(TimeSpan duration, UnitStyle style, int maxUnits, NumberRounding rounding = NumberRounding.Down)
		{
			Span<char> text = stackalloc char[MaxLength];
			TryFormatUnits(duration, style, maxUnits, rounding, text, out int written);
			return new string(text.Slice(0, written));
		}

		/// <summary>Rounds zero or more ticks to a multiple of <paramref name="unit"/>. Past <see cref="TimeSpan.MaxValue"/> it rounds down.</summary>
		private static long RoundTo(long ticks, long unit, NumberRounding rounding)
		{
			if (rounding > NumberRounding.Up)
			{
				throw new ArgumentOutOfRangeException(nameof(rounding), rounding, null);
			}

			long rest = ticks % unit;
			if (rest == 0) return ticks;

			long down = ticks - rest;
			bool up = rounding switch
			{
				NumberRounding.Up   => true,
				NumberRounding.Down => false,
				_                   => rest >= unit - rest,
			};

			return up && down <= long.MaxValue - unit ? down + unit : down;
		}

		private static long TicksOf(TimeSpan duration)
		{
			if (duration.Ticks < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(duration), duration, "A duration is formatted without a sign; the caller writes it.");
			}

			return duration.Ticks;
		}

		private static void WriteUnit(long amount, int unit, UnitStyle style, Span<char> text, ref int at)
		{
			if (at > 0 && style != UnitStyle.Compact) text[at++] = ' ';

			WriteWhole(amount, 1, text, ref at);
			if (style != UnitStyle.Full)
			{
				text[at++] = UnitLetters[unit];
				return;
			}

			UnitWords[unit].AsSpan().CopyTo(text.Slice(at));
			at += UnitWords[unit].Length;
			if (amount != 1) text[at++] = 's';
		}

		private static void WriteClockPart(long value, Span<char> text, ref int at)
		{
			text[at++] = ':';
			WriteWhole(value, 2, text, ref at);
		}

		/// <summary>Writes a whole number of zero or more, with at least <paramref name="minDigits"/> digits.</summary>
		private static void WriteWhole(long value, int minDigits, Span<char> text, ref int at)
		{
			int length = 1;
			for (long rest = value / 10; rest > 0; rest /= 10) length++;

			length = Math.Max(length, minDigits);
			for (int i = at + length - 1; i >= at; i--)
			{
				text[i] = (char)('0' + value % 10);
				value /= 10;
			}

			at += length;
		}

		private static bool TryCopy(ReadOnlySpan<char> text, Span<char> destination, out int charsWritten)
		{
			charsWritten = text.TryCopyTo(destination) ? text.Length : 0;
			return charsWritten == text.Length;
		}

		// ------------------------------------------------------------------ parsing

		/// <summary>Reads a clock, units or seconds alone, as described on <see cref="DurationText"/>.</summary>
		public static bool TryParse(ReadOnlySpan<char> text, out TimeSpan duration)
		{
			duration = TimeSpan.Zero;
			text = Trim(text);

			bool negative = false;
			if (!text.IsEmpty && (text[0] == '-' || text[0] == '+'))
			{
				negative = text[0] == '-';
				text = text.Slice(1);
			}

			// The sign sits on the number: "- 90" is not a duration.
			if (text.IsEmpty || IsWhitespace(text[0])) return false;

			long ticks;
			bool read = text.IndexOf(':') >= 0 ? TryParseClock(text, out ticks) : TryParseUnits(text, out ticks);
			if (!read) return false;

			duration = new TimeSpan(negative ? -ticks : ticks);
			return true;
		}

		private static bool TryParseClock(ReadOnlySpan<char> text, out long ticks)
		{
			ticks = 0;

			// Whole days and a d before a clock of hours, minutes and seconds: "2d 05:30:00".
			int digits = 0;
			while (digits < text.Length && IsDigit(text[digits])) digits++;

			if (digits > 0 && digits < text.Length && (text[digits] | 0x20) == 'd')
			{
				int clock = SkipWhitespace(text, digits + 1);
				return clock > digits + 1
				       && TryReadWhole(text.Slice(0, digits), out long days)
				       && TryAdd(ref ticks, days, TimeSpan.TicksPerDay)
				       && TryReadClock(text.Slice(clock), true, ref ticks);
			}

			return TryReadClock(text, false, ref ticks);
		}

		/// <summary>
		/// Reads clock parts, the last of them seconds with an optional fraction, and adds them to
		/// <paramref name="ticks"/>. After days, the clock is hours, minutes and seconds of two
		/// digits each; otherwise it has two to four parts, and the first takes any number of digits.
		/// </summary>
		private static bool TryReadClock(ReadOnlySpan<char> text, bool afterDays, ref long ticks)
		{
			int parts = 1;
			foreach (char c in text)
			{
				if (c == ':') parts++;
			}

			if (afterDays ? parts != 3 : (parts < 2 || parts > UnitTicks.Length)) return false;

			// The last part is seconds, and each part before it the next larger unit.
			int at = 0;
			for (int part = 0, unit = UnitTicks.Length - parts; part < parts; part++, unit++)
			{
				int start = at;
				while (at < text.Length && IsDigit(text[at])) at++;

				// Every part but a leading one takes two digits, and stays below 60, or below 24
				// for hours, which always come after days here.
				int digits = at - start;
				bool bounded = part > 0 || afterDays;
				if (digits == 0 || (bounded && digits != 2)) return false;
				if (!TryReadWhole(text.Slice(start, digits), out long amount)) return false;
				if (bounded && amount >= (unit == HourUnit ? 24 : 60)) return false;
				if (!TryAdd(ref ticks, amount, UnitTicks[unit])) return false;

				if (part < parts - 1)
				{
					if (at == text.Length || text[at] != ':') return false;
					at++;
				}
			}

			if (at == text.Length) return true;

			// A fraction of a second, and nothing after it.
			if (text[at] != '.') return false;

			int fractionStart = ++at;
			while (at < text.Length && IsDigit(text[at])) at++;

			return at > fractionStart && at == text.Length && TryAdd(ref ticks, FractionTicks(text.Slice(fractionStart), TimeSpan.TicksPerSecond), 1);
		}

		/// <summary>Reads units, or a number of seconds alone, from text that starts with neither whitespace nor a sign.</summary>
		private static bool TryParseUnits(ReadOnlySpan<char> text, out long ticks)
		{
			ticks = 0;

			int at = 0;
			int lastUnit = -1;
			while (at < text.Length)
			{
				// A number: digits with an optional fraction, as in "90", "1.5" or ".5".
				int wholeStart = at;
				while (at < text.Length && IsDigit(text[at])) at++;
				ReadOnlySpan<char> whole = text.Slice(wholeStart, at - wholeStart);

				ReadOnlySpan<char> fraction = default;
				if (at < text.Length && text[at] == '.')
				{
					int fractionStart = ++at;
					while (at < text.Length && IsDigit(text[at])) at++;
					fraction = text.Slice(fractionStart, at - fractionStart);
				}

				if (whole.IsEmpty && fraction.IsEmpty) return false;
				if (!TryReadWhole(whole, out long amount)) return false;

				// Its unit, after optional whitespace. A number without one is seconds, and alone.
				int nameStart = SkipWhitespace(text, at);
				int nameEnd = nameStart;
				while (nameEnd < text.Length && IsLetter(text[nameEnd])) nameEnd++;

				int unit;
				if (nameEnd > nameStart)
				{
					if (!TryReadUnit(text.Slice(nameStart, nameEnd - nameStart), out unit) || unit <= lastUnit) return false;
					at = nameEnd;
				}
				else if (lastUnit < 0 && at == text.Length)
				{
					unit = SecondUnit;
				}
				else
				{
					return false;
				}

				if (!TryAdd(ref ticks, amount, UnitTicks[unit])) return false;
				if (!TryAdd(ref ticks, FractionTicks(fraction, UnitTicks[unit]), 1)) return false;

				lastUnit = unit;
				at = SkipWhitespace(text, at);
			}

			return lastUnit >= 0;
		}

		private static bool TryReadUnit(ReadOnlySpan<char> name, out int unit)
		{
			for (unit = 0; unit < UnitNames.Length; unit++)
			{
				foreach (string candidate in UnitNames[unit])
				{
					if (EqualsIgnoringCase(name, candidate)) return true;
				}
			}

			return false;
		}

		/// <summary>Compares ASCII letters with a name in lowercase.</summary>
		private static bool EqualsIgnoringCase(ReadOnlySpan<char> letters, string lowercase)
		{
			if (letters.Length != lowercase.Length) return false;

			for (int i = 0; i < letters.Length; i++)
			{
				// Setting bit 5 lowercases an ASCII letter.
				if ((letters[i] | 0x20) != lowercase[i]) return false;
			}

			return true;
		}

		/// <summary>Reads digits as a whole number, zero when there are none. False when it doesn't fit a long.</summary>
		private static bool TryReadWhole(ReadOnlySpan<char> digits, out long value)
		{
			value = 0;
			foreach (char c in digits)
			{
				int digit = c - '0';
				if (value > (long.MaxValue - digit) / 10) return false;

				value = value * 10 + digit;
			}

			return true;
		}

		/// <summary>
		/// The ticks in a decimal fraction of <paramref name="unit"/>, from the digits after the
		/// point. Exact for any number of digits; a part of a tick is dropped.
		/// </summary>
		private static long FractionTicks(ReadOnlySpan<char> digits, long unit)
		{
			// unit × 0.d1d2…dn from the last digit back: each step adds unit × d and divides by ten.
			// The value stays below unit, and dropping each step's remainder drops it once overall.
			long ticks = 0;
			for (int i = digits.Length - 1; i >= 0; i--)
			{
				ticks = (ticks + (digits[i] - '0') * unit) / 10;
			}

			return ticks;
		}

		/// <summary>Adds amount × unit to zero or more ticks. False when the sum doesn't fit a long.</summary>
		private static bool TryAdd(ref long ticks, long amount, long unit)
		{
			if (amount > (long.MaxValue - ticks) / unit) return false;

			ticks += amount * unit;
			return true;
		}

		private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> text)
		{
			int start = SkipWhitespace(text, 0);
			int end = text.Length;
			while (end > start && IsWhitespace(text[end - 1])) end--;
			return text.Slice(start, end - start);
		}

		private static int SkipWhitespace(ReadOnlySpan<char> text, int at)
		{
			while (at < text.Length && IsWhitespace(text[at])) at++;
			return at;
		}

		private static bool IsDigit(char c) => c >= '0' && c <= '9';

		private static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

		private static bool IsWhitespace(char c) => c == ' ' || (c >= '\t' && c <= '\r');
	}
}
