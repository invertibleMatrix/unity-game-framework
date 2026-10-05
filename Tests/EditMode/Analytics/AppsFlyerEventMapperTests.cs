using System.Collections.Generic;
using AK.CoreDomain.Analytics;
using AK.Services.Analytics;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Analytics
{
	/// <summary>What reaches AppsFlyer: purchases, rewarded views, ad revenue, and the design events a game selects.</summary>
	public class AppsFlyerEventMapperTests
	{
		[Test]
		public void RewardReceived_MapsToAdView_WithPlacementAndNetwork()
		{
			var evt = AnalyticsEvent.Ad(
				AnalyticsAdAction.RewardReceived,
				"rewarded",
				"continue_after_death",
				parameters: new Dictionary<string, object> { { "network", "Google AdMob" } });

			Assert.IsTrue(AppsFlyerEventMapper.TryMapEvent(evt, null, out string name, out Dictionary<string, string> values));
			Assert.AreEqual("af_ad_view", name);
			Assert.AreEqual("rewarded", values["af_adrev_ad_type"]);
			Assert.AreEqual("continue_after_death", values["placement"]);
			Assert.AreEqual("Google AdMob", values["af_adrev_network_name"]);
		}

		[Test]
		public void AdShowAndFailures_AreNotInAppEvents()
		{
			Assert.IsFalse(AppsFlyerEventMapper.TryMapEvent(AnalyticsEvent.Ad(AnalyticsAdAction.Show, "interstitial", "p", revenue: 0.01), null, out _, out _));
			Assert.IsFalse(AppsFlyerEventMapper.TryMapEvent(AnalyticsEvent.Ad(AnalyticsAdAction.FailedShow, "rewarded", "p", failReason: "no_fill"), null, out _, out _));
			Assert.IsFalse(AppsFlyerEventMapper.TryMapEvent(AnalyticsEvent.Ad(AnalyticsAdAction.Clicked, "rewarded", "p"), null, out _, out _));
		}

		[Test]
		public void ShowWithRevenue_MapsToAdRevenue_InUsd()
		{
			var evt = AnalyticsEvent.Ad(
				AnalyticsAdAction.Show,
				"rewarded",
				"continue_after_death",
				revenue: 0.0123,
				parameters: new Dictionary<string, object> { { "network", "Unity Ads" }, { "placement", "ignored_when_envelope_has_one" } });

			Assert.IsTrue(AppsFlyerEventMapper.TryMapAdRevenue(evt, out AppsFlyerAdRevenue revenue));
			Assert.AreEqual("applovin", revenue.MediationSdk);
			Assert.AreEqual("Unity Ads", revenue.MonetizationNetwork);
			Assert.AreEqual("USD", revenue.Currency);
			Assert.AreEqual(0.0123, revenue.Revenue, 1e-9);
			Assert.AreEqual("rewarded", revenue.AdditionalParameters["ad_type"]);
			Assert.AreEqual("continue_after_death", revenue.AdditionalParameters["placement"]);
		}

		[Test]
		public void ShowWithoutRevenue_IsNotAdRevenue()
		{
			Assert.IsFalse(AppsFlyerEventMapper.TryMapAdRevenue(AnalyticsEvent.Ad(AnalyticsAdAction.Show, "rewarded", "p"), out _));
			Assert.IsFalse(AppsFlyerEventMapper.TryMapAdRevenue(AnalyticsEvent.Ad(AnalyticsAdAction.Show, "rewarded", "p", revenue: 0), out _));
			Assert.IsFalse(AppsFlyerEventMapper.TryMapAdRevenue(AnalyticsEvent.Ad(AnalyticsAdAction.RewardReceived, "rewarded", "p", revenue: 0.5), out _));
		}

		[Test]
		public void MonetizationNetwork_FallsBackToMediationSdk()
		{
			Assert.IsTrue(AppsFlyerEventMapper.TryMapAdRevenue(AnalyticsEvent.Ad(AnalyticsAdAction.Show, "banner", "p", revenue: 0.001), out AppsFlyerAdRevenue revenue));
			Assert.AreEqual("applovin", revenue.MonetizationNetwork);
		}

		[Test]
		public void Purchase_MapsToAfPurchase_InvariantPrice()
		{
			var evt = new AnalyticsEvent
			{
				Id = "business",
				Kind = AnalyticsEventKind.Business,
				ItemId = "gems_100",
				Price = 4.99,
				Currency = "EUR",
				CartType = "iap"
			};

			string name;
			Dictionary<string, string> values;
			using (new CultureScope("de-DE"))
			{
				Assert.IsTrue(AppsFlyerEventMapper.TryMapEvent(evt, null, out name, out values));
			}

			Assert.AreEqual("af_purchase", name);
			Assert.AreEqual("4.99", values["af_revenue"]);
			Assert.AreEqual("EUR", values["af_currency"]);
			Assert.AreEqual("gems_100", values["af_content_id"]);
			Assert.AreEqual("iap", values["af_content_type"]);
			Assert.AreEqual("1", values["af_quantity"]);
		}

		[Test]
		public void ProgressionResourceAndError_AreDropped()
		{
			Assert.IsFalse(AppsFlyerEventMapper.TryMapEvent(AnalyticsEvent.Progression(AnalyticsProgressionStatus.Start, "World", "1"), new SelectAll(), out _, out _));
			Assert.IsFalse(AppsFlyerEventMapper.TryMapEvent(new AnalyticsEvent { Kind = AnalyticsEventKind.Resource }, new SelectAll(), out _, out _));
			Assert.IsFalse(AppsFlyerEventMapper.TryMapEvent(new AnalyticsEvent { Kind = AnalyticsEventKind.Error }, new SelectAll(), out _, out _));
			Assert.IsFalse(AppsFlyerEventMapper.TryMapEvent(null, new SelectAll(), out _, out _));
		}

		[Test]
		public void DesignEvents_WithoutASelector_AreDropped()
		{
			Assert.IsFalse(AppsFlyerEventMapper.TryMapEvent(AnalyticsEvent.Design("level:complete:1"), null, out string name, out Dictionary<string, string> values));
			Assert.IsNull(name);
			Assert.IsNull(values);
		}

		[Test]
		public void DesignEvents_GoToTheSelector()
		{
			var selector = new SelectAll { Values = new Dictionary<string, string> { { "af_level", "3" } } };
			AnalyticsEvent evt = AnalyticsEvent.Design("level:complete:3");

			Assert.IsTrue(AppsFlyerEventMapper.TryMapEvent(evt, selector, out string name, out Dictionary<string, string> values));

			Assert.AreSame(evt, selector.Event);
			CollectionAssert.AreEqual(new[] { "level", "complete", "3" }, selector.Parts);
			Assert.AreEqual("selected", name);
			Assert.AreSame(selector.Values, values);
		}

		[Test]
		public void ASelectionWithoutValues_SendsEmptyValues_AndOneWithoutAName_IsDropped()
		{
			Assert.IsTrue(AppsFlyerEventMapper.TryMapEvent(AnalyticsEvent.Design("a"), new SelectAll(), out _, out Dictionary<string, string> values));
			CollectionAssert.IsEmpty(values);

			Assert.IsFalse(AppsFlyerEventMapper.TryMapEvent(AnalyticsEvent.Design("a"), new SelectAll { Name = "" }, out string name, out values));
			Assert.IsNull(name);
			Assert.IsNull(values);
		}

		private sealed class SelectAll : IAppsFlyerEventSelector
		{
			public string Name = "selected";
			public Dictionary<string, string> Values;
			public AnalyticsEvent Event;
			public List<string> Parts;

			public bool TrySelect(AnalyticsEvent evt, DesignEventParts parts, out string eventName, out Dictionary<string, string> values)
			{
				Event = evt;
				Parts = new List<string>(parts);
				eventName = Name;
				values = Values;
				return true;
			}
		}
	}
}
