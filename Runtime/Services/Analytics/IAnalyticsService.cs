using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;

namespace AK.Services
{
	/// <summary>
	/// Multi-provider analytics facade.
	/// Definitions are an optional overlay (sampling, disable, mapping) — not a gate.
	/// </summary>
	public interface IAnalyticsService
	{
		/// <summary>
		/// Starts providers that must run before identity exists (attribution). Safe to call
		/// once at the top of boot; <see cref="Initialize()"/> still runs the full init later.
		/// </summary>
		void EarlyStart();

		void Initialize();
		void Initialize(AnalyticsMeta meta, AnalyticsInitOptions options = null);

		void Track(AnalyticsEvent evt);

		void TrackEvent(string eventName, Dictionary<string, object> parameters);

		void TrackEvent(AnalyticsEventDefinition definition, Dictionary<ParameterName, object> parameters);
		void TrackEvent(Uid<AnalyticsEventDefinition> eventId, Dictionary<ParameterName, object> parameters);

		void TrackDesign(string eventId, float? value = null, Dictionary<string, object> parameters = null);

		void TrackProgression(
			AnalyticsProgressionStatus status,
			string progression01,
			string progression02 = null,
			string progression03 = null,
			int? score = null,
			Dictionary<string, object> parameters = null);

		void TrackAd(
			AnalyticsAdAction action,
			string adType,
			string placement,
			string sdkName = "applovin",
			long? durationMs = null,
			string failReason = null,
			double? revenue = null,
			Dictionary<string, object> parameters = null);

		void TrackResource(
			AnalyticsResourceFlow flow,
			string currency,
			float amount,
			string itemType,
			string itemId,
			Dictionary<string, object> parameters = null);

		void TrackError(AnalyticsErrorSeverity severity, string message, Dictionary<string, object> parameters = null);

		void TrackPurchase(string itemID, double price, string currency);

		void TrackAdImpression(string placementID, string adType);

		void TrackAdClick(string placementID, string adType);

		void TrackAdReward(string placementID, string rewardType, int rewardAmount);

		void SetUserProperty(string propertyName, string value);

		void SetUserID(string userID);

		void SetCustomDimension(int index, string value);

		void Flush();

		void SetAnalyticsEnabled(bool enabled);

		bool IsAnalyticsEnabled { get; }

		bool IsInitialized { get; }
	}

	/// <summary>
	/// Per-run settings for <see cref="IAnalyticsService.Initialize(AnalyticsMeta, AnalyticsInitOptions)"/>.
	/// The custom dimensions' names and allowed values belong to the game's
	/// <see cref="AK.Services.Analytics.AnalyticsTaxonomy"/>; these only set their first values.
	/// </summary>
	public sealed class AnalyticsInitOptions
	{
		public string UserId;
		public string Build;
		public string CustomDimension01;
		public string CustomDimension02;
		public string CustomDimension03;
		public Dictionary<string, string> ProviderConfig;
	}
}
