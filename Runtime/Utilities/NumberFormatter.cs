using System;
using System.Globalization;
using AK.Kernel.Formatting;

namespace AK.Utilities
{
	/// <summary>
	/// Short numbers for UI, such as 1500 as "1.5K": a facade over the kernel's
	/// <see cref="CompactNumber"/>.
	///
	/// <para><b>FormatAbbreviated</b> rounds to the nearest, halves away from zero, and uses the
	/// suffixes K, M, B, T and Q. <b>FormatDouble</b> rounds down or up, shows no decimals below a
	/// thousand, and goes on past T with aa to zz.</para>
	///
	/// <para>Both round before they pick the suffix, so 999,950 at one decimal is "1M", and both
	/// write the same text on every device: a point before the decimals and no thousands
	/// separators. A long is taken exactly, a double to fifteen significant digits and a float to
	/// seven.</para>
	/// </summary>
	public static class NumberFormatter
	{
		/// <summary>
		/// Formats a number with the suffixes K, M, B, T and Q, rounded to the nearest:
		/// 1500 → "1.5K", 999,950 → "1M". Zeros after the point are left off; past Q the number
		/// keeps growing.
		/// </summary>
		/// <param name="decimalPlaces">The most decimals shown, from 0 to 15.</param>
		public static string FormatAbbreviated(this int number, int decimalPlaces = 1) =>
			FormatAbbreviated((long)number, decimalPlaces);

		/// <inheritdoc cref="FormatAbbreviated(int, int)"/>
		public static string FormatAbbreviated(long number, int decimalPlaces = 1) =>
			CompactNumber.Format(number, Abbreviated(decimalPlaces));

		/// <summary>
		/// Formats a number with the suffixes K, M, B, T and Q, rounded to the nearest:
		/// 1500.5 → "1.5K", 999.95 → "1K". NaN and infinities are written "NaN", "Infinity" and
		/// "-Infinity".
		/// </summary>
		/// <param name="decimalPlaces">The most decimals shown, from 0 to 15.</param>
		public static string FormatAbbreviated(float number, int decimalPlaces = 1) =>
			CompactNumber.Format(number, Abbreviated(decimalPlaces));

		/// <inheritdoc cref="FormatAbbreviated(float, int)"/>
		public static string FormatAbbreviated(double number, int decimalPlaces = 1) =>
			CompactNumber.Format(number, Abbreviated(decimalPlaces));

		/// <summary>
		/// Reads a number with one of the suffixes K, M, B, T and Q, in any case, rounded to a whole
		/// number: "1.5K" → 1500, "1.2345K" → 1235. Returns 0 for text that isn't such a number or
		/// doesn't fit a long.
		/// </summary>
		public static long ParseAbbreviated(string abbreviatedNumber) =>
			CompactNumber.TryParseLong(abbreviatedNumber, NumberSuffixes.ShortScale, out long value) ? value : 0L;

		/// <summary>
		/// Formats a number with between 0 and 2 decimals: see <see cref="FormatDouble(double, int, int, bool)"/>.
		/// </summary>
		public static string FormatDouble(double d, bool roundDown = false) => FormatDouble(d, 0, 2, roundDown);

		/// <summary>
		/// Formats a number with exactly <paramref name="decimals"/> decimals above a thousand:
		/// see <see cref="FormatDouble(double, int, int, bool)"/>.
		/// </summary>
		public static string FormatDouble(double d, int decimals, bool roundDown = false) => FormatDouble(d, decimals, decimals, roundDown);

		/// <summary>
		/// Formats a number with the suffixes K, M, B and T, then aa to zz. Below a thousand it has
		/// no decimals.
		/// </summary>
		/// <param name="roundDown">
		/// True to round toward negative infinity, so the text is never more than the value: for
		/// amounts the player holds. False to round toward positive infinity: for costs and targets.
		/// </param>
		/// <exception cref="NumberFormatterException">The value is NaN or infinite, or <paramref name="maxDecimals"/> is below <paramref name="minDecimals"/>.</exception>
		public static string FormatDouble(double d, int minDecimals, int maxDecimals, bool roundDown = false) =>
			CompactNumber.Format(d, Directed(d, minDecimals, maxDecimals, roundDown));

