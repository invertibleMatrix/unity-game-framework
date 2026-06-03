using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;

namespace AK.Services.Analytics.Providers
{
	/// <summary>
	/// Base class for analytics providers with common functionality.
	/// </summary>
	public abstract class BaseAnalyticsProvider : IAnalyticsProvider
	{
		protected bool                _isEnabled     = true;
		protected bool                _isInitialized = false;
		protected AnalyticsMeta _metaDataRepository;

		public abstract string ProviderName { get; }
		public          bool   IsEnabled    => _isEnabled && _isInitialized;

		public virtual void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config)
		{
			_metaDataRepository = analyticsMeta;
		}

		public virtual void TrackEvent(UID eventId, Dictionary<ParameterName, object> parameters) { }

		public abstract void TrackEvent(string eventName, Dictionary<string, object> parameters);

		public abstract void TrackPurchase(string itemID, double price, string currency);

		public abstract void TrackAdImpression(string placementID, string adType);

		public abstract void TrackAdClick(string placementID, string adType);

		public abstract void TrackAdReward(string placementID, string rewardType, int rewardAmount);

		public abstract void SetUserProperty(string propertyName, string value);

		public abstract void SetUserID(string userID);

		public abstract void Flush();

		public virtual void SetEnabled(bool enabled)
		{
			_isEnabled = enabled;
		}

		/// <summary>
		/// Helper method to convert parameters to a string for logging.
		/// </summary>
		protected string ParametersToString(Dictionary<string, object> parameters)
		{
			if (parameters == null || parameters.Count == 0)
			{
				return "{}";
			}

			var pairs = new List<string>();
			foreach (var kvp in parameters)
			{
				pairs.Add($"{kvp.Key}={kvp.Value}");
			}

			return $"{{{string.Join(", ", pairs)}}}";
		}

		public virtual Dictionary<string, object> StringifyParameters(Dictionary<ParameterName, object> parametere)
		{
			Dictionary<string, object> content = new();
			foreach (var kvp in parametere)
			{
				content.Add(kvp.Key.ToString(), kvp.Value);
			}

			return content;
		}
	}
}