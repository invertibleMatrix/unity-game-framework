using System.Collections.Generic;
using AK.CoreDomain.Analytics;
using AK.Services.Analytics;
using NUnit.Framework;

namespace AK.Tests.Analytics
{
	/// <summary>Flat event names and properties for Mixpanel and Meta.</summary>
	public class MixpanelEventMapperTests
	{
		[Test]
		public void DesignEvent_WithoutANamer_JoinsItsParts()
		{
			MixpanelMappedEvent mapped = MixpanelEventMapper.Map(
				AnalyticsEvent.Design("level:start:w2", 3f, new Dictionary<string, object> { { "attempt", 2 } }),
				null);

			Assert.AreEqual("level_start_w2", mapped.EventName);
			Assert.AreEqual("level:start:w2", mapped.Properties["ga_event_id"]);
			Assert.AreEqual(3f, mapped.Properties["value"]);
			Assert.AreEqual(2, mapped.Properties["attempt"]);
			Assert.AreEqual(3, mapped.Properties.Count, "no part becomes a property by itself");
		}

		[Test]
		public void DesignEvent_WithNoParts_IsNamedEvent()
		{
			Assert.AreEqual("event", MixpanelEventMapper.Map(AnalyticsEvent.Design(":"), null).EventName);
			Assert.AreEqual("event", MixpanelEventMapper.Map(null, null).EventName);
		}

		[Test]
		public void TheNamer_SeesThePartsAndProperties_AndNamesTheEvent()
		{
			var namer = new RecordingNamer("named");

			MixpanelMappedEvent mapped = MixpanelEventMapper.Map(
				AnalyticsEvent.Design("a:b", 1f, new Dictionary<string, object> { { "p", "q" } }),
				namer);

			Assert.AreEqual("named", mapped.EventName);
			CollectionAssert.AreEqual(new[] { "a", "b" }, namer.Parts);
			Assert.AreEqual("a:b", namer.Properties["ga_event_id"]);
			Assert.AreEqual(1f, namer.Properties["value"]);
			Assert.AreEqual("q", namer.Properties["p"]);
			Assert.AreSame(namer.Properties, mapped.Properties, "what the namer adds is sent");
		}

		[Test]
		public void ANamerThatDeclines_LeavesTheDefaultName()
		{
			Assert.AreEqual("a_b", MixpanelEventMapper.Map(AnalyticsEvent.Design("a:b"), new RecordingNamer(null)).EventName);
		}

		[Test]
		public void TheNamer_IsOnlyAskedAboutDesignEvents()
		{
			var namer = new RecordingNamer("named");

			MixpanelEventMapper.Map(AnalyticsEvent.Ad(AnalyticsAdAction.Show, "rewarded", "p"), namer);
			MixpanelEventMapper.Map(AnalyticsEvent.Progression(AnalyticsProgressionStatus.Start, "World"), namer);

			Assert.IsNull(namer.Parts);
		}

		[Test]
		public void Progression_FlattensStatusAndParts()
		{
			MixpanelMappedEvent mapped = MixpanelEventMapper.Map(AnalyticsEvent.Progression(
				AnalyticsProgressionStatus.Complete,
				"Age",
				"3",
				"e2"), null);

			Assert.AreEqual("progression", mapped.EventName);
			Assert.AreEqual("complete", mapped.Properties["status"]);
			Assert.AreEqual("Age", mapped.Properties["p01"]);
			Assert.AreEqual("3", mapped.Properties["p02"]);
			Assert.AreEqual("e2", mapped.Properties["p03"]);
		}

		[Test]
		public void NativeAd_MapsActionToEventName()
		{
			MixpanelMappedEvent show = MixpanelEventMapper.Map(
				AnalyticsEvent.Ad(AnalyticsAdAction.Show, "rewarded", "rewarded_continue", revenue: 0.01), null);
			Assert.AreEqual("ad_sdk_show", show.EventName);
			Assert.AreEqual("rewarded_continue", show.Properties["placement"]);
			Assert.AreEqual("rewarded", show.Properties["type"]);
			Assert.AreEqual(0.01, show.Properties["revenue"]);

			Assert.AreEqual(
				"ad_sdk_reward",
				MixpanelEventMapper.Map(AnalyticsEvent.Ad(AnalyticsAdAction.RewardReceived, "rewarded", "p"), null).EventName);
			Assert.AreEqual(
				"ad_sdk_failed",
				MixpanelEventMapper.Map(AnalyticsEvent.Ad(AnalyticsAdAction.FailedShow, "rewarded", "p"), null).EventName);
		}

		[Test]
		public void Purchase_AndResource_AndError_HaveFixedNames()
		{
			var purchase = new AnalyticsEvent { Kind = AnalyticsEventKind.Business, ItemId = "gems", Price = 0.99, Currency = "USD" };
			var resource = new AnalyticsEvent { Kind = AnalyticsEventKind.Resource, ResourceFlow = AnalyticsResourceFlow.Sink, ResourceCurrency = "gold", ResourceAmount = 2f };
			var error = new AnalyticsEvent { Kind = AnalyticsEventKind.Error, ErrorSeverity = AnalyticsErrorSeverity.Warning, ErrorMessage = "boom" };

			MixpanelMappedEvent mappedPurchase = MixpanelEventMapper.Map(purchase, null);
			MixpanelMappedEvent mappedResource = MixpanelEventMapper.Map(resource, null);
			MixpanelMappedEvent mappedError = MixpanelEventMapper.Map(error, null);

			Assert.AreEqual("purchase", mappedPurchase.EventName);
			Assert.AreEqual(0.99, mappedPurchase.Properties["price"]);
			Assert.AreEqual("resource", mappedResource.EventName);
			Assert.AreEqual("sink", mappedResource.Properties["flow"]);
			Assert.AreEqual("error", mappedError.EventName);
			Assert.AreEqual("warning", mappedError.Properties["severity"]);
		}

		private sealed class RecordingNamer : IFlatEventNamer
		{
			private readonly string _name;

			public RecordingNamer(string name)
			{
				_name = name;
			}

			public List<string> Parts { get; private set; }

			public Dictionary<string, object> Properties { get; private set; }

			public bool TryName(DesignEventParts parts, Dictionary<string, object> properties, out string eventName)
			{
				Parts = new List<string>(parts);
				Properties = properties;
				eventName = _name;
				return _name != null;
			}
		}
	}
}
