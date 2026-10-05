using System;
using System.Collections.Generic;
using AK.Services.Analytics;
using NUnit.Framework;

namespace AK.Tests.Analytics
{
	/// <summary>Naming design events from a rule table, and moving id parts into properties.</summary>
	public class FlatEventNameRulesTests
	{
		[Test]
		public void FirstMatchingRuleNamesTheEvent()
		{
			var rules = new FlatEventNameRules(
				new FlatEventNameRule("shop:buy", "shop_buy"),
				new FlatEventNameRule("shop", "shop_other"));

			Assert.AreEqual("shop_buy", Name(rules, "shop:buy:sword"));
			Assert.AreEqual("shop_other", Name(rules, "shop:browse"));
			Assert.AreEqual("shop_buy", Name(rules, "SHOP:Buy"), "prefixes ignore case");
		}

		[Test]
		public void NoMatch_LeavesTheEventAndItsPropertiesAlone()
		{
			var rules = new FlatEventNameRules(new FlatEventNameRule("shop", "shop", PartProperty.Set(1, "action")));
			var properties = new Dictionary<string, object> { { "ga_event_id", "level:start" } };

			bool named = rules.TryName(DesignEventParts.Parse("level:start"), properties, out string eventName);

			Assert.IsFalse(named);
			Assert.IsNull(eventName);
			CollectionAssert.AreEquivalent(new Dictionary<string, object> { { "ga_event_id", "level:start" } }, properties);
		}

		[Test]
		public void Set_CopiesThePart_OverAParameterOfTheSameName()
		{
			var rules = new FlatEventNameRules(new FlatEventNameRule("level:start", "level_start",
				PartProperty.Set(2, "world"),
				PartProperty.Set(3, "stage")));
			var properties = new Dictionary<string, object> { { "world", "from_parameter" } };

			rules.TryName(DesignEventParts.Parse("level:start:w2:s5"), properties, out _);

			Assert.AreEqual("w2", properties["world"]);
			Assert.AreEqual("s5", properties["stage"]);
		}

		[Test]
		public void SetIfMissing_KeepsAParameterOfTheSameName()
		{
			var rules = new FlatEventNameRules(new FlatEventNameRule("ad:watch", "ad_watch", PartProperty.SetIfMissing(2, "placement")));
			var withParameter = new Dictionary<string, object> { { "placement", "from_parameter" } };
			var without = new Dictionary<string, object>();

			rules.TryName(DesignEventParts.Parse("ad:watch:continue"), withParameter, out _);
			rules.TryName(DesignEventParts.Parse("ad:watch:continue"), without, out _);

			Assert.AreEqual("from_parameter", withParameter["placement"]);
			Assert.AreEqual("continue", without["placement"]);
		}

		[Test]
		public void APartPastTheEnd_SetsNothing()
		{
			var rules = new FlatEventNameRules(new FlatEventNameRule("dice:roll", "dice_roll", PartProperty.Set(2, "age")));
			var properties = new Dictionary<string, object>();

			Assert.IsTrue(rules.TryName(DesignEventParts.Parse("dice:roll"), properties, out string eventName));

			Assert.AreEqual("dice_roll", eventName);
			CollectionAssert.IsEmpty(properties);
		}

		[Test]
		public void Rules_KeepTheirOrder_AndCannotBeChangedThroughTheArray()
		{
			var first = new FlatEventNameRule("a", "first");
			var second = new FlatEventNameRule("b", "second");
			var source = new[] { first, second };
			var rules = new FlatEventNameRules(source);

			source[0] = second;

			CollectionAssert.AreEqual(new[] { first, second }, rules.Rules);
			Assert.AreEqual("first", Name(rules, "a"));
		}

		[Test]
		public void Rule_ExposesWhatItWasBuiltFrom()
		{
			var rule = new FlatEventNameRule("Meeple:Chat", "meeple_chat", PartProperty.Set(2, "age_dim"), PartProperty.SetIfMissing(3, "kind"));

			CollectionAssert.AreEqual(new[] { "Meeple", "Chat" }, rule.Prefix);
			Assert.AreEqual("meeple_chat", rule.EventName);
			Assert.AreEqual(2, rule.Properties.Count);
			Assert.AreEqual(2, rule.Properties[0].Part);
			Assert.AreEqual("age_dim", rule.Properties[0].Name);
			Assert.IsFalse(rule.Properties[0].OnlyIfMissing);
			Assert.IsTrue(rule.Properties[1].OnlyIfMissing);
		}

		[Test]
		public void BadRules_AreRejected()
		{
			Assert.Throws<ArgumentException>(() => new FlatEventNameRule(null, "name"));
			Assert.Throws<ArgumentException>(() => new FlatEventNameRule(":", "name"));
			Assert.Throws<ArgumentException>(() => new FlatEventNameRule("a", ""));
			Assert.Throws<ArgumentException>(() => new FlatEventNameRule("a", "name", PartProperty.Set(-1, "p")));
			Assert.Throws<ArgumentException>(() => new FlatEventNameRule("a", "name", PartProperty.Set(1, "")));
			Assert.Throws<ArgumentException>(() => new FlatEventNameRule("a", "name", default(PartProperty)));
			Assert.Throws<ArgumentException>(() => new FlatEventNameRules(new FlatEventNameRule("a", "name"), null));
			Assert.Throws<ArgumentNullException>(() => new FlatEventNameRules(null));
		}

		private static string Name(IFlatEventNamer namer, string id)
		{
			return namer.TryName(DesignEventParts.Parse(id), new Dictionary<string, object>(), out string eventName) ? eventName : null;
		}
	}
}
