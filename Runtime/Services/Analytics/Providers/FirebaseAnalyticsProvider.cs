using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.Services;
using GameplayCore.MetaData;
using GameplayCore.MetaData.Analytics;
using UnityEngine;

namespace AK.Services.Analytics.Providers
{
	/// <summary>
	/// Firebase Analytics provider implementation.
	/// Integrates with Firebase Analytics SDK.
	/// 
	/// Prerequisites:
	/// 1. Install Firebase Analytics SDK via Package Manager
	/// 2. Add "FIREBASE_ANALYTICS" to Scripting Define Symbols in Player Settings
	/// 3. Configure Firebase in your project (google-services.json for Android, GoogleService-Info.plist for iOS)
	/// 4. Ensure IFirebaseInitializationService is initialized before using this provider
	/// </summary>
	public class FirebaseAnalyticsProvider : BaseAnalyticsProvider
	{
		public override string ProviderName => "Firebase";

		private const string AD_IMPRESSION_EVENT = "ad_impression";
		private const string AD_CLICK_EVENT = "ad_click";
		private const string AD_REWARD_EVENT = "ad_reward";

		private IFirebaseInitializationService _firebaseInit;

		/// <summary>
		/// Sets the Firebase initialization service. Must be called before Initialize if using Firebase.
		/// </summary>
		public void SetFirebaseInitializationService(IFirebaseInitializationService firebaseInit)
		{
			_firebaseInit = firebaseInit;
		}

		public override void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config)
		{
			base.Initialize(analyticsMeta, config);

			// Check if Firebase is available via the initialization service
			if (_firebaseInit != null && _firebaseInit.CheckAvailable())
			{
				_isInitialized = true;
				Debug.Log($"[{ProviderName}] Initialized successfully - Firebase is available");
			}
			else
			{
				_isInitialized = false;
				var reason = _firebaseInit?.UnavailableReason ?? "IFirebaseInitializationService not set";
				Debug.LogWarning($"[{ProviderName}] Firebase not available: {reason}. Analytics will be disabled.");
			}
		}

		public override void TrackEvent(UID eventId, Dictionary<ParameterName, object> parameters)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			var eventDefinition = _metaDataRepository?.GetEventByID(eventId);
			var eventName = eventDefinition?.EventID ?? eventId.ToString();

			var stringifiedParams = StringifyParameters(parameters);
			TrackEvent(eventName, stringifiedParams);
		}

		public override void TrackEvent(string eventName, Dictionary<string, object> parameters)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

#if FIREBASE_ANALYTICS
			try
			{
				var firebaseParams = ConvertToFirebaseParameters(parameters);
				Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName, firebaseParams);

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] Event tracked: {eventName} with {parameters?.Count ?? 0} parameters");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[{ProviderName}] Failed to track event '{eventName}': {ex.Message}");
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

