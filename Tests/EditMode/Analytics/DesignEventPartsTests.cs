using System;
using System.Collections.Generic;
using AK.Services.Analytics;
using NUnit.Framework;

namespace AK.Tests.Analytics
{
	/// <summary>Splitting a design id into its parts, and matching prefixes.</summary>
	public class DesignEventPartsTests
	{
		[TestCase("meeple:chat:age_3", new[] { "meeple", "chat", "age_3" })]
		[TestCase("single", new[] { "single" })]
		[TestCase("a::b:", new[] { "a", "b" })]
		[TestCase(":a", new[] { "a" })]
		public void Parse_SplitsOnColons_DroppingEmptyParts(string id, string[] expected)
		{
			CollectionAssert.AreEqual(expected, DesignEventParts.Parse(id));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase(":::")]
		public void Parse_OfNoParts_IsEmpty(string id)
		{
			Assert.AreSame(DesignEventParts.Empty, DesignEventParts.Parse(id));
			Assert.AreEqual(0, DesignEventParts.Empty.Count);
		}

		[Test]
		public void At_IsNullPastEitherEnd()
		{
			DesignEventParts parts = DesignEventParts.Parse("a:b");

			Assert.AreEqual("a", parts.At(0));
			Assert.AreEqual("b", parts.At(1));
			Assert.IsNull(parts.At(2));
			Assert.IsNull(parts.At(-1));
		}

		[Test]
		public void JoinAndToString_RebuildTheId()
		{
			DesignEventParts parts = DesignEventParts.Parse("a::b:c");

			Assert.AreEqual("a_b_c", parts.Join("_"));
			Assert.AreEqual("a:b:c", parts.ToString());
			Assert.AreEqual("", DesignEventParts.Empty.Join("_"));
		}

		[Test]
		public void StartsWith_IgnoresCase_AndNeedsEveryPrefixPart()
		{
			DesignEventParts parts = DesignEventParts.Parse("Ad:Offer:shown:rewarded");

			Assert.IsTrue(parts.StartsWith("ad"));
			Assert.IsTrue(parts.StartsWith("ad", "offer"));
			Assert.IsTrue(parts.StartsWith("AD", "OFFER", "SHOWN"));
			Assert.IsFalse(parts.StartsWith("ad", "watch"));
			Assert.IsFalse(parts.StartsWith("offer"));
			Assert.IsFalse(DesignEventParts.Parse("ad").StartsWith("ad", "offer"));
			Assert.IsFalse(parts.StartsWith((string)null));
		}

		[Test]
		public void StartsWith_AnotherId()
		{
			DesignEventParts parts = DesignEventParts.Parse("funnel:onboarding:complete");

			Assert.IsTrue(parts.StartsWith(DesignEventParts.Parse("FUNNEL:onboarding")));
			Assert.IsTrue(parts.StartsWith(DesignEventParts.Parse("funnel:onboarding:complete")));
			Assert.IsFalse(parts.StartsWith(DesignEventParts.Parse("funnel:onboarding:complete:late")));
			Assert.IsFalse(parts.StartsWith(DesignEventParts.Parse("funnel:tutorial")));
			Assert.IsTrue(parts.StartsWith(DesignEventParts.Empty));
			Assert.Throws<ArgumentNullException>(() => parts.StartsWith((DesignEventParts)null));
		}

		[Test]
		public void Enumerates_InOrder()
		{
			var seen = new List<string>();
			foreach (string part in DesignEventParts.Parse("x:y:z"))
			{
				seen.Add(part);
			}

			CollectionAssert.AreEqual(new[] { "x", "y", "z" }, seen);
		}
	}
}
