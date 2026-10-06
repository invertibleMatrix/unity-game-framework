using System.Collections.Generic;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using AK.Services;
using AK.Services.Analytics;
using AK.Services.Analytics.Providers;

namespace AK.Tests.Analytics
{
	/// <summary>
	/// A provider that records what the service hands it. Custom dimensions go through the base
	/// class too, so <see cref="UserProperties"/> shows the names the taxonomy gives them.
	/// </summary>
	public sealed class RecordingAnalyticsProvider : BaseAnalyticsProvider
	{
		public override string ProviderName => "Recording";

		public readonly List<AnalyticsEvent> Events = new();
		public readonly List<string> UserIds = new();
		public readonly List<(int Index, string Value)> Dimensions = new();
		public readonly List<(string Name, string Value)> UserProperties = new();

		public int EarlyStarts { get; private set; }
		public int Configures { get; private set; }
		public AnalyticsInitOptions Options => _options;
		public AnalyticsTaxonomy Taxonomy => _taxonomy;
		public Dictionary<string, string> Config { get; private set; }
		public bool ConfiguredBeforeInitialize { get; private set; }

		public override void Configure(AnalyticsInitOptions options, AnalyticsTaxonomy taxonomy)
		{
			base.Configure(options, taxonomy);
			Configures++;
		}

		public override void EarlyStart()
		{
			EarlyStarts++;
		}

		public override void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config)
		{
			base.Initialize(analyticsMeta, config);
			Config = config;
			ConfiguredBeforeInitialize = Configures > 0;
			_isInitialized = true;
		}

		public override void Track(AnalyticsEvent evt)
		{
			Events.Add(evt);
		}

		public override void TrackEvent(string eventName, Dictionary<string, object> parameters)
		{
			Events.Add(AnalyticsEvent.Design(eventName, parameters: parameters));
		}

		public override void TrackPurchase(string itemID, double price, string currency) { }

		public override void TrackAdImpression(string placementID, string adType) { }

		public override void TrackAdClick(string placementID, string adType) { }

		public override void TrackAdReward(string placementID, string rewardType, int rewardAmount) { }

		public override void SetUserProperty(string propertyName, string value) => UserProperties.Add((propertyName, value));

		public override void SetUserID(string userID) => UserIds.Add(userID);

		public override void SetCustomDimension(int index, string value)
		{
			Dimensions.Add((index, value));
			base.SetCustomDimension(index, value);
		}

		public override void Flush() { }
	}
}
