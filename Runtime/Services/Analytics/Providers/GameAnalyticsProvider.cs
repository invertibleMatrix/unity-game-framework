using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using UnityEngine;

namespace AK.Services.Analytics.Providers
{
	/// <summary>
	/// GameAnalytics provider implementation.
	/// Integrates with GameAnalytics SDK.
	/// 
	/// Prerequisites:
	/// 1. Install GameAnalytics SDK via Package Manager or Asset Store
	/// 2. Add "GAME_ANALYTICS" to Scripting Define Symbols in Player Settings
	/// 3. Configure GameAnalytics with your game key and secret key
	/// </summary>
	public class GameAnalyticsProvider : BaseAnalyticsProvider
	{
		public override string ProviderName => "GameAnalytics";

		public override void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config)
		{
			base.Initialize(analyticsMeta, config);

#if GAME_ANALYTICS
			try
			{
				// GameAnalytics initializes automatically if configured in the editor
				// or you can initialize programmatically with keys
				if (config.TryGetValue("gameKey", out var gameKey) && config.TryGetValue("secretKey", out var secretKey))
				{
					GameAnalyticsSDK.GameAnalytics.Initialize();
				}
				
				_isInitialized = true;
				Debug.Log($"[{ProviderName}] Initialized successfully");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Initialization failed: {ex.Message}");
			}
#else
			_isInitialized = true; // Mark as initialized for debug logging mode
			Debug.LogWarning($"[{ProviderName}] GameAnalytics SDK not integrated. Define GAME_ANALYTICS to enable.");
#endif
		}

		public override void TrackEvent(UID eventId, Dictionary<ParameterName, object> parameters)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			var eventDefinition = _metaDataRepository?.GetEventByID(eventId);
			var eventName = eventDefinition?.EventID ?? eventId.ToString();

			// Convert parameters to Dictionary<string, object>
			var stringifiedParams = StringifyParameters(parameters);
			TrackEvent(eventName, stringifiedParams);
		}

		public override void TrackEvent(string eventName, Dictionary<string, object> parameters)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

#if GAME_ANALYTICS
			try
			{
				// GameAnalytics uses design events for custom events
				// Format: eventName:param1:param2:...
				var eventId = BuildGameAnalyticsEventId(eventName, parameters);
				
				if (parameters != null && parameters.TryGetValue("value", out var valueObj) && valueObj is float value)
				{
					GameAnalyticsSDK.GameAnalytics.NewDesignEvent(eventId, value);
				}
				else
				{
					GameAnalyticsSDK.GameAnalytics.NewDesignEvent(eventId);
				}

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] Event tracked: {eventId}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Failed to track event '{eventName}': {ex.Message}");
			}
#else
			if (Debug.isDebugBuild)
			{
				Debug.Log($"[{ProviderName}] Event: {eventName} with {ParametersToString(parameters)} (SDK not integrated)");
			}
#endif
		}

		public override void TrackPurchase(string itemID, double price, string currency)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

#if GAME_ANALYTICS
			try
			{
				// Convert price to cents/smallest currency unit
				var amountInCents = (int)(price * 100);
				
				// Map currency to ISO 4217 number (simplified - extend as needed)
				var currencyNumber = GetCurrencyNumber(currency);
				
				// For GameAnalytics, you need to provide cart type and item type
				// Using defaults here - adjust based on your needs
				GameAnalyticsSDK.GameAnalytics.NewBusinessEvent(
					currency.ToUpper(),
					amountInCents,
					"iap",
					itemID,
					"cart"
				);

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] Purchase tracked: {itemID} for {price} {currency}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Failed to track purchase: {ex.Message}");
			}
#else
			if (Debug.isDebugBuild)
			{
				Debug.Log($"[{ProviderName}] Purchase: {itemID} for {price} {currency} (SDK not integrated)");
			}
#endif
		}

		public override void TrackAdImpression(string placementID, string adType)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

#if GAME_ANALYTICS
			try
			{
				// GameAnalytics ad impression tracking
				// adType should be one of: video, rewardedVideo, interstitial, offerWall, banner
				var gaAdType = MapAdTypeToGameAnalytics(adType);
				
				GameAnalyticsSDK.GameAnalytics.NewAdEvent(
					GameAnalyticsSDK.GAResourceFlowType.Source,
					gaAdType,
					placementID,
					1 // impressions count
				);

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] Ad impression tracked: {placementID} ({adType})");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Failed to track ad impression: {ex.Message}");
			}
