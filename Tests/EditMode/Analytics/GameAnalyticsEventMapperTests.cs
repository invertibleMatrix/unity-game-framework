using System.Text.RegularExpressions;
using AK.Services.Analytics;
using NUnit.Framework;

namespace AK.Tests.Analytics
{
	/// <summary>GameAnalytics design ids: at most five clean parts.</summary>
	public class GameAnalyticsEventMapperTests
	{
		// GameAnalytics' own check for a part (GAValidator.ValidateEventPartCharacters).
		private static readonly Regex ValidPart = new(@"^[A-Za-z0-9\s\-_\.\(\)\!\?]{1,64}$");

		[Test]
		public void BuildDesignEventId_JoinsParts_SplittingAnyWithColons()
		{
			Assert.AreEqual("dice:roll:age3", GameAnalyticsEventMapper.BuildDesignEventId("dice:roll", "age3"));
		}

		[Test]
		public void BuildDesignEventId_CapsAtFiveParts()
		{
			Assert.AreEqual("a:b:c:d:e", GameAnalyticsEventMapper.BuildDesignEventId("a", "b", "c", "d", "e", "f"));
		}

		[Test]
		public void BuildDesignEventId_SkipsEmptyParts_AndIsEventWhenNoneAreLeft()
		{
			Assert.AreEqual("a:b", GameAnalyticsEventMapper.BuildDesignEventId("a", null, "", "::b"));
			Assert.AreEqual("event", GameAnalyticsEventMapper.BuildDesignEventId());
			Assert.AreEqual("event", GameAnalyticsEventMapper.BuildDesignEventId(null, ":"));
		}

		[Test]
		public void SanitizeSegment_ReplacesReservedCharacters_AndCapsTheLength()
		{
			Assert.AreEqual("hello_world", GameAnalyticsEventMapper.SanitizeSegment("hello:world"));
			Assert.AreEqual("a_b", GameAnalyticsEventMapper.SanitizeSegment(" a b "));
			Assert.AreEqual("none", GameAnalyticsEventMapper.SanitizeSegment(null));
			Assert.AreEqual(GameAnalyticsEventMapper.MaxPartLength, GameAnalyticsEventMapper.SanitizeSegment(new string('x', 100)).Length);
		}

		[TestCase("Bus-Driver_2.0 (night)!?", "Bus-Driver_2.0_(night)!?")]
		[TestCase("caf\u00e9 owner", "caf__owner")]
		[TestCase("a,b", "a_b")]
		[TestCase("\u0663ages", "_ages")]
		[TestCase("\u00fcber", "_ber")]
		[TestCase("\u65e5\u672c", "__")]
		[TestCase("go\U0001F642", "go__")]
		[TestCase("\t\r\n x \v\f", "x")]
		[TestCase("\u00a0x\u3000", "_x_")]
		[TestCase(" \t ", "none")]
		[TestCase("", "none")]
		public void SanitizeSegment_KeepsAsciiLettersDigitsAndAllowedMarks_Only(string input, string expected)
		{
			Assert.AreEqual(expected, GameAnalyticsEventMapper.SanitizeSegment(input));
		}

		[Test]
		public void SanitizeSegment_ReturnsAValidPart_AsIs()
		{
			const string part = "meeple_chat";
			Assert.AreSame(part, GameAnalyticsEventMapper.SanitizeSegment(part));
		}

		[Test]
		public void SanitizeSegment_CutsAfterTrimming()
		{
			string padded = "  " + new string('y', 70) + "  ";
			Assert.AreEqual(new string('y', GameAnalyticsEventMapper.MaxPartLength), GameAnalyticsEventMapper.SanitizeSegment(padded));
		}

		[Test]
		public void SanitizeSegment_AlwaysPassesGameAnalyticsCheck()
		{
			for (int c = 1; c < 0x3100; c++)
			{
				string input = "a" + (char)c + "b";
				string part = GameAnalyticsEventMapper.SanitizeSegment(input);
				Assert.IsTrue(ValidPart.IsMatch(part), $"U+{c:X4} gave '{part}'");
			}

			Assert.IsTrue(ValidPart.IsMatch(GameAnalyticsEventMapper.SanitizeSegment("\ud83d")), "a lone surrogate");
		}
	}
}
