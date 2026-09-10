using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using AK.Services.Analytics;

namespace AK.Services.Analytics.Providers
{
	public abstract class BaseAnalyticsProvider : IAnalyticsProvider
	{
		protected bool _isEnabled = true;
		protected bool _isInitialized;
		protected AnalyticsMeta _metaDataRepository;
		protected AnalyticsInitOptions _options;
		protected string _pendingUserId;

		public abstract string ProviderName { get; }
		public bool IsEnabled => _isEnabled && _isInitialized;

		public virtual void Configure(AnalyticsInitOptions options)
		{
			_options = options;
			if (options != null && !string.IsNullOrEmpty(options.UserId))
			{
				_pendingUserId = options.UserId;
			}
		}

		public virtual void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config)
		{
			_metaDataRepository = analyticsMeta;
			if (config != null && config.TryGetValue("userId", out string userId) && !string.IsNullOrEmpty(userId))
			{
				_pendingUserId = userId;
			}
		}

		public virtual void Track(AnalyticsEvent evt)
		{
			if (evt == null)
			{
				return;
			}

			TrackEvent(evt.Id, evt.Parameters);
		}

		public virtual void TrackEvent(Uid<AnalyticsEventDefinition> eventId, Dictionary<ParameterName, object> parameters)
		{
			if (_metaDataRepository == null || !_metaDataRepository.TryGetEvent(eventId, out AnalyticsEventDefinition definition) || string.IsNullOrEmpty(definition.EventID))
			{
				return;
			}

			TrackEvent(definition.EventID, StringifyParameters(parameters));
		}

		public abstract void TrackEvent(string eventName, Dictionary<string, object> parameters);

		public abstract void TrackPurchase(string itemID, double price, string currency);

		public abstract void TrackAdImpression(string placementID, string adType);

		public abstract void TrackAdClick(string placementID, string adType);

		public abstract void TrackAdReward(string placementID, string rewardType, int rewardAmount);

		public abstract void SetUserProperty(string propertyName, string value);

		public abstract void SetUserID(string userID);

		public virtual void SetCustomDimension(int index, string value)
		{
			SetUserProperty("custom_0" + index, value);
		}

		public abstract void Flush();

		public virtual void SetEnabled(bool enabled)
		{
			_isEnabled = enabled;
		}

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

		public virtual Dictionary<string, object> StringifyParameters(Dictionary<ParameterName, object> parameters)
		{
			return AnalyticsEventResolver.Stringify(parameters);
		}

		protected bool CanTrack()
		{
			return _isEnabled && _isInitialized;
		}
	}
}