#else
			if (Debug.isDebugBuild)
			{
				Debug.Log($"[{ProviderName}] Ad Impression: {placementID} ({adType}) (SDK not integrated)");
			}
#endif
		}

		public override void TrackAdClick(string placementID, string adType)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

#if GAME_ANALYTICS
			try
			{
				// GameAnalytics doesn't have a native ad click event
				// Track as custom design event
				var eventId = $"ad_click:{placementID}:{adType}";
				GameAnalyticsSDK.GameAnalytics.NewDesignEvent(eventId);

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] Ad click tracked: {placementID} ({adType})");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Failed to track ad click: {ex.Message}");
			}
#else
			if (Debug.isDebugBuild)
			{
				Debug.Log($"[{ProviderName}] Ad Click: {placementID} ({adType}) (SDK not integrated)");
			}
#endif
		}

		public override void TrackAdReward(string placementID, string rewardType, int rewardAmount)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

#if GAME_ANALYTICS
			try
			{
				// Track ad reward as resource source
				GameAnalyticsSDK.GameAnalytics.NewResourceEvent(
					GameAnalyticsSDK.GAResourceFlowType.Source,
					rewardType,
					rewardAmount,
					"ad_reward",
					placementID
				);

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] Ad reward tracked: {placementID} - {rewardType} x{rewardAmount}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Failed to track ad reward: {ex.Message}");
			}
#else
			if (Debug.isDebugBuild)
			{
				Debug.Log($"[{ProviderName}] Ad Reward: {placementID} - {rewardType} x{rewardAmount} (SDK not integrated)");
			}
#endif
		}

		public override void SetUserProperty(string propertyName, string value)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

#if GAME_ANALYTICS
			try
			{
				// GameAnalytics uses custom dimensions for user properties
				// Supports up to 3 custom dimensions (01, 02, 03)
				// Map your property names to custom dimension indices
				var dimensionIndex = MapPropertyToCustomDimension(propertyName);
				
				if (dimensionIndex > 0)
				{
					GameAnalyticsSDK.GameAnalytics.SetCustomDimension01(value);
				}

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] User property set: {propertyName} = {value}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Failed to set user property: {ex.Message}");
			}
#else
			if (Debug.isDebugBuild)
			{
				Debug.Log($"[{ProviderName}] User Property: {propertyName} = {value} (SDK not integrated)");
			}
#endif
		}

		public override void SetUserID(string userID)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

#if GAME_ANALYTICS
			try
			{
				GameAnalyticsSDK.GameAnalytics.SetUserId(userID);

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] User ID set: {userID}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Failed to set user ID: {ex.Message}");
			}
#else
			if (Debug.isDebugBuild)
			{
				Debug.Log($"[{ProviderName}] User ID: {userID} (SDK not integrated)");
			}
#endif
		}

		public override void Flush()
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

#if GAME_ANALYTICS
			try
			{
				// GameAnalytics automatically batches and sends events
				// No manual flush needed, but we can force a submit
				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] Flush called (GameAnalytics handles batching automatically)");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Failed to flush: {ex.Message}");
			}
#else
			if (Debug.isDebugBuild)
			{
				Debug.Log($"[{ProviderName}] Flush (SDK not integrated)");
			}
#endif
		}

