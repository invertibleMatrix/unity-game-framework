using System;
using AK.Core.Extensions;
using AK.Tests.Support;
using AK.Utilities;
using NUnit.Framework;

namespace AK.Tests.Utilities
{
	public class NumberFormatterTests
	{
		// ------------------------------------------------------------------ FormatAbbreviated

		[TestCase(0L, 1, "0")]
		[TestCase(999L, 1, "999")]
		[TestCase(1000L, 1, "1K")]
		[TestCase(1500L, 1, "1.5K")]
		[TestCase(2000L, 1, "2K")]
		[TestCase(2_500_000L, 1, "2.5M")]
		[TestCase(1_500_000_000L, 1, "1.5B")]
		[TestCase(2_500_000_000_000L, 1, "2.5T")]
		[TestCase(1_500_000_000_000_000L, 1, "1.5Q")]
		[TestCase(1234L, 2, "1.23K")]
		[TestCase(1250L, 1, "1.3K", Description = "halves round away from zero")]
		[TestCase(-1250L, 1, "-1.3K")]
		[TestCase(1_050_000L, 1, "1.1M")]
		[TestCase(1_000_100L, 1, "1M", Description = "zeros after the point are left off")]
		public void FormatAbbreviated_Long(long value, int decimals, string expected)
		{
			Assert.AreEqual(expected, NumberFormatter.FormatAbbreviated(value, decimals));
		}

		[TestCase(999_949L, 1, "999.9K")]
		[TestCase(999_950L, 1, "1M")]
		[TestCase(999_999L, 1, "1M")]
		[TestCase(999_994L, 2, "999.99K")]
		[TestCase(999_995L, 2, "1M")]
		[TestCase(999_499L, 0, "999K")]
		[TestCase(999_500L, 0, "1M")]
		[TestCase(-999_950L, 1, "-1M")]
		[TestCase(999_950_000L, 1, "1B")]
		public void FormatAbbreviated_RoundsBeforePickingTheSuffix(long value, int decimals, string expected)
		{
			Assert.AreEqual(expected, NumberFormatter.FormatAbbreviated(value, decimals));
		}

		[TestCase(10_000L, "10K")]
		[TestCase(100_000L, "100K")]
		[TestCase(20_000_000L, "20M")]
		[TestCase(1500L, "2K")]
		[TestCase(2500L, "3K")]
		public void FormatAbbreviated_WithoutDecimals_KeepsTheWholeNumber(long value, string expected)
		{
			Assert.AreEqual(expected, NumberFormatter.FormatAbbreviated(value, 0));
		}

		[Test]
		public void FormatAbbreviated_PastQ_TheNumberKeepsGrowing()
		{
			Assert.AreEqual("9223.4Q", NumberFormatter.FormatAbbreviated(long.MaxValue));
			Assert.AreEqual("-9223.4Q", NumberFormatter.FormatAbbreviated(long.MinValue));
			Assert.AreEqual("1000Q", NumberFormatter.FormatAbbreviated(999_999_999_999_999_999L));
		}

		[TestCase(0d, 0, "0")]
		[TestCase(0.5d, 0, "1")]
		[TestCase(-0.4d, 0, "0", Description = "no negative zero")]
		[TestCase(-0.5d, 0, "-1")]
		[TestCase(100d, 0, "100")]
		[TestCase(12.34d, 1, "12.3")]
		[TestCase(0.15d, 1, "0.2", Description = "rounds the decimal the double was written as")]
		[TestCase(2.675d, 2, "2.68")]
		[TestCase(999.94d, 1, "999.9")]
		[TestCase(999.95d, 1, "1K")]
		[TestCase(-2_500_000.75d, 1, "-2.5M")]
		[TestCase(1e18d, 1, "1000Q")]
		[TestCase(1e21d, 1, "1000000Q")]
		public void FormatAbbreviated_Double(double value, int decimals, string expected)
		{
			Assert.AreEqual(expected, NumberFormatter.FormatAbbreviated(value, decimals));
		}

		[TestCase(double.NaN, "NaN")]
		[TestCase(double.PositiveInfinity, "Infinity")]
		[TestCase(double.NegativeInfinity, "-Infinity")]
		public void FormatAbbreviated_WritesNaNAndInfinities_AsTheInvariantCultureDoes(double value, string expected)
		{
			Assert.AreEqual(expected, NumberFormatter.FormatAbbreviated(value));
		}

		[TestCase(1234.56f, 1, "1.2K")]
		[TestCase(2.675f, 2, "2.68", Description = "a float is read to seven significant digits")]
		[TestCase(0.3f, 1, "0.3")]
		[TestCase(999.95f, 1, "1K")]
		public void FormatAbbreviated_Float(float value, int decimals, string expected)
		{
			Assert.AreEqual(expected, NumberFormatter.FormatAbbreviated(value, decimals));
		}

