using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AK.Utilities
{
	/// <summary>
	/// Utility class for formatting numbers in abbreviated format (K, M, B, etc.)
	/// Useful for displaying large numbers in UI elements with limited space.
	/// </summary>
	public static class NumberFormatter
	{
		// Suffixes for thousands, millions, billions, trillions, quadrillions
		private static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Q" };

		private const int    ASCII_OFFSET = 97;
		private const string SUFFIXES     = "KMBT";

		/// <summary>
		/// Formats a number into abbreviated format (K, M, B, T, Q)
		/// Examples: 1500 -> "1.5K", 2500000 -> "2.5M", 1000000000 -> "1B"
		/// </summary>
		/// <param name="number">The number to format</param>
		/// <param name="decimalPlaces">Number of decimal places to show (default: 1)</param>
		/// <returns>Formatted string representation</returns>
		public static string FormatAbbreviated(this int number, int decimalPlaces = 1)
		{
			return FormatAbbreviated((long)number, decimalPlaces);
		}

		/// <summary>
		/// Formats a number into abbreviated format (K, M, B, T, Q)
		/// Examples: 1500 -> "1.5K", 2500000 -> "2.5M", 1000000000 -> "1B"
		/// </summary>
		/// <param name="number">The number to format</param>
		/// <param name="decimalPlaces">Number of decimal places to show (default: 1)</param>
		/// <returns>Formatted string representation</returns>
		public static string FormatAbbreviated(long number, int decimalPlaces = 1)
		{
			if (number < 1000)
				return number.ToString();

			// Determine the appropriate suffix
			int suffixIndex = 0;
			double scaledNumber = number;

			while (scaledNumber >= 1000 && suffixIndex < Suffixes.Length - 1)
			{
				scaledNumber /= 1000;
				suffixIndex++;
			}

			// Format with specified decimal places
			string format = $"0.{new string('0', decimalPlaces)}";
			string formatted = scaledNumber.ToString(format);

			// Remove trailing zeros and decimal point if not needed
			formatted = formatted.TrimEnd('0').TrimEnd('.');

			return $"{formatted}{Suffixes[suffixIndex]}";
		}

		/// <summary>
		/// Formats a number into abbreviated format (K, M, B, T, Q)
		/// Examples: 1500.5 -> "1.5K", 2500000.75 -> "2.5M", 1000000000.25 -> "1B"
		/// </summary>
		/// <param name="number">The number to format</param>
		/// <param name="decimalPlaces">Number of decimal places to show (default: 1)</param>
		/// <returns>Formatted string representation</returns>
		public static string FormatAbbreviated(float number, int decimalPlaces = 1)
		{
			return FormatAbbreviated((double)number, decimalPlaces);
		}

		/// <summary>
		/// Formats a number into abbreviated format (K, M, B, T, Q)
		/// Examples: 1500.5 -> "1.5K", 2500000.75 -> "2.5M", 1000000000.25 -> "1B"
		/// </summary>
		/// <param name="number">The number to format</param>
		/// <param name="decimalPlaces">Number of decimal places to show (default: 1)</param>
		/// <returns>Formatted string representation</returns>
		public static string FormatAbbreviated(double number, int decimalPlaces = 1)
		{
			if (Math.Abs(number) < 1000)
				return number.ToString($"F{decimalPlaces}").TrimEnd('0').TrimEnd('.');

			// Determine the appropriate suffix
			int suffixIndex = 0;
			double scaledNumber = Math.Abs(number);

			while (scaledNumber >= 1000 && suffixIndex < Suffixes.Length - 1)
			{
				scaledNumber /= 1000;
				suffixIndex++;
			}

			// Apply the scaling to the original number (preserving sign)
			scaledNumber = number / Math.Pow(1000, suffixIndex);

			// Format with specified decimal places
			string format = $"0.{new string('0', decimalPlaces)}";
			string formatted = scaledNumber.ToString(format);

			// Remove trailing zeros and decimal point if not needed
			formatted = formatted.TrimEnd('0').TrimEnd('.');

			return $"{formatted}{Suffixes[suffixIndex]}";
		}

		/// <summary>
		/// Parses an abbreviated format string back to a long number
		/// Examples: "1.5K" -> 1500, "2.5M" -> 2500000, "1B" -> 1000000000
		/// </summary>
		/// <param name="abbreviatedNumber">The abbreviated string to parse</param>
		/// <returns>The parsed number as long</returns>
		public static long ParseAbbreviated(string abbreviatedNumber)
		{
			if (string.IsNullOrEmpty(abbreviatedNumber))
				return 0;

			abbreviatedNumber = abbreviatedNumber.Trim().ToUpper();

			// Find the suffix
			string suffix = "";
			string numberPart = abbreviatedNumber;

			for (int i = 0; i < Suffixes.Length; i++)
			{
				if (abbreviatedNumber.EndsWith(Suffixes[i]))
				{
					suffix = Suffixes[i];
					numberPart = abbreviatedNumber.Substring(0, abbreviatedNumber.Length - suffix.Length);
					break;
				}
			}

			if (!double.TryParse(numberPart, out double number))
				return 0;

			int suffixIndex = Array.IndexOf(Suffixes, suffix);
			return (long)(number * Math.Pow(1000, suffixIndex));
		}

		/// <summary>
		/// Takes a double and converts it to a short value with a suffix.
		/// By default, between 0 and 2 decimals will be used
		/// </summary>
		/// <param name="d">The number to be formatted</param>
		/// <param name="roundDown">Pass true to floor the value, false to ceiling it.</param>
		/// <remarks>Values relating to user inventory should be rounded down while costs and targets should not.
		/// Numbers below 1000 will always be displayed with zero decimals.</remarks>
		/// <returns></returns>
		public static string FormatDouble(double d, bool roundDown = false)
		{
			return FormatDouble(d, 0, 2, roundDown);
		}

		/// <summary>
		/// Takes a double and converts it to a short value with a suffix.
		/// </summary>
		/// <param name="d">The number to be formatted</param>
		/// <param name="decimals">The minimum and maximum number of decimals to display</param>
		/// <param name="roundDown">Pass true to floor the value, false to ceiling it.</param>
		/// <remarks>Values relating to user inventory should be rounded down while costs and targets should not.
		/// Numbers below 1000 will always be displayed with zero decimals.</remarks>
		/// <returns></returns>
		public static string FormatDouble(double d, int decimals, bool roundDown = false)
		{
			return FormatDouble(d, decimals, decimals, roundDown);
		}

		/// <summary>
		/// Takes a double and converts it to a short value with a suffix.
		/// </summary>
		/// <param name="d">The number to be formatted</param>
		/// <param name="minDecimals">The minimum number of decimals to display</param>
		/// <param name="maxDecimals">The maximum number of decimals to display</param>
		/// <param name="roundDown">Pass true to floor the value, false to ceiling it.</param>
		/// <remarks>Values relating to user inventory should be rounded down while costs and targets should not.
		/// Numbers below 1000 will always be displayed with zero decimals.</remarks>
		/// <returns></returns>
		public static string FormatDouble(double d, int minDecimals, int maxDecimals, bool roundDown = false)
		{
			if (Double.IsNaN(d) || Double.IsInfinity(d))
				throw new NumberFormatterException(String.Format(NumberFormatterException.FORMAT_VALUE_INVALID_MESSAGE, d));
			if (maxDecimals < minDecimals)
				throw new NumberFormatterException(String.Format(NumberFormatterException.FORMAT_DECIMALS_INVALID_MESSAGE,
					maxDecimals, minDecimals));
			string format = GetFormat(minDecimals, maxDecimals);
			if (d < 1000d)
			{
				d = roundDown ? Math.Floor(d) : Math.Ceiling(d); //If d is less than 1000 we can simply return it without a suffix
				return $"{d}";
			}

			double shortened = ShortenDouble(d, minDecimals, maxDecimals, roundDown);
			int e = GetExponent(d);
			return string.Format(format, shortened, GetSuffix(e));
		}

		public static double ShortenDouble(double d, int minDecimals, int maxDecimals, bool roundDown = false)
		{
			if (Double.IsNaN(d) || Double.IsInfinity(d))
				throw new NumberFormatterException(String.Format(NumberFormatterException.FORMAT_VALUE_INVALID_MESSAGE, d));
			if (maxDecimals < minDecimals)
				throw new NumberFormatterException(String.Format(NumberFormatterException.FORMAT_DECIMALS_INVALID_MESSAGE,
					maxDecimals, minDecimals));
			int e = GetExponent(d); //First get the exponent, what power of 1000 is less than d
			d = d / Math.Pow(1000,
				e); //Second, divide d by that power of 1000 to get the value that will display before the suffix
			double t = Math.Pow(10, maxDecimals); //Third, we need to find 10 to the power of our maxDecimals for rounding
			d = roundDown
				? Math.Floor(d * t) / t
				: Math.Ceiling(d * t) / t; //Fourth, we either floor or ceiling d to the number of maxDecimals requested
			return d;
		}

		private static int GetExponent(double d) => (int)Math.Floor(Math.Log(Math.Abs(d)) / Math.Log(1000));

		/// <summary>
		/// Takes a formatted string and attempts to parse the decimal value
		/// </summary>
		/// <param name="s">The string value to parse</param>
		/// <returns>The double value of the string</returns>
		public static double Parse(string s)
		{
			Regex pattern = new Regex("^(-?[0-9,.]+)([a-zA-Z]+)$");
			Match match = pattern.Match(s);
			double d = 0;
			int e = 0;
			if (!match.Success)
			{
				if (!Double.TryParse(s, out d))
					throw new NumberFormatterException(String.Format(NumberFormatterException.PARSE_NUMERIC_VALUE_INVALID_MESSAGE,
						s));
				else
					return d;
			}

			string numericString = match.Groups[1].Value;
			string exponentString = match.Groups[2].Value;
			if (!Double.TryParse(numericString, out d))
				throw new NumberFormatterException(String.Format(NumberFormatterException.PARSE_NUMERIC_VALUE_INVALID_MESSAGE, s));
			if (exponentString.Length == 1)
			{
				e = SUFFIXES.IndexOf(exponentString) + 1;
				if (e == 0)
					throw new NumberFormatterException(String.Format(NumberFormatterException.PARSE_SUFFIX_VALUE_INVALID_MESSAGE,
						s));
			}
			else if (exponentString.Length == 2)
			{
				e = (int)(exponentString[0] - ASCII_OFFSET) * 26 + (int)exponentString[1] - ASCII_OFFSET + 5;
				if (e < 5 || e > 26 * 26 + 5)
					throw new NumberFormatterException(String.Format(NumberFormatterException.PARSE_SUFFIX_VALUE_INVALID_MESSAGE,
						s));
			}
			else
				throw new NumberFormatterException(String.Format(NumberFormatterException.PARSE_SUFFIX_VALUE_INVALID_MESSAGE, s));

			d *= Math.Pow(1000, e);
			return d;
		}

		public static bool TryParse(string s, out double d)
		{
			try
			{
				d = Parse(s);
				return true;
			}
			catch (NumberFormatterException)
			{
				d = 0;
				return false;
			}
		}

		private static string GetFormat(int minDecimals, int maxDecimals)
		{
			StringBuilder format = new StringBuilder("{0:0.");
			format.Append('0', minDecimals);
			format.Append('#', maxDecimals - minDecimals);
			format.Append("}{1}");
			return format.ToString();
		}

		private static string GetSuffix(int e)
		{
			if (e == 0)
				return "";
			else if (e < 5)
				return SUFFIXES.Substring(e - 1, 1);
			int index = e - 5;
			char[] chars = new char[2];
			chars[0] = (char)(ASCII_OFFSET + index / 26);
			chars[1] = (char)(ASCII_OFFSET + index % 26);
			return new string(chars);
		}
	}

	public class NumberFormatterException : Exception
	{
		public static string FORMAT_VALUE_INVALID_MESSAGE = "Failed to format double, value {0} is invalid";

		public static string FORMAT_DECIMALS_INVALID_MESSAGE =
			"Failed to format double, maxDecimals {0} is lower than minDecimals {1}";

		public static string PARSE_NUMERIC_VALUE_INVALID_MESSAGE =
			"Failed to parse string \"{0}\", numeric value could not be parsed";

		public static string PARSE_SUFFIX_VALUE_INVALID_MESSAGE = "Failed to parse string \"{0}\", suffix value is invalid";

		public NumberFormatterException(string message) : base(message) { }
	}
}