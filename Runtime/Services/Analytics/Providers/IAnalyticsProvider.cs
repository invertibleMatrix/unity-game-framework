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

		void Configure(AnalyticsInitOptions options);

		void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config);

		void Track(AnalyticsEvent evt);

		void TrackEvent(UID eventId, Dictionary<ParameterName, object> parameters);

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
