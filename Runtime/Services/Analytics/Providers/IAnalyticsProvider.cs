using System.Collections.Generic;
using AK.Core;
using GameplayCore.MetaData;
using GameplayCore.MetaData.Analytics;

namespace AK.Services.Analytics.Providers
{
	/// <summary>
	/// Interface for individual analytics SDK providers.
	/// Each provider (Firebase, GameAnalytics, etc.) implements this interface.
	/// </summary>
	public interface IAnalyticsProvider
	{
		/// <summary>
		/// The name of this provider (e.g., "Firebase", "GameAnalytics").
		/// </summary>
		string ProviderName { get; }

		/// <summary>
		/// Whether this provider is enabled and initialized.
		/// </summary>
		bool IsEnabled { get; }

		/// <summary>
		/// Initializes the provider with the given configuration.
		/// </summary>
		/// <param name="analyticsMeta"></param>
		/// <param name="config">Configuration dictionary for this provider</param>
		void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config);

		void TrackEvent(UID eventId, Dictionary<ParameterName, object> parameters);

		/// <summary>
		/// Tracks an event with the given name and parameters.
		/// </summary>
		/// <param name="eventName">The event name</param>
		/// <param name="parameters">Event parameters</param>
		void TrackEvent(string eventName, Dictionary<string, object> parameters);

		/// <summary>
		/// Tracks a monetization event.
		/// </summary>
		/// <param name="itemID">The purchased item ID</param>
		/// <param name="price">The price in local currency</param>
		/// <param name="currency">The currency code</param>
		void TrackPurchase(string itemID, double price, string currency);

		/// <summary>
		/// Tracks an ad impression.
		/// </summary>
		/// <param name="placementID">The ad placement ID</param>
		/// <param name="adType">The type of ad</param>
		void TrackAdImpression(string placementID, string adType);

		/// <summary>
		/// Tracks an ad click.
		/// </summary>
		/// <param name="placementID">The ad placement ID</param>
		/// <param name="adType">The type of ad</param>
		void TrackAdClick(string placementID, string adType);

		/// <summary>
		/// Tracks a rewarded ad watch.
		/// </summary>
		/// <param name="placementID">The ad placement ID</param>
		/// <param name="rewardType">The reward type</param>
		/// <param name="rewardAmount">The reward amount</param>
		void TrackAdReward(string placementID, string rewardType, int rewardAmount);

		/// <summary>
		/// Sets a user property.
		/// </summary>
		/// <param name="propertyName">The property name</param>
		/// <param name="value">The property value</param>
		void SetUserProperty(string propertyName, string value);

		/// <summary>
		/// Sets the user ID.
		/// </summary>
		/// <param name="userID">The user ID</param>
		void SetUserID(string userID);

		/// <summary>
		/// Flushes pending events.
		/// </summary>
		void Flush();

		/// <summary>
		/// Enables or disables this provider.
		/// </summary>
		/// <param name="enabled">Whether to enable</param>
		void SetEnabled(bool enabled);

		Dictionary<string, object> StringifyParameters(Dictionary<ParameterName, object> parametere);
	}
}