#if FIREBASE_ANALYTICS
			try
			{
				var parameters = new Firebase.Analytics.Parameter[]
				{
					new Firebase.Analytics.Parameter("item_id", itemID),
					new Firebase.Analytics.Parameter("value", price),
					new Firebase.Analytics.Parameter("currency", currency.ToUpper())
				};

				Firebase.Analytics.FirebaseAnalytics.LogEvent("purchase", parameters);

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

#if FIREBASE_ANALYTICS
			try
			{
				var parameters = new Firebase.Analytics.Parameter[]
				{
					new Firebase.Analytics.Parameter("placement_id", placementID),
					new Firebase.Analytics.Parameter("ad_type", adType)
				};

				Firebase.Analytics.FirebaseAnalytics.LogEvent(AD_IMPRESSION_EVENT, parameters);

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

#if FIREBASE_ANALYTICS
			try
			{
				var parameters = new Firebase.Analytics.Parameter[]
				{
					new Firebase.Analytics.Parameter("placement_id", placementID),
					new Firebase.Analytics.Parameter("ad_type", adType)
				};

				Firebase.Analytics.FirebaseAnalytics.LogEvent(AD_CLICK_EVENT, parameters);

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

#if FIREBASE_ANALYTICS
			try
			{
				var parameters = new Firebase.Analytics.Parameter[]
				{
					new Firebase.Analytics.Parameter("placement_id", placementID),
					new Firebase.Analytics.Parameter("reward_type", rewardType),
					new Firebase.Analytics.Parameter("reward_amount", rewardAmount)
				};

				Firebase.Analytics.FirebaseAnalytics.LogEvent(AD_REWARD_EVENT, parameters);

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

#if FIREBASE_ANALYTICS
			try
			{
				Firebase.Analytics.FirebaseAnalytics.SetUserProperty(propertyName, value);

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

#if FIREBASE_ANALYTICS
			try
			{
				Firebase.Analytics.FirebaseAnalytics.SetUserId(userID);

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

#if FIREBASE_ANALYTICS
			try
			{
				// Firebase Analytics automatically batches and flushes events
				// This is a no-op but kept for interface consistency
				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] Flush called (Firebase handles batching automatically)");
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

#if FIREBASE_ANALYTICS
		/// <summary>
		/// Converts Dictionary{string, object} to Firebase Parameter array.
		/// Only available when Firebase SDK is integrated.
		/// </summary>
		private Firebase.Analytics.Parameter[] ConvertToFirebaseParameters(Dictionary<string, object> parameters)
		{
			if (parameters == null || parameters.Count == 0)
			{
				return Array.Empty<Firebase.Analytics.Parameter>();
			}

			var firebaseParams = new List<Firebase.Analytics.Parameter>();

			foreach (var kvp in parameters)
			{
				var paramName = kvp.Key;
				var value = kvp.Value;

				if (value == null)
				{
					firebaseParams.Add(new Firebase.Analytics.Parameter(paramName, "null"));
					continue;
				}

				switch (value)
				{
					case string stringValue:
						firebaseParams.Add(new Firebase.Analytics.Parameter(paramName, stringValue));
						break;
					case int intValue:
						firebaseParams.Add(new Firebase.Analytics.Parameter(paramName, intValue));
						break;
					case long longValue:
						firebaseParams.Add(new Firebase.Analytics.Parameter(paramName, longValue));
						break;
					case float floatValue:
						firebaseParams.Add(new Firebase.Analytics.Parameter(paramName, floatValue));
						break;
					case double doubleValue:
						firebaseParams.Add(new Firebase.Analytics.Parameter(paramName, (float)doubleValue));
						break;
					case bool boolValue:
						firebaseParams.Add(new Firebase.Analytics.Parameter(paramName, boolValue ? "true" : "false"));
						break;
					default:
						firebaseParams.Add(new Firebase.Analytics.Parameter(paramName, value.ToString()));
						break;
				}
			}

			return firebaseParams.ToArray();
		}
#endif

		/// <summary>
		/// Converts Dictionary{ParameterName, object} to Dictionary{string, object}.
		/// Maps ParameterName enum values to Firebase-compatible string keys.
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
				var key = MapParameterNameToFirebaseKey(kvp.Key);
				stringifiedParams[key] = kvp.Value;
			}

			return stringifiedParams;
		}

		/// <summary>
		/// Maps ParameterName enum to Firebase Analytics parameter keys.
		/// Uses string literals - update these to Firebase macros when SDK is integrated.
		/// </summary>
		private string MapParameterNameToFirebaseKey(ParameterName parameterName)
		{
			return parameterName switch
			{
				ParameterName.None            => "parameter",
				ParameterName.Platform        => "platform",
				ParameterName.DeviveModel     => "device_model",
				ParameterName.LevelNumber     => "level",
				ParameterName.FailReason      => "fail_reason",
				ParameterName.ActiveStars     => "active_stars",
				ParameterName.Duration        => "value",
				ParameterName.SessionDuration => "session_duration",
				ParameterName.PowerupId       => "powerup_id",
				ParameterName.Attempts        => "attempts",
				ParameterName.PowerupName     => "powerup_name",
				ParameterName.Name            => "item_name",
				ParameterName.EarnedStars     => "earned_stars",
				ParameterName.StartIntention  => "start_intention",
				ParameterName.CurrencyCode    => "currency_code",
				ParameterName.Amount          => "amount",
				ParameterName.ItemType        => "item_type",
				ParameterName.ItemId          => "item_id",
				_                             => ToSnakeCase(parameterName.ToString())
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