using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;

namespace AK.Services.Analytics.Providers
{
	public interface IAnalyticsProvider
	{
		string ProviderName { get; }

		bool IsEnabled { get; }

		/// <summary>
		/// Called once before <see cref="Initialize"/> with the run's options and the game's
		/// analytics vocabulary, which is never null.
		/// </summary>
		void Configure(AnalyticsInitOptions options, AnalyticsTaxonomy taxonomy);

		/// <summary>
		/// Called at the top of boot, before identity or session context exists. Attribution
		/// SDKs start their install measurement here; analytics providers ignore it and wait
		/// for <see cref="Initialize"/>.
		/// </summary>
		void EarlyStart();

		void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config);

		void Track(AnalyticsEvent evt);

		void TrackEvent(Uid<AnalyticsEventDefinition> eventId, Dictionary<ParameterName, object> parameters);

		void TrackEvent(string eventName, Dictionary<string, object> parameters);

		void TrackPurchase(string itemID, double price, string currency);

		void TrackAdImpression(string placementID, string adType);

		void TrackAdClick(string placementID, string adType);

		void TrackAdReward(string placementID, string rewardType, int rewardAmount);

		void SetUserProperty(string propertyName, string value);

		void SetUserID(string userID);

		void SetCustomDimension(int index, string value);

		void Flush();

		void SetEnabled(bool enabled);

		Dictionary<string, object> StringifyParameters(Dictionary<ParameterName, object> parameters);
	}
}
