#if UGFW_FIREBASE_ANALYTICS
using System;
using System.Collections.Generic;
using AK.Core;
using AK.Services;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using UnityEngine;

namespace AK.Services.Analytics.Providers
{
	/// <summary>
	/// Firebase Analytics provider. Add it with
	/// <see cref="FirebaseAnalyticsBuilderExtensions.UseFirebase"/>.
	///
	/// <para>Compiled only with com.google.firebase.analytics installed. Configure Firebase in the
	/// project (google-services.json for Android, GoogleService-Info.plist for iOS). Events go to
	/// Firebase once the <see cref="IFirebaseInitializationService"/> reports it available, and are
	/// dropped before then; the user id waits for it. Event, parameter and user property names are
	/// made valid with <see cref="FirebaseAnalyticsNames"/>. Firebase doesn't run on WebGL, where
	/// the provider does nothing.</para>
	/// </summary>
	public class FirebaseAnalyticsProvider : BaseAnalyticsProvider
	{
		public override string ProviderName => "Firebase";

		// Not ad_click and ad_reward: Firebase reserves those names and drops the events.
		private const string AD_IMPRESSION_EVENT = "ad_impression";
		private const string AD_CLICK_EVENT = "ad_clicked";
		private const string AD_REWARD_EVENT = "ad_rewarded";

		private readonly IFirebaseInitializationService _firebase;

		// Whether _pendingUserId has yet to go to Firebase.
		private bool _userIdPending;

		/// <param name="firebase">Firebase's initialization, which says when Firebase is available.</param>
		/// <exception cref="ArgumentNullException"><paramref name="firebase"/> is null.</exception>
		public FirebaseAnalyticsProvider(IFirebaseInitializationService firebase)
		{
			_firebase = firebase ?? throw new ArgumentNullException(nameof(firebase));
		}

		public override void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config)
		{
			base.Initialize(analyticsMeta, config);
			_isInitialized = true;
			_userIdPending = !string.IsNullOrEmpty(_pendingUserId);

			if (!_firebase.CheckAvailable())
			{
				Debug.Log($"[{ProviderName}] Firebase isn't available yet; events are dropped until it is.");
			}
		}

		// Firebase may become available after the provider initializes, so it is asked each time.
		// The first time it is, the user id set before then goes to Firebase.
		private bool Ready()
		{
			if (!_isEnabled || !_isInitialized || !_firebase.CheckAvailable())
			{
				return false;
			}

			if (_userIdPending)
			{
				_userIdPending = false;
				SendUserId(_pendingUserId);
			}

			return true;
		}

		public override void Track(AnalyticsEvent evt)
		{
			if (evt == null || !Ready())
			{
				return;
			}

			Dictionary<string, object> parameters = evt.Parameters != null
				? new Dictionary<string, object>(evt.Parameters)
				: new Dictionary<string, object>();
			parameters["kind"] = evt.Kind.ToString();
			if (evt.Value.HasValue)
			{
				parameters["value"] = evt.Value.Value;
			}

			TrackEvent(string.IsNullOrEmpty(evt.Id) ? "event" : evt.Id, parameters);
		}

		public override void TrackEvent(Uid<AnalyticsEventDefinition> eventId, Dictionary<ParameterName, object> parameters)
		{
			if (!Ready())
			{
				return;
			}

			base.TrackEvent(eventId, parameters);
		}

		public override void TrackEvent(string eventName, Dictionary<string, object> parameters)
		{
			if (!Ready())
			{
				return;
			}

#if !UNITY_WEBGL
			try
			{
				var firebaseParams = ConvertToFirebaseParameters(parameters);
				Firebase.Analytics.FirebaseAnalytics.LogEvent(FirebaseAnalyticsNames.SanitizeEventName(eventName), firebaseParams);

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] Event tracked: {eventName} with {parameters?.Count ?? 0} parameters");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[{ProviderName}] Failed to track event '{eventName}': {ex.Message}");
			}
