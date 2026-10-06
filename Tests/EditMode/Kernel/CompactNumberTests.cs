using System;
using AK.Kernel.Formatting;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class CompactNumberTests
	{
		private const NumberRounding Nearest = NumberRounding.Nearest;
		private const NumberRounding Down    = NumberRounding.Down;
		private const NumberRounding Up      = NumberRounding.Up;

		private const NumberSuffixes ShortScale = NumberSuffixes.ShortScale;
		private const NumberSuffixes Letters    = NumberSuffixes.Letters;

		// ------------------------------------------------------------------ longs

		[TestCase(0L, 0, 1, Nearest, ShortScale, false, "0")]
		[TestCase(999L, 0, 1, Nearest, ShortScale, false, "999")]
		[TestCase(123L, 2, 2, Nearest, ShortScale, false, "123.00")]
		[TestCase(-1L, 0, 0, Nearest, ShortScale, false, "-1")]
		[TestCase(1000L, 0, 1, Nearest, ShortScale, false, "1K")]
		[TestCase(1500L, 0, 1, Nearest, ShortScale, false, "1.5K")]
		[TestCase(1500L, 2, 2, Nearest, ShortScale, false, "1.50K")]
		[TestCase(1249L, 0, 1, Nearest, ShortScale, false, "1.2K")]
		[TestCase(1250L, 0, 1, Nearest, ShortScale, false, "1.3K", Description = "halves go away from zero")]
		[TestCase(-1250L, 0, 1, Nearest, ShortScale, false, "-1.3K")]
		[TestCase(1201L, 0, 1, Down, ShortScale, false, "1.2K")]
		[TestCase(1201L, 0, 1, Up, ShortScale, false, "1.3K")]
		[TestCase(-1201L, 0, 1, Down, ShortScale, false, "-1.3K", Description = "down is toward negative infinity")]
		[TestCase(-1201L, 0, 1, Up, ShortScale, false, "-1.2K")]
		[TestCase(999_950L, 0, 1, Nearest, ShortScale, false, "1M", Description = "rounding comes before the suffix")]
		[TestCase(999_999L, 0, 2, Down, ShortScale, false, "999.99K")]
		[TestCase(999_991L, 0, 2, Up, ShortScale, false, "1M")]
		[TestCase(1_000_000_000_000_000L, 0, 1, Nearest, ShortScale, false, "1Q")]
		[TestCase(long.MaxValue, 0, 1, Nearest, ShortScale, false, "9223.4Q")]
		[TestCase(long.MinValue, 0, 3, Nearest, ShortScale, false, "-9223.372Q")]
		[TestCase(long.MaxValue, 0, 15, Nearest, ShortScale, false, "9223.372036854775807Q", Description = "a long is taken exactly")]
		[TestCase(1_500_000_000_000_000L, 0, 1, Nearest, Letters, false, "1.5aa")]
		[TestCase(long.MaxValue, 0, 2, Down, Letters, false, "9.22ab")]
		[TestCase(999L, 0, 2, Up, Letters, true, "999")]
		[TestCase(-999L, 0, 2, Down, Letters, true, "-999")]
		[TestCase(1500L, 2, 2, Down, Letters, true, "1.50K")]
		public void Long_IsWrittenExactly(long value, int min, int max, NumberRounding rounding, NumberSuffixes suffixes, bool wholeBelowThousand, string expected)
		{
			Assert.AreEqual(expected, CompactNumber.Format(value, new CompactNumberFormat(min, max, rounding, suffixes, wholeBelowThousand)));
		}

		// ------------------------------------------------------------------ doubles and floats

		[TestCase(0.30000000000000004d, 0, 15, Nearest, ShortScale, false, "0.3", Description = "a double is taken to fifteen significant digits")]
		[TestCase(2.675d, 0, 2, Nearest, ShortScale, false, "2.68", Description = "rounding works on decimal digits")]
		[TestCase(0.5d, 0, 0, Nearest, ShortScale, false, "1")]
		[TestCase(-0.5d, 0, 0, Nearest, ShortScale, false, "-1")]
		[TestCase(-0.4d, 0, 0, Nearest, ShortScale, false, "0", Description = "zero has no sign")]
		[TestCase(0.004d, 0, 2, Nearest, ShortScale, false, "0")]
		[TestCase(0.005d, 0, 2, Nearest, ShortScale, false, "0.01")]
		[TestCase(0.004d, 0, 2, Up, ShortScale, false, "0.01")]
		[TestCase(-0.004d, 0, 2, Down, ShortScale, false, "-0.01")]
		[TestCase(-0.004d, 0, 2, Up, ShortScale, false, "0")]
		[TestCase(double.Epsilon, 0, 15, Nearest, ShortScale, false, "0")]
		[TestCase(1.23456789012345678e17d, 0, 3, Nearest, ShortScale, false, "123.457Q")]
		[TestCase(1e18d, 0, 1, Nearest, ShortScale, false, "1000Q", Description = "past Q the number grows")]
		[TestCase(1e18d, 0, 1, Nearest, Letters, false, "1ab")]
		[TestCase(1100d, 0, 2, Up, Letters, true, "1.1K", Description = "rounding up a value that is already exact adds nothing")]
		[TestCase(999.2d, 0, 2, Up, Letters, true, "1K")]
		[TestCase(500.7d, 2, 2, Down, Letters, true, "500")]
		[TestCase(double.MaxValue, 0, 2, Down, Letters, false, "179.76dt")]
		[TestCase(-double.MaxValue, 0, 2, Down, Letters, false, "-179.77dt")]
		public void Double_IsTakenToFifteenDigits(double value, int min, int max, NumberRounding rounding, NumberSuffixes suffixes, bool wholeBelowThousand, string expected)
		{
			Assert.AreEqual(expected, CompactNumber.Format(value, new CompactNumberFormat(min, max, rounding, suffixes, wholeBelowThousand)));
		}

		[TestCase(0.3f, 0, 15, "0.3", Description = "a float is taken to seven significant digits")]
		[TestCase(2.675f, 0, 2, "2.68")]
		[TestCase(1234.56f, 0, 1, "1.2K")]
		[TestCase(16_777_217f, 0, 3, "16.777M")]
		public void Float_IsTakenToSevenDigits(float value, int min, int max, string expected)
		{
			Assert.AreEqual(expected, CompactNumber.Format(value, new CompactNumberFormat(min, max)));
		}

		[Test]
		public void FloatWidenedToDouble_ShowsTheBinaryDigits()
		{
			Assert.AreEqual("0.300000011920929", CompactNumber.Format((double)0.3f, new CompactNumberFormat(0, 15)));
		}

		[TestCase(double.NaN, "NaN"), TestCase(double.PositiveInfinity, "Infinity"), TestCase(double.NegativeInfinity, "-Infinity")]
		public void NonFinite_IsWrittenAsTheInvariantCultureDoes(double value, string expected)
		{
			Assert.AreEqual(expected, CompactNumber.Format(value, new CompactNumberFormat(0, 1)));
			Assert.AreEqual(expected, CompactNumber.Format((float)value, new CompactNumberFormat(0, 1)));
		}

		// ------------------------------------------------------------------ destination

		[Test]
		public void TryFormat_FailsWithoutWriting_WhenTheDestinationIsShort()
		{
			Span<char> text = stackalloc char[4];
			text.Fill('#');

			Assert.IsFalse(CompactNumber.TryFormat(1500L, new CompactNumberFormat(0, 2), text.Slice(0, 3), out int written));
			Assert.AreEqual(0, written);
			Assert.AreEqual("####", new string(text));

			Assert.IsTrue(CompactNumber.TryFormat(1500L, new CompactNumberFormat(0, 2), text, out written));
			Assert.AreEqual("1.5K", new string(text.Slice(0, written)));

			Assert.IsFalse(CompactNumber.TryFormat(double.NaN, new CompactNumberFormat(0, 2), text.Slice(0, 2), out written));
			Assert.AreEqual(0, written);
		}

		[Test]
		public void MaxLength_HoldsTheLongestText()
		{
			string text = CompactNumber.Format(-double.MaxValue, new CompactNumberFormat(15, 15, Nearest, ShortScale));
			Assert.LessOrEqual(text.Length, CompactNumber.MaxLength);
			StringAssert.EndsWith("Q", text);
		}

		// ------------------------------------------------------------------ format

		[TestCase(-1, 1), TestCase(16, 16), TestCase(2, 1), TestCase(0, 16)]
		public void Format_Throws_ForDecimalsOutOfRange(int min, int max)
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new CompactNumberFormat(min, max));
		}

		[Test]
		public void Format_Throws_ForEnumsOutOfRange()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new CompactNumberFormat(0, 1, (NumberRounding)3));
			Assert.Throws<ArgumentOutOfRangeException>(() => new CompactNumberFormat(0, 1, Nearest, (NumberSuffixes)2));
		}

		// ------------------------------------------------------------------ parsing doubles

		[TestCase("1.5K", ShortScale, 1500d)]
		[TestCase("1.5k", ShortScale, 1500d)]
		[TestCase(" -2.5 M ", ShortScale, -2_500_000d)]
		[TestCase("+3", ShortScale, 3d)]
		[TestCase(".5K", ShortScale, 500d)]
		[TestCase("5.K", ShortScale, 5000d)]
		[TestCase("1Q", ShortScale, 1e15d)]
		[TestCase("1.5aa", Letters, 1.5e15d)]
		[TestCase("1.5AA", Letters, 1.5e15d)]
		[TestCase("2T", Letters, 2e12d)]
		[TestCase("179.76dt", Letters, 1.7976e308d)]
		public void ParseDouble_ReadsANumberAndItsSuffix(string text, NumberSuffixes suffixes, double expected)
		{
			Assert.IsTrue(CompactNumber.TryParseDouble(text, suffixes, out double value));
			Assert.AreEqual(expected, value);
		}

		[TestCase("", ShortScale), TestCase("   ", ShortScale), TestCase("K", ShortScale), TestCase("-", ShortScale), TestCase(".", ShortScale)]
		[TestCase("1.5X", ShortScale), TestCase("1.5KK", ShortScale), TestCase("1.5 K K", ShortScale), TestCase("1e3", ShortScale)]
		[TestCase("1,5K", ShortScale), TestCase("--1", ShortScale), TestCase("1 5", ShortScale), TestCase("NaN", ShortScale), TestCase("Infinity", ShortScale)]
		[TestCase("1.5aa", ShortScale), TestCase("1.5Q", Letters), TestCase("1zz", Letters, Description = "beyond a double")]
		public void ParseDouble_RefusesAnythingElse(string text, NumberSuffixes suffixes)
		{
			Assert.IsFalse(CompactNumber.TryParseDouble(text, suffixes, out double value));
			Assert.AreEqual(0d, value);
		}

		// ------------------------------------------------------------------ parsing longs

		[TestCase("1.2345K", ShortScale, 1235L)]
		[TestCase("-1.2345K", ShortScale, -1235L)]
		[TestCase("0.5", ShortScale, 1L, Description = "halves go away from zero")]
		[TestCase("-0.5", ShortScale, -1L)]
		[TestCase("0.49", ShortScale, 0L)]
		[TestCase("1.5aa", Letters, 1_500_000_000_000_000L)]
		[TestCase("9223.372036854775807Q", ShortScale, long.MaxValue)]
		[TestCase("-9223.372036854775808Q", ShortScale, long.MinValue)]
		[TestCase("-9223.3720368547758075Q", ShortScale, long.MinValue)]
		public void ParseLong_ReadsExactly_AndRoundsToAWholeNumber(string text, NumberSuffixes suffixes, long expected)
		{
			Assert.IsTrue(CompactNumber.TryParseLong(text, suffixes, out long value));
			Assert.AreEqual(expected, value);
		}

		[TestCase("9223.372036854775808Q"), TestCase("9223.3720368547758075Q"), TestCase("-9223.372036854775809Q")]
		[TestCase("99999999999999999999"), TestCase("1e3"), TestCase("")]
		public void ParseLong_Refuses_WhatALongCantHold(string text)
		{
			Assert.IsFalse(CompactNumber.TryParseLong(text, ShortScale, out long value));
			Assert.AreEqual(0L, value);
		}

		[TestCase(0L), TestCase(1L), TestCase(999L), TestCase(1234L), TestCase(999_950L), TestCase(-2_500_000L)]
		[TestCase(123_456_789_012L), TestCase(long.MaxValue), TestCase(long.MinValue)]
		public void ParseLong_ReadsBackWhatFormatWrites_WithFifteenDecimals(long value)
		{
			string text = CompactNumber.Format(value, new CompactNumberFormat(0, 15));

			Assert.IsTrue(CompactNumber.TryParseLong(text, ShortScale, out long read), text);
			Assert.AreEqual(value, read, text);
		}

		// ------------------------------------------------------------------ allocations

		[Test]
		public void FormattingIntoASpanAndParsing_DoNotAllocate()
		{
			var format = new CompactNumberFormat(0, 2, Down, Letters, true);
			Action exercise = () => Exercise(format);

			// The first run JIT-compiles the body, which allocates; the second is measured.
			GcAllocations.Count(exercise);
			int allocations = GcAllocations.Count(exercise);

			Assert.AreEqual(0, allocations);
		}

		private static void Exercise(CompactNumberFormat format)
		{
			Span<char> text = stackalloc char[CompactNumber.MaxLength];
			for (int i = 0; i < 100; i++)
			{
				CompactNumber.TryFormat(1_234_567L * i, format, text, out _);
				CompactNumber.TryFormat(1.5e17d * i, format, text, out _);
				CompactNumber.TryFormat(2.5f * i, format, text, out _);
				CompactNumber.TryParseDouble("3.25aa".AsSpan(), Letters, out _);
				CompactNumber.TryParseLong("-1.2345K".AsSpan(), ShortScale, out _);
			}
		}
	}
}