#if GAME_ANALYTICS
		/// <summary>
		/// Builds a GameAnalytics event ID string from event name and parameters.
		/// Format: eventName:param1:param2:...
		/// </summary>
		private string BuildGameAnalyticsEventId(string eventName, Dictionary<string, object> parameters)
		{
			if (parameters == null || parameters.Count == 0)
			{
				return eventName;
			}

			// GameAnalytics recommends hierarchical event naming with colons
			// event:subevent:subevent2
			var paramParts = parameters
				.Where(p => p.Key != "value") // value is handled separately
				.Select(p => SanitizeForGameAnalytics(p.Value?.ToString() ?? "null"));
			
			var eventId = string.Join(":", new[] { eventName }.Concat(paramParts));
			
			// GameAnalytics has a 64 character limit for event IDs
			if (eventId.Length > 64)
			{
				eventId = eventId.Substring(0, 64);
			}
			
			return eventId;
		}

		/// <summary>
		/// Sanitizes a string for GameAnalytics event IDs.
		/// Removes colons and other problematic characters.
		/// </summary>
		private string SanitizeForGameAnalytics(string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				return "null";
			}
			
			// Replace colons with underscores (colons are reserved for hierarchy)
			return input.Replace(':', '_').Replace(' ', '_');
		}

		/// <summary>
		/// Maps ad type strings to GameAnalytics GAAdType.
		/// </summary>
		private GameAnalyticsSDK.GAAdType MapAdTypeToGameAnalytics(string adType)
		{
			return adType?.ToLower() switch
			{
				"rewarded" or "rewardedvideo" or "rewarded_video" => GameAnalyticsSDK.GAAdType.RewardedVideo,
				"interstitial" => GameAnalyticsSDK.GAAdType.Interstitial,
				"video" => GameAnalyticsSDK.GAAdType.Video,
				"banner" => GameAnalyticsSDK.GAAdType.Banner,
				"offerwall" or "offer_wall" => GameAnalyticsSDK.GAAdType.OfferWall,
				_ => GameAnalyticsSDK.GAAdType.Undefined
			};
		}

		/// <summary>
		/// Maps property name to custom dimension index (1-3).
		/// Returns 0 if not mapped.
		/// </summary>
		private int MapPropertyToCustomDimension(string propertyName)
		{
			return propertyName?.ToLower() switch
			{
				"user_type" or "player_type" => 1,
				"user_level" or "player_level" => 1,
				"cohort" or "cohort_id" => 2,
				"tutorial_completed" => 2,
				"vip_status" or "is_vip" => 3,
				_ => 1 // Default to dimension 01
			};
		}

		/// <summary>
		/// Gets the ISO 4217 currency number for a currency code.
		/// Returns 0 for unknown currencies.
		/// </summary>
		private int GetCurrencyNumber(string currencyCode)
		{
			return currencyCode?.ToUpper() switch
			{
				"USD" => 840,
				"EUR" => 978,
				"GBP" => 826,
				"JPY" => 392,
				"CNY" => 156,
				"KRW" => 410,
				"AUD" => 36,
				"CAD" => 124,
				"CHF" => 756,
				"SEK" => 752,
				"NZD" => 554,
				"SGD" => 702,
				"HKD" => 344,
				"NOK" => 578,
				"MXN" => 484,
				"INR" => 356,
				"RUB" => 643,
				"ZAR" => 710,
				"BRL" => 986,
				"TRY" => 949,
				"AED" => 784,
				_ => 0
			};
		}
#endif

		/// <summary>
		/// Converts Dictionary{ParameterName, object} to Dictionary{string, object}.
		/// Maps ParameterName enum values to GameAnalytics-compatible string keys.
		/// </summary>
		public override Dictionary<string, object> StringifyParameters(Dictionary<ParameterName, object> parameters)
		{
			if (parameters == null)
			{
				return new Dictionary<string, object>();
			}

			var stringifiedParams = new Dictionary<string, object>();

			foreach (var kvp in parameters)
			{
				var key = MapParameterNameToGameAnalyticsKey(kvp.Key);
				stringifiedParams[key] = kvp.Value;
			}

			return stringifiedParams;
		}

		/// <summary>
		/// Maps ParameterName enum to GameAnalytics parameter keys.
		/// </summary>
		private string MapParameterNameToGameAnalyticsKey(ParameterName parameterName)
		{
			return parameterName switch
			{
				ParameterName.None => "parameter",
				ParameterName.Platform => "platform",
				ParameterName.DeviveModel => "device_model",
				ParameterName.LevelNumber => "level_number",
				ParameterName.FailReason => "fail_reason",
				ParameterName.ActiveStars => "active_stars",
				ParameterName.EarnedStars => "earned_stars",
				ParameterName.Duration => "duration",
				ParameterName.SessionDuration => "session_duration",
				ParameterName.PowerupId => "powerup_id",
				ParameterName.Attempts => "attempts",
				ParameterName.PowerupName => "powerup_name",
				ParameterName.Name => "name",
				_ => ToSnakeCase(parameterName.ToString())
			};
		}

		/// <summary>
		/// Converts string to snake_case.
		/// </summary>
		private static string ToSnakeCase(string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				return input;
			}

			var result = System.Text.RegularExpressions.Regex.Replace(
				input,
				"(?<!^)([A-Z][a-z]|[a-zA-Z])",
				"_$1"
			).ToLower();

			return result.TrimStart('_');
		}
	}
}