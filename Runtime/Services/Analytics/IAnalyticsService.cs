using System.Collections.Generic;
using AK.Core;
using GameplayCore.MetaData.Analytics;

namespace AK.Services
{
	/// <summary>
	/// Main interface for the Analytics Service.
	/// Provides a unified API for tracking analytics events across multiple SDK providers.
	/// </summary>
	public interface IAnalyticsService
	{
		/// <summary>
		/// Initializes the analytics service with all configured providers.
		/// </summary>
		void Initialize();

		void TrackEvent(string eventName, Dictionary<string, object> parameters);
		
		/// <summary>
		/// Tracks an event with parameters.
		/// </summary>
		/// <param name="eventID">The event ID from AnalyticsEventDefinition</param>
		/// <param name="parameters">Dictionary of parameter names and values</param>
		void TrackEvent(UID eventID, Dictionary<ParameterName, object> parameters);

		/// <summary>
		/// Tracks a monetization event (IAP purchase).
		/// </summary>
		/// <param name="itemID">The purchased item ID</param>
		/// <param name="price">The price in local currency</param>
		/// <param name="currency">The currency code (e.g., "USD")</param>
		void TrackPurchase(string itemID, double price, string currency);

		/// <summary>
		/// Tracks an ad impression.
		/// </summary>
		/// <param name="placementID">The ad placement ID</param>
		/// <param name="adType">The type of ad shown</param>
		void TrackAdImpression(string placementID, string adType);

		/// <summary>
		/// Tracks an ad click.
		/// </summary>
		/// <param name="placementID">The ad placement ID</param>
		/// <param name="adType">The type of ad clicked</param>
		void TrackAdClick(string placementID, string adType);

		/// <summary>
		/// Tracks a rewarded ad watch completion.
		/// </summary>
		/// <param name="placementID">The ad placement ID</param>
		/// <param name="rewardType">The type of reward granted</param>
		/// <param name="rewardAmount">The amount of reward granted</param>
		void TrackAdReward(string placementID, string rewardType, int rewardAmount);

		/// <summary>
		/// Sets a user property that persists across sessions.
		/// </summary>
		/// <param name="propertyName">The property name</param>
		/// <param name="value">The property value</param>
		void SetUserProperty(string propertyName, string value);

		/// <summary>
		/// Sets the user ID for cross-platform tracking.
		/// </summary>
		/// <param name="userID">The unique user ID</param>
		void SetUserID(string userID);

		/// <summary>
		/// Flushes any pending events to the analytics providers.
		/// </summary>
		void Flush();

		/// <summary>
		/// Enables or disables analytics tracking.
		/// </summary>
		/// <param name="enabled">Whether analytics should be enabled</param>
		void SetAnalyticsEnabled(bool enabled);

		/// <summary>
		/// Checks if analytics is currently enabled.
		/// </summary>
		bool IsAnalyticsEnabled { get; }
	}
}