		/// <summary>
		/// The number <see cref="FormatDouble(double, int, int, bool)"/> shows before its suffix:
		/// 1,234,567 rounded down to two decimals is 1.23.
		/// </summary>
		public static double ShortenDouble(double d, int minDecimals, int maxDecimals, bool roundDown = false)
		{
			Span<char> text = stackalloc char[CompactNumber.MaxLength];
			CompactNumber.TryFormat(d, Directed(d, minDecimals, maxDecimals, roundDown), text, out int written);

			int end = written;
			while (end > 0 && char.IsLetter(text[end - 1])) end--;

			CompactNumber.TryParseDouble(text.Slice(0, end), NumberSuffixes.Letters, out double shortened);
			return shortened;
		}

		/// <summary>
		/// Reads text that <see cref="FormatDouble(double, int, int, bool)"/> writes, such as "1.5K"
		/// or "3.25aa". Suffixes are read in any case.
		/// </summary>
		/// <exception cref="NumberFormatterException">The number or its suffix can't be read.</exception>
		public static double Parse(string s)
		{
			if (s == null) throw new ArgumentNullException(nameof(s));
			if (CompactNumber.TryParseDouble(s, NumberSuffixes.Letters, out double value)) return value;

			string message = IsNumberWithUnknownSuffix(s)
				? NumberFormatterException.PARSE_SUFFIX_VALUE_INVALID_MESSAGE
				: NumberFormatterException.PARSE_NUMERIC_VALUE_INVALID_MESSAGE;

			throw new NumberFormatterException(string.Format(message, s));
		}

		/// <summary>As <see cref="Parse"/>, but false instead of throwing.</summary>
		public static bool TryParse(string s, out double d) =>
			CompactNumber.TryParseDouble(s, NumberSuffixes.Letters, out d);

		/// <summary>
		/// <see cref="FormatDouble(double, bool)"/> for a whole number, taken exactly: a long past
		/// 2^53 has no exact double.
		/// </summary>
		internal static string FormatWhole(long value, bool roundDown) =>
			CompactNumber.Format(value, new CompactNumberFormat(0, 2, roundDown ? NumberRounding.Down : NumberRounding.Up, NumberSuffixes.Letters, wholeBelowThousand: true));

		private static CompactNumberFormat Abbreviated(int decimalPlaces) =>
			new(0, decimalPlaces, NumberRounding.Nearest, NumberSuffixes.ShortScale);

		private static CompactNumberFormat Directed(double d, int minDecimals, int maxDecimals, bool roundDown)
		{
			if (double.IsNaN(d) || double.IsInfinity(d))
			{
				throw new NumberFormatterException(string.Format(CultureInfo.InvariantCulture, NumberFormatterException.FORMAT_VALUE_INVALID_MESSAGE, d));
			}

			if (maxDecimals < minDecimals)
			{
				throw new NumberFormatterException(string.Format(NumberFormatterException.FORMAT_DECIMALS_INVALID_MESSAGE, maxDecimals, minDecimals));
			}

			return new CompactNumberFormat(minDecimals, maxDecimals, roundDown ? NumberRounding.Down : NumberRounding.Up, NumberSuffixes.Letters, wholeBelowThousand: true);
		}

		/// <summary>A number followed by letters that aren't a suffix, such as "1.5X".</summary>
		private static bool IsNumberWithUnknownSuffix(string text)
		{
			ReadOnlySpan<char> span = text.AsSpan().TrimEnd();
			int end = span.Length;
			while (end > 0 && char.IsLetter(span[end - 1])) end--;

			return end < span.Length && CompactNumber.TryParseDouble(span.Slice(0, end), NumberSuffixes.Letters, out _);
		}
	}

	public class NumberFormatterException : Exception
	{
		public const string FORMAT_VALUE_INVALID_MESSAGE = "Failed to format double, value {0} is invalid";

		public const string FORMAT_DECIMALS_INVALID_MESSAGE =
			"Failed to format double, maxDecimals {0} is lower than minDecimals {1}";

		public const string PARSE_NUMERIC_VALUE_INVALID_MESSAGE =
			"Failed to parse string \"{0}\", numeric value could not be parsed";

		public const string PARSE_SUFFIX_VALUE_INVALID_MESSAGE = "Failed to parse string \"{0}\", suffix value is invalid";

		public NumberFormatterException(string message) : base(message) { }
	}
}