#endif
		}

		public override void TrackPurchase(string itemID, double price, string currency)
		{
			if (!Ready())
			{
				return;
			}

#if !UNITY_WEBGL
			try
			{
				var parameters = new Firebase.Analytics.Parameter[]
				{
					new Firebase.Analytics.Parameter("item_id", itemID),
					new Firebase.Analytics.Parameter("value", price),
					new Firebase.Analytics.Parameter("currency", currency.ToUpperInvariant())
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
#endif
		}

		public override void TrackAdImpression(string placementID, string adType)
		{
			if (!Ready())
			{
				return;
			}

#if !UNITY_WEBGL
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
#endif
		}

		public override void TrackAdClick(string placementID, string adType)
		{
			if (!Ready())
			{
				return;
			}

#if !UNITY_WEBGL
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
#endif
		}

		public override void TrackAdReward(string placementID, string rewardType, int rewardAmount)
		{
			if (!Ready())
			{
				return;
			}

#if !UNITY_WEBGL
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
#endif
		}

		public override void SetUserProperty(string propertyName, string value)
		{
			if (!Ready())
			{
				return;
			}

#if !UNITY_WEBGL
			try
			{
				Firebase.Analytics.FirebaseAnalytics.SetUserProperty(FirebaseAnalyticsNames.SanitizeUserPropertyName(propertyName), value);

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] User property set: {propertyName} = {value}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Failed to set user property: {ex.Message}");
			}
#endif
		}

		/// <summary>Sets the user id now, or once Firebase is available. Null or empty is ignored.</summary>
		public override void SetUserID(string userID)
		{
			_pendingUserId = userID;
			_userIdPending = !string.IsNullOrEmpty(userID);
			Ready(); // sends it if Firebase is available
		}

		/// <summary>Firebase batches and sends events itself.</summary>
		public override void Flush()
		{
		}

		private void SendUserId(string userId)
		{
#if !UNITY_WEBGL
			try
			{
				Firebase.Analytics.FirebaseAnalytics.SetUserId(userId);

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[{ProviderName}] User ID set: {userId}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Failed to set user ID: {ex.Message}");
			}
#endif
		}

#if !UNITY_WEBGL
		/// <summary>
		/// Converts Dictionary{string, object} to Firebase Parameter array, with valid names.
		/// </summary>
		private static Firebase.Analytics.Parameter[] ConvertToFirebaseParameters(Dictionary<string, object> parameters)
		{
			if (parameters == null || parameters.Count == 0)
			{
				return Array.Empty<Firebase.Analytics.Parameter>();
			}

			var firebaseParams = new Firebase.Analytics.Parameter[parameters.Count];
			int count = 0;

			foreach (var kvp in parameters)
			{
				var paramName = FirebaseAnalyticsNames.SanitizeParameterName(kvp.Key);
				firebaseParams[count++] = kvp.Value switch
				{
					null               => new Firebase.Analytics.Parameter(paramName, "null"),
					string stringValue => new Firebase.Analytics.Parameter(paramName, stringValue),
					int intValue       => new Firebase.Analytics.Parameter(paramName, intValue),
					long longValue     => new Firebase.Analytics.Parameter(paramName, longValue),
					float floatValue   => new Firebase.Analytics.Parameter(paramName, floatValue),
					double doubleValue => new Firebase.Analytics.Parameter(paramName, doubleValue),
					bool boolValue     => new Firebase.Analytics.Parameter(paramName, boolValue ? "true" : "false"),
					var value          => new Firebase.Analytics.Parameter(paramName, value.ToString())
				};
			}

			return firebaseParams;
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
		/// Maps a ParameterName to a Firebase Analytics parameter key: Firebase's standard name
		/// where it has one (level, value, item_id, item_name, the strings its
		/// FirebaseAnalytics.Parameter* constants hold), and a snake_case name otherwise.
		/// </summary>
		private string MapParameterNameToFirebaseKey(ParameterName parameterName)
		{
			return parameterName switch
			{
				ParameterName.None            => "parameter",
				ParameterName.Platform        => "platform",
				ParameterName.DeviceModel     => "device_model",
				ParameterName.LevelNumber     => "level",
				ParameterName.FailReason      => "fail_reason",
				ParameterName.Duration        => "value",
				ParameterName.SessionDuration => "session_duration",
				ParameterName.Attempts        => "attempts",
				ParameterName.Name            => "item_name",
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
			return AnalyticsNameUtility.ToSnakeCase(input);
		}
	}
}
#endif