		[Test]
		public void FormatAbbreviated_ExtendsInt()
		{
			Assert.AreEqual("1.5K", 1500.FormatAbbreviated());
			Assert.AreEqual("1.23K", 1234.FormatAbbreviated(2));
		}

		[TestCase(-1), TestCase(16)]
		public void FormatAbbreviated_Throws_ForDecimalsOutOfRange(int decimals)
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => NumberFormatter.FormatAbbreviated(1500L, decimals));
			Assert.Throws<ArgumentOutOfRangeException>(() => NumberFormatter.FormatAbbreviated(1500d, decimals));
		}

		// ------------------------------------------------------------------ ParseAbbreviated

		[TestCase("1.5K", 1500L)]
		[TestCase("1.5k", 1500L)]
		[TestCase("2.5M", 2_500_000L)]
		[TestCase("1B", 1_000_000_000L)]
		[TestCase("1.5Q", 1_500_000_000_000_000L)]
		[TestCase("999", 999L)]
		[TestCase(" 2.5M ", 2_500_000L)]
		[TestCase("1.5 K", 1500L)]
		[TestCase("-1.5K", -1500L)]
		[TestCase("1.2345K", 1235L, Description = "rounds to a whole number, halves away from zero")]
		[TestCase("9223.372036854775807Q", long.MaxValue)]
		[TestCase("-9223.372036854775808Q", long.MinValue)]
		public void ParseAbbreviated_ReadsTheShortScaleSuffixes(string text, long expected)
		{
			Assert.AreEqual(expected, NumberFormatter.ParseAbbreviated(text));
		}

		[TestCase((string)null), TestCase(""), TestCase("invalid"), TestCase("1.5X"), TestCase("1,5K"), TestCase("1.5aa"), TestCase("K"), TestCase("1.5KK"), TestCase("9223.372036854775808Q")]
		public void ParseAbbreviated_ReturnsZero_ForAnythingElse(string text)
		{
			Assert.AreEqual(0L, NumberFormatter.ParseAbbreviated(text));
		}

		[TestCase(500L), TestCase(1500L), TestCase(25_000L), TestCase(12_500_000L), TestCase(2_500_000_000L)]
		public void ParseAbbreviated_ReadsBackWhatFormatAbbreviatedWrites(long value)
		{
			Assert.AreEqual(value, NumberFormatter.ParseAbbreviated(NumberFormatter.FormatAbbreviated(value)));
		}

		// ------------------------------------------------------------------ FormatDouble

		[TestCase(999.2d, false, "1K")]
		[TestCase(999.2d, true, "999")]
		[TestCase(1100d, false, "1.1K", Description = "1.1 has no exact double, and rounding up must not make it 1.11")]
		[TestCase(1100d, true, "1.1K")]
		[TestCase(999_999d, false, "1M")]
		[TestCase(999_999d, true, "999.99K")]
		[TestCase(1e6d, true, "1M")]
		[TestCase(1e9d, true, "1B")]
		[TestCase(1e12d, true, "1T")]
		[TestCase(1e15d, true, "1aa")]
		[TestCase(1e18d, true, "1ab")]
		[TestCase(2.5e17d, true, "250aa")]
		[TestCase(-1500d, true, "-1.5K")]
		[TestCase(-1501d, true, "-1.51K", Description = "rounding down goes toward negative infinity")]
		[TestCase(-1501d, false, "-1.5K")]
		[TestCase(0.5d, false, "1")]
		[TestCase(0.5d, true, "0")]
		[TestCase(-0.5d, true, "-1")]
		[TestCase(-0.5d, false, "0")]
		[TestCase(double.MaxValue, true, "179.76dt")]
		public void FormatDouble_RoundsUpOrDown_WithLetterSuffixesPastT(double value, bool roundDown, string expected)
		{
			Assert.AreEqual(expected, NumberFormatter.FormatDouble(value, roundDown));
		}

		[Test]
		public void FormatDouble_ShowsBetweenTheMinimumAndMaximumDecimals_AndNoneBelowAThousand()
		{
			Assert.AreEqual("1.2345K", NumberFormatter.FormatDouble(1234.5678d, 2, 4, roundDown: true));
			Assert.AreEqual("1.20K", NumberFormatter.FormatDouble(1200d, 2, 4, roundDown: true));
			Assert.AreEqual("1.20K", NumberFormatter.FormatDouble(1200d, 2, roundDown: true));
			Assert.AreEqual("500", NumberFormatter.FormatDouble(500.7d, 2, roundDown: true));
			Assert.AreEqual("501", NumberFormatter.FormatDouble(500.2d, 2, roundDown: false));
		}

		[TestCase(double.NaN), TestCase(double.PositiveInfinity), TestCase(double.NegativeInfinity)]
		public void FormatDouble_Throws_ForNaNAndInfinities(double value)
		{
			Assert.Throws<NumberFormatterException>(() => NumberFormatter.FormatDouble(value));
		}

		[Test]
		public void FormatDouble_Throws_WhenTheMaximumDecimalsAreBelowTheMinimum()
		{
			Assert.Throws<NumberFormatterException>(() => NumberFormatter.FormatDouble(1500d, 3, 2));
		}

		[TestCase(1_234_567d, true, 1.23d)]
		[TestCase(999_999d, false, 1d, Description = "the carry into M")]
		[TestCase(1100d, false, 1.1d)]
		[TestCase(500.555d, true, 500d, Description = "no decimals below a thousand, as FormatDouble shows")]
		public void ShortenDouble_IsTheNumberFormatDoubleShows_BeforeItsSuffix(double value, bool roundDown, double expected)
		{
			Assert.AreEqual(expected, NumberFormatter.ShortenDouble(value, 0, 2, roundDown));
		}

		// ------------------------------------------------------------------ Parse

		[TestCase("1.5K", 1500d)]
		[TestCase("1.5k", 1500d)]
		[TestCase("2.5M", 2.5e6d)]
		[TestCase("1.5aa", 1.5e15d)]
		[TestCase("1ab", 1e18d)]
		[TestCase("123", 123d)]
		[TestCase("-1.5K", -1500d)]
		[TestCase("179.76dt", 1.7976e308d)]
		public void Parse_ReadsTheLetterSuffixes(string text, double expected)
		{
			Assert.AreEqual(expected, NumberFormatter.Parse(text));
			Assert.IsTrue(NumberFormatter.TryParse(text, out double value));
			Assert.AreEqual(expected, value);
		}

		[TestCase("1.5X", NumberFormatterException.PARSE_SUFFIX_VALUE_INVALID_MESSAGE)]
		[TestCase("1.5Q", NumberFormatterException.PARSE_SUFFIX_VALUE_INVALID_MESSAGE)]
		[TestCase("abc", NumberFormatterException.PARSE_NUMERIC_VALUE_INVALID_MESSAGE)]
		[TestCase("", NumberFormatterException.PARSE_NUMERIC_VALUE_INVALID_MESSAGE)]
		[TestCase("1,5K", NumberFormatterException.PARSE_NUMERIC_VALUE_INVALID_MESSAGE)]
		public void Parse_Throws_SayingWhichPartIsWrong(string text, string message)
		{
			var exception = Assert.Throws<NumberFormatterException>(() => NumberFormatter.Parse(text));
			Assert.AreEqual(string.Format(message, text), exception.Message);

			Assert.IsFalse(NumberFormatter.TryParse(text, out double value));
			Assert.AreEqual(0d, value);
		}

		[TestCase(1500d), TestCase(999_990d), TestCase(2.5e17d), TestCase(1.25e21d)]
		public void Parse_ReadsBackWhatFormatDoubleWrites(double value)
		{
			Assert.AreEqual(value, NumberFormatter.Parse(NumberFormatter.FormatDouble(value, roundDown: true)));
		}

		// ------------------------------------------------------------------ ToSuffix

		[TestCase(1500L, "1.5K")]
		[TestCase(999_999L, "999.99K")]
		[TestCase(-1501L, "-1.51K")]
		[TestCase(long.MaxValue, "9.22ab")]
		public void ToSuffix_RoundsDown_WithLetterSuffixes(long value, string expected)
		{
			Assert.AreEqual(expected, value.ToSuffix());
		}

		[Test]
		public void ToSuffix_ExtendsInt()
		{
			Assert.AreEqual("2.14B", int.MaxValue.ToSuffix());
		}

		// ------------------------------------------------------------------ culture

		[TestCase("de-DE"), TestCase("fr-FR"), TestCase("ar-SA")]
		public void TextIsTheSame_OnADeviceInAnotherCulture(string culture)
		{
			using (new CultureScope(culture))
			{
				Assert.AreEqual("1.5K", NumberFormatter.FormatAbbreviated(1500.5d));
				Assert.AreEqual("1.23M", NumberFormatter.FormatAbbreviated(1_234_567L, 2));
				Assert.AreEqual("1.23K", NumberFormatter.FormatDouble(1234.5d, roundDown: true));
				Assert.AreEqual(1500L, NumberFormatter.ParseAbbreviated("1.5K"));
				Assert.AreEqual(1500d, NumberFormatter.Parse("1.5K"));
			}
		}
	}
}
