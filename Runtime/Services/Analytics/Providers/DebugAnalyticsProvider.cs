using System;
using System.Collections.Generic;
using AK.CoreDomain;
using UnityEngine;

namespace AK.Services.Analytics.Providers
{
	/// <summary>
	/// Debug analytics provider that logs events to the Unity console.
	/// Useful for development and testing without actual SDK integration.
	/// </summary>
	public class DebugAnalyticsProvider : BaseAnalyticsProvider
	{
		public override string ProviderName => "Debug";

		public override void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config)
		{
			base.Initialize(analyticsMeta, config);
			// Without this, IsEnabled (base: _isEnabled && _isInitialized) is always false
			// and the provider silently drops every event.
			_isInitialized = true;
			Debug.Log($"[{ProviderName}] Initialized");
		}

		public override void TrackEvent(string eventName, Dictionary<string, object> parameters)
		{
			string paramString = ParametersToString(parameters);
			Debug.Log($"[{ProviderName}] Event: {eventName} {paramString}");
		}

		public override void TrackPurchase(string itemID, double price, string currency)
		{
			Debug.Log($"[{ProviderName}] Purchase: {itemID} for {price} {currency}");
		}

		public override void TrackAdImpression(string placementID, string adType)
		{
			Debug.Log($"[{ProviderName}] Ad Impression: {placementID} ({adType})");
		}

		public override void TrackAdClick(string placementID, string adType)
		{
			Debug.Log($"[{ProviderName}] Ad Click: {placementID} ({adType})");
		}

		public override void TrackAdReward(string placementID, string rewardType, int rewardAmount)
		{
			Debug.Log($"[{ProviderName}] Ad Reward: {placementID} - {rewardType} x{rewardAmount}");
		}

		public override void SetUserProperty(string propertyName, string value)
		{
			Debug.Log($"[{ProviderName}] Set User Property: {propertyName} = {value}");
		}

		public override void SetUserID(string userID)
		{
			Debug.Log($"[{ProviderName}] Set User ID: {userID}");
		}

		public override void Flush()
		{
			Debug.Log($"[{ProviderName}] Flushed events");
		}

		public override void SetEnabled(bool enabled)
		{
			base.SetEnabled(enabled);
			Debug.Log($"[{ProviderName}] {(enabled ? "Enabled" : "Disabled")}");
		}
	}
}
