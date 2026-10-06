using AK.Kernel.RemoteConfig;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class RemoteValueTextTests
	{
		// ------------------------------------------------------------------ booleans

		[TestCase("true"), TestCase("TRUE"), TestCase("t"), TestCase("Yes"), TestCase("y"), TestCase("on"), TestCase("1"), TestCase(" true\n")]
		public void Bool_ReadsTheTrueWords_InAnyCase(string text)
		{
			Assert.IsTrue(RemoteValueText.TryParseBool(text, out bool value));
			Assert.IsTrue(value);
		}

		[TestCase("false"), TestCase("False"), TestCase("f"), TestCase("NO"), TestCase("n"), TestCase("off"), TestCase("0"), TestCase("\tfalse ")]
		public void Bool_ReadsTheFalseWords_InAnyCase(string text)
		{
			Assert.IsTrue(RemoteValueText.TryParseBool(text, out bool value));
			Assert.IsFalse(value);
		}

		[TestCase((string)null), TestCase(""), TestCase("   "), TestCase("2"), TestCase("tru"), TestCase("truex"), TestCase("yes please")]
		public void Bool_RefusesAnythingElse_EmptyTextIncluded(string text)
		{
			Assert.IsFalse(RemoteValueText.TryParseBool(text, out bool value));
			Assert.IsFalse(value);
		}

		[Test]
		public void Bool_IsWrittenAsAWord()
		{
			Assert.AreEqual("true", RemoteValueText.Format(true));
			Assert.AreEqual("false", RemoteValueText.Format(false));
		}

		// ------------------------------------------------------------------ numbers

		[TestCase("42", 42), TestCase("-7", -7), TestCase("+8", 8), TestCase(" 9 ", 9)]
		public void Int_ReadsSignedWholeNumbers(string text, int expected)
		{
			Assert.IsTrue(RemoteValueText.TryParseInt(text, out int value));
			Assert.AreEqual(expected, value);
		}

		[TestCase((string)null), TestCase(""), TestCase("1,000"), TestCase("1.0"), TestCase("1e3"), TestCase("2147483648"), TestCase("0x10")]
		public void Int_RefusesFractionsSeparatorsAndOverflow(string text)
		{
			Assert.IsFalse(RemoteValueText.TryParseInt(text, out int value));
			Assert.AreEqual(0, value);
		}

		[Test]
		public void Long_ReadsBeyondTheIntRange()
		{
			Assert.IsTrue(RemoteValueText.TryParseLong("9007199254740993", out long value));
			Assert.AreEqual(9007199254740993L, value);
			Assert.AreEqual("9007199254740993", RemoteValueText.Format(value));
		}

		[TestCase("1.5", 1.5d), TestCase("-2e3", -2000d), TestCase(".25", 0.25d), TestCase(" 3 ", 3d)]
		public void Reals_ReadDecimalPointsAndExponents(string text, double expected)
		{
			Assert.IsTrue(RemoteValueText.TryParseDouble(text, out double d));
			Assert.AreEqual(expected, d);
			Assert.IsTrue(RemoteValueText.TryParseFloat(text, out float f));
			Assert.AreEqual((float)expected, f);
		}

		[TestCase((string)null), TestCase(""), TestCase("1,5"), TestCase("1,000.5"), TestCase("NaN"), TestCase("Infinity"), TestCase("-Infinity"), TestCase("1e999")]
		public void Reals_RefuseCommasNaNAndInfinities(string text)
		{
			Assert.IsFalse(RemoteValueText.TryParseDouble(text, out double d));
			Assert.AreEqual(0d, d);
			Assert.IsFalse(RemoteValueText.TryParseFloat(text, out float f));
			Assert.AreEqual(0f, f);
		}

		[TestCase("de-DE"), TestCase("fr-FR"), TestCase("ar-SA")]
		public void Numbers_ReadAndWriteTheSame_OnADeviceInAnotherCulture(string culture)
		{
			using (new CultureScope(culture))
			{
				Assert.IsTrue(RemoteValueText.TryParseDouble("1.5", out double d));
				Assert.AreEqual(1.5d, d);
				Assert.IsFalse(RemoteValueText.TryParseDouble("1,5", out _), "a decimal comma is refused everywhere");
				Assert.AreEqual("1.5", RemoteValueText.Format(1.5d));
				Assert.AreEqual("1.5", RemoteValueText.Format(1.5f));
				Assert.AreEqual("-1234567", RemoteValueText.Format(-1234567));
			}
		}

		[Test]
		public void Reals_AreWrittenAsTheShortestTextThatReadsBack()
		{
			Assert.AreEqual("0.1", RemoteValueText.Format(0.1d));
			Assert.AreEqual("0.1", RemoteValueText.Format(0.1f));
			Assert.AreEqual("2.5", RemoteValueText.Format(2.5f));
		}

		[TestCase(0.1d), TestCase(1d / 3d), TestCase(0.6822871999174d, Description = "Mono's \"R\" writes text that doesn't read back"), TestCase(double.MaxValue), TestCase(double.Epsilon), TestCase(-123456789.125d)]
		public void Double_ReadsBackAsTheSameValue(double value)
		{
			Assert.IsTrue(RemoteValueText.TryParseDouble(RemoteValueText.Format(value), out double back));
			Assert.AreEqual(value, back);
		}

		[TestCase(0.1f), TestCase(1f / 3f), TestCase(float.MaxValue), TestCase(float.Epsilon), TestCase(16777216f), TestCase(-0.3f)]
		public void Float_ReadsBackAsTheSameValue(float value)
		{
			Assert.IsTrue(RemoteValueText.TryParseFloat(RemoteValueText.Format(value), out float back));
			Assert.AreEqual(value, back);
		}

		// ------------------------------------------------------------------ JSON

		[Test]
		public void UnwrapJson_LeavesAnObjectAsItIs()
		{
			const string json = "{\"on\":true}";

			Assert.AreSame(json, RemoteValueText.UnwrapJson(json, out bool repaired));
			Assert.IsFalse(repaired);
		}

		[Test]
		public void UnwrapJson_TrimsWhitespaceAndAByteOrderMark()
		{
			Assert.AreEqual("{\"a\":1}", RemoteValueText.UnwrapJson("﻿  {\"a\":1}\n", out bool repaired));
			Assert.IsFalse(repaired);
		}

		[Test]
		public void UnwrapJson_DecodesJsonInsideAJsonString()
		{
			Assert.AreEqual("{\"name\":\"café\",\"path\":\"a/b\"}", RemoteValueText.UnwrapJson("\"{\\\"name\\\":\\\"caf\\u00e9\\\",\\\"path\\\":\\\"a\\/b\\\"}\"", out bool repaired));
			Assert.IsFalse(repaired);
		}

		[Test]
		public void UnwrapJson_DecodesABrokenJsonStringLeniently_AndSaysSo()
		{
			// Quotes inside that weren't escaped; the escaped tab still decodes.
			Assert.AreEqual("{\"on\":\"a\tb\"}", RemoteValueText.UnwrapJson("\"{\"on\":\"a\\tb\"}\"", out bool repaired));
			Assert.IsTrue(repaired);
		}

		[Test]
		public void UnwrapJson_KeepsNull()
		{
			Assert.IsNull(RemoteValueText.UnwrapJson(null, out bool repaired));
			Assert.IsFalse(repaired);
		}

		// ------------------------------------------------------------------ cost

		[Test]
		public void Reading_DoesNotAllocate()
		{
			int Window() => GcAllocations.Count(() =>
			{
				for (int i = 0; i < 1000; i++)
				{
					RemoteValueText.TryParseBool(" Yes ", out _);
					RemoteValueText.TryParseInt("-42", out _);
					RemoteValueText.TryParseLong("9007199254740993", out _);
					RemoteValueText.TryParseFloat("1.5e3", out _);
					RemoteValueText.TryParseDouble("0.25", out _);
					RemoteValueText.UnwrapJson("{\"on\":true}", out _);
				}
			});

			Window();
			Assert.AreEqual(0, Window(), "reading remote values allocated");
		}
	}
}
