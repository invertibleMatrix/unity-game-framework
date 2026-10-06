using System;
using System.Collections.Generic;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using mixpanel;
using UnityEngine;

namespace AK.Services.Analytics.Providers
{
	/// <summary>
	/// Mixpanel Unity SDK adapter. Lives in AK.Services.Mixpanel so AK.Services
	/// does not reference the vendor assembly. Compiled only when com.mixpanel.unity
	/// is installed (versionDefine UGFW_MIXPANEL_SDK).
	/// Tokens: Edit → Project Settings → Mixpanel (Runtime vs Debug). Editor uses Debug Token.
	/// Unity SDK requires Original ID Merge. Identify with the account's stable user id, not a
	/// per-character or per-device one.
	/// </summary>
	public class MixpanelProvider : BaseAnalyticsProvider
	{
		public override string ProviderName => "Mixpanel";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void RegisterFactory()
		{
			AnalyticsProviderFactory.Mixpanel = static () => new MixpanelProvider();
		}

		public override void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config)
		{
			base.Initialize(analyticsMeta, config);

			try
			{
				Mixpanel.Init();
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Mixpanel.Init failed: {ex.Message}");
			}

			_isInitialized = true;

			if (string.IsNullOrEmpty(ActiveToken(MixpanelSettings.Instance)))
			{
				Debug.LogWarning($"[{ProviderName}] Token is empty. Set Runtime Token (builds) and Debug Token (editor) in Edit → Project Settings → Mixpanel. Use a dedicated Mixpanel project for Debug Token.");
			}

			ApplyPendingIdentity();
			Debug.Log($"[{ProviderName}] Initialized");
		}

		public override void Track(AnalyticsEvent evt)
		{
			if (!CanTrack() || evt == null)
			{
				return;
			}

			try
			{
				MixpanelMappedEvent mapped = MixpanelEventMapper.Map(evt, _taxonomy.FlatNamer);
				Value properties = ToMixpanelValue(mapped.Properties);
				if (properties == null)
				{
					Mixpanel.Track(mapped.EventName);
				}
				else
				{
					Mixpanel.Track(mapped.EventName, properties);
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Failed to track '{evt.Id}': {ex.Message}");
			}
		}

		public override void TrackEvent(string eventName, Dictionary<string, object> parameters)
		{
			Track(AnalyticsEvent.Design(eventName, parameters: parameters));
		}

		public override void TrackPurchase(string itemID, double price, string currency)
		{
			Track(new AnalyticsEvent
			{
				Id = "business",
				Kind = AnalyticsEventKind.Business,
				ItemId = itemID,
				Price = price,
				Currency = currency,
				CartType = "iap"
			});
		}

		public override void TrackAdImpression(string placementID, string adType)
		{
			Track(AnalyticsEvent.Ad(AnalyticsAdAction.Show, adType, placementID));
		}

		public override void TrackAdClick(string placementID, string adType)
		{
			Track(AnalyticsEvent.Ad(AnalyticsAdAction.Clicked, adType, placementID));
		}

		public override void TrackAdReward(string placementID, string rewardType, int rewardAmount)
		{
			Track(AnalyticsEvent.Ad(
				AnalyticsAdAction.RewardReceived,
				"rewarded",
				placementID,
				parameters: new Dictionary<string, object>
				{
					{ "reward_type", rewardType },
					{ "reward_amount", rewardAmount }
				}));
		}

		public override void SetUserProperty(string propertyName, string value)
		{
			RegisterProperty(propertyName, value);
		}

		public override void SetUserID(string userID)
		{
			_pendingUserId = userID;
			if (!_isInitialized || string.IsNullOrEmpty(userID))
			{
				return;
			}

			Identify(userID);
		}

		public override void SetCustomDimension(int index, string value)
		{
			RegisterProperty(_taxonomy.DimensionName(index), value);
		}

		public override void Flush()
		{
			try
			{
				Mixpanel.Flush();
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Flush failed: {ex.Message}");
			}
		}

		private void ApplyPendingIdentity()
		{
			if (!string.IsNullOrEmpty(_pendingUserId))
			{
				Identify(_pendingUserId);
			}

			if (!string.IsNullOrEmpty(_options?.CustomDimension01))
			{
				SetCustomDimension(1, _options.CustomDimension01);
			}

			if (!string.IsNullOrEmpty(_options?.CustomDimension02))
			{
				SetCustomDimension(2, _options.CustomDimension02);
			}

			if (!string.IsNullOrEmpty(_options?.CustomDimension03))
			{
				SetCustomDimension(3, _options.CustomDimension03);
			}
		}

		private static string ActiveToken(MixpanelSettings settings)
		{
			if (settings == null)
			{
				return null;
			}

#if UNITY_EDITOR || DEBUG
			return settings.DebugToken;
#else
			return settings.RuntimeToken;
#endif
		}

		private static void Identify(string userId)
		{
			try
			{
				Mixpanel.Identify(userId);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[Mixpanel] Identify failed: {ex.Message}");
			}
		}

		private static void RegisterProperty(string key, string value)
		{
			if (string.IsNullOrEmpty(key) || value == null)
			{
				return;
			}

			try
			{
				Mixpanel.Register(key, value);
				Mixpanel.People.Set(key, value);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[Mixpanel] Set property '{key}' failed: {ex.Message}");
			}
		}

		private static Value ToMixpanelValue(Dictionary<string, object> properties)
		{
			if (properties == null || properties.Count == 0)
			{
				return null;
			}

			var value = new Value();
			foreach (KeyValuePair<string, object> kvp in properties)
			{
				if (string.IsNullOrEmpty(kvp.Key) || kvp.Value == null)
				{
					continue;
				}

				Value converted = ConvertValue(kvp.Value);
				if (converted != null)
				{
					value[kvp.Key] = converted;
				}
			}

			return value;
		}

		private static Value ConvertValue(object raw)
		{
			switch (raw)
			{
				case string s:
					return s;
				case bool b:
					return b;
				case byte n:
					return n;
				case sbyte n:
					return n;
				case short n:
					return n;
				case ushort n:
					return n;
				case int n:
					return n;
				case uint n:
					return n;
				case long n:
					return n;
				case ulong n:
					return n;
				case float n:
					return n;
				case double n:
					return n;
				case decimal n:
					return n;
				default:
					return raw.ToString();
			}
		}
	}
}
