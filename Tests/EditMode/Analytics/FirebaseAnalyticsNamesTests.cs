using System;
using System.Text.RegularExpressions;
using AK.Services.Analytics;
using NUnit.Framework;

namespace AK.Tests.Analytics
{
	/// <summary>Firebase Analytics names: ASCII, starting with a letter, not Firebase's own, and short enough.</summary>
	public class FirebaseAnalyticsNamesTests
	{
		// Firebase's rule for a name's characters.
		private static readonly Regex ValidName = new(@"^[A-Za-z][A-Za-z0-9_]*\z");

		[TestCase("level_up", "level_up")]
		[TestCase("Level Up 2", "Level_Up_2")]
		[TestCase("a-b.c", "a_b_c")]
		[TestCase("caf\u00e9", "caf_")]
		[TestCase("go\U0001F642", "go__")]
		[TestCase("1st_win", "e_1st_win")]
		[TestCase("_hidden", "e__hidden")]
		[TestCase("\u00e9t\u00e9", "e__t_")]
		[TestCase("firebase_x", "e_firebase_x")]
		[TestCase("google_y", "e_google_y")]
		[TestCase("ga_z", "e_ga_z")]
		[TestCase("ad_click", "e_ad_click")]
		[TestCase("ad reward", "e_ad_reward")]
		[TestCase("session_start", "e_session_start")]
		[TestCase("", "event")]
		[TestCase(null, "event")]
		public void SanitizeEventName_MakesAValidName(string name, string expected)
		{
			Assert.AreEqual(expected, FirebaseAnalyticsNames.SanitizeEventName(name));
		}

		[TestCase("item id", "item_id")]
		[TestCase("error", "error")]
		[TestCase("ga_session", "e_ga_session")]
		[TestCase("2x", "e_2x")]
		[TestCase("", "parameter")]
		public void SanitizeParameterName_MakesAValidName(string name, string expected)
		{
			Assert.AreEqual(expected, FirebaseAnalyticsNames.SanitizeParameterName(name));
		}

		[TestCase("favorite_mode", "favorite_mode")]
		[TestCase("user_id", "e_user_id")]
		[TestCase("first_open_time", "e_first_open_time")]
		[TestCase("google_ads", "e_google_ads")]
		[TestCase("", "property")]
		public void SanitizeUserPropertyName_MakesAValidName(string name, string expected)
		{
			Assert.AreEqual(expected, FirebaseAnalyticsNames.SanitizeUserPropertyName(name));
		}

		[Test]
		public void AValidName_ComesBackAsIs()
		{
			const string name = "meeple_chat";
			Assert.AreSame(name, FirebaseAnalyticsNames.SanitizeEventName(name));
			Assert.AreSame(name, FirebaseAnalyticsNames.SanitizeParameterName(name));
			Assert.AreSame(name, FirebaseAnalyticsNames.SanitizeUserPropertyName(name));
		}

		[Test]
		public void Names_AreCutToTheirMaximumLength()
		{
			string name = new string('x', 50);
			Assert.AreEqual(new string('x', FirebaseAnalyticsNames.MaxEventNameLength), FirebaseAnalyticsNames.SanitizeEventName(name));
			Assert.AreEqual(new string('x', FirebaseAnalyticsNames.MaxParameterNameLength), FirebaseAnalyticsNames.SanitizeParameterName(name));
			Assert.AreEqual(new string('x', FirebaseAnalyticsNames.MaxUserPropertyNameLength), FirebaseAnalyticsNames.SanitizeUserPropertyName(name));
		}

		[Test]
		public void APrefixedName_IsCutAgain()
		{
			Assert.AreEqual("e_1" + new string('x', 37), FirebaseAnalyticsNames.SanitizeEventName("1" + new string('x', 50)));
		}

		[Test]
		public void ANameCutToAReservedOne_IsPrefixed()
		{
			// Cut to 24 characters, it is lifetime_user_engagement.
			Assert.AreEqual("e_lifetime_user_engageme", FirebaseAnalyticsNames.SanitizeUserPropertyName("lifetime_user_engagement_days"));
		}

		[Test]
		public void SanitizedNames_AlwaysFollowFirebaseRules()
		{
			for (int c = 1; c < 0x3100; c++)
			{
				string inside = "a" + (char)c + "b";
				string first  = (char)c + "x";
				AssertValid(inside, FirebaseAnalyticsNames.SanitizeEventName(inside), FirebaseAnalyticsNames.MaxEventNameLength);
				AssertValid(first, FirebaseAnalyticsNames.SanitizeEventName(first), FirebaseAnalyticsNames.MaxEventNameLength);
				AssertValid(first, FirebaseAnalyticsNames.SanitizeParameterName(first), FirebaseAnalyticsNames.MaxParameterNameLength);
				AssertValid(first, FirebaseAnalyticsNames.SanitizeUserPropertyName(first), FirebaseAnalyticsNames.MaxUserPropertyNameLength);
			}

			AssertValid("a lone surrogate", FirebaseAnalyticsNames.SanitizeEventName("\ud83d"), FirebaseAnalyticsNames.MaxEventNameLength);
		}

		private static void AssertValid(string name, string sanitized, int maxLength)
		{
			bool valid = sanitized.Length <= maxLength && ValidName.IsMatch(sanitized)
				&& !sanitized.StartsWith("firebase_", StringComparison.Ordinal)
				&& !sanitized.StartsWith("google_", StringComparison.Ordinal)
				&& !sanitized.StartsWith("ga_", StringComparison.Ordinal);
			if (!valid)
			{
				Assert.Fail($"'{name}' gave '{sanitized}'");
			}
		}
	}
}
