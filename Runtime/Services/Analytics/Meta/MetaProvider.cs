#if UGFW_FACEBOOK_SDK
using System;
using System.Collections.Generic;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using Facebook.Unity;
using UnityEngine;

namespace AK.Services.Analytics.Providers
{
	/// <summary>
	/// Meta (Facebook) SDK adapter. Mirrors analytics events to Meta as app events so CPI
	/// campaigns attribute installs and can optimize on in-app events. Lives in
	/// AK.Services.Meta so AK.Services does not reference the vendor assembly.
	/// App ID / client token: Facebook → Edit Settings (FacebookSettings.asset).
	/// Installs/activations are auto-logged by the native SDK (AutoLogAppEvents).
	/// With <see cref="MetaProviderConfig.InstallsOnlyKey"/> set, the SDK is kept for install
	/// attribution only and in-app events are dropped — AppsFlyer posts them to Meta, and Meta
	/// would count events arriving through both paths.
	/// Compiled only with UGFW_FACEBOOK_SDK, which AK.Editor's FacebookSdkDefine keeps in
	/// Assets/csc.rsp while the Facebook SDK is in the project.
	/// </summary>
	public class MetaProvider : BaseAnalyticsProvider
	{
		public override string ProviderName => "Meta";

		private const int MaxPreInitEvents = 128;

		// True once FB.Init's callback has completed and ActivateApp ran. The Unity FB SDK
		// throws "Facebook object is not yet loaded" on any call before that, so events
		// tracked during the async init window buffer in _preInitEvents and flush on ready.
		private bool _sdkReady;
		private bool _installsOnly;
		private readonly Queue<AnalyticsEvent> _preInitEvents = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void RegisterFactory()
		{
			AnalyticsProviderFactory.Meta = static () => new MetaProvider();
		}

		public override void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config)
		{
			base.Initialize(analyticsMeta, config);
			_installsOnly = MetaProviderConfig.IsInstallsOnly(config);

			// iOS: gate init on the ATT answer (owned by the GA provider) so the install /
			// activate events carry the IDFA when the user consents. Same tradeoff GA makes:
			// events tracked while the prompt is on screen buffer in _preInitEvents.
			if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.tvOS)
			{
				if (AttConsentStatus.Authorized.HasValue)
				{
					InitializeSdk(AttConsentStatus.Authorized.Value);
				}
				else
				{
					Debug.Log($"[{ProviderName}] Waiting for ATT consent before initializing...");
					AttConsentStatus.Resolved += OnAttResolved;
				}

				return;
			}

			InitializeSdk(null);
		}

		private void OnAttResolved(bool authorized)
		{
			AttConsentStatus.Resolved -= OnAttResolved;
			InitializeSdk(authorized);
		}

		private void InitializeSdk(bool? advertiserTracking)
		{
			string attNote = advertiserTracking.HasValue
				? $" (ATT {(advertiserTracking.Value ? "authorized" : "denied")})"
				: "";

			try
			{
				// FB.Mobile.* throws "Facebook object is not yet loaded" before FB.Init
				// completes — advertiser tracking goes AFTER init, before ActivateApp,
				// so the activate event still carries the ATT-corrected IDFA state.
				if (FB.IsInitialized)
				{
					ApplyAdvertiserTracking(advertiserTracking);
					CompleteSdkInitialize(attNote);
				}
				else
				{
					FB.Init(() =>
					{
						if (FB.IsInitialized)
						{
							ApplyAdvertiserTracking(advertiserTracking);
							CompleteSdkInitialize(attNote);
						}
						else
						{
							Debug.LogError($"[{ProviderName}] FB.Init failed — check Facebook → Edit Settings (App ID, Client Token).");
						}
					});

					// CanTrack() must pass while FB.Init completes so events buffer instead of dropping.
					_isInitialized = true;
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] FB.Init threw: {ex.Message}");
			}
		}

		private void ApplyAdvertiserTracking(bool? advertiserTracking)
		{
			if (!advertiserTracking.HasValue)
			{
				return;
			}

			try
			{
				FB.Mobile.SetAdvertiserTrackingEnabled(advertiserTracking.Value);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] SetAdvertiserTrackingEnabled failed: {ex.Message}");
			}
		}

		private void CompleteSdkInitialize(string attNote)
		{
			if (_sdkReady)
			{
				return;
			}

			try
			{
				FB.ActivateApp();
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] ActivateApp failed: {ex.Message}");
			}

			_sdkReady = true;
			_isInitialized = true;
			ApplyPendingUserId();
			FlushPreInitEvents();
			Debug.Log($"[{ProviderName}] Initialized{attNote}{(_installsOnly ? " (installs only — in-app events go to Meta via AppsFlyer)" : "")}");
		}

		private void FlushPreInitEvents()
		{
			while (_preInitEvents.Count > 0)
			{
				TrackInternal(_preInitEvents.Dequeue());
			}
		}

		public override void Track(AnalyticsEvent evt)
		{
			if (_installsOnly || !CanTrack() || evt == null)
			{
				return;
			}

			if (!_sdkReady)
			{
				if (_preInitEvents.Count >= MaxPreInitEvents)
				{
					_preInitEvents.Dequeue();
				}

				_preInitEvents.Enqueue(evt.Clone());
				return;
			}

			TrackInternal(evt);
		}

		private void TrackInternal(AnalyticsEvent evt)
		{
			try
			{
				MixpanelMappedEvent mapped = MixpanelEventMapper.Map(evt, _taxonomy.FlatNamer);
				Dictionary<string, object> parameters = Sanitize(mapped.Properties, out float? valueToSum);
				FB.LogAppEvent(mapped.EventName, valueToSum, parameters);
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
			if (_installsOnly || !CanTrack())
			{
				return;
			}

			if (!_sdkReady)
			{
				Debug.LogWarning($"[{ProviderName}] Purchase before SDK ready — skipped: {itemID}");
				return;
			}

			try
			{
				FB.LogPurchase(
					(float)price,
					currency,
					new Dictionary<string, object> { { AppEventParameterName.ContentID, itemID } });
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] LogPurchase failed: {ex.Message}");
			}
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
			// Meta app events have no user-property concept; attribution is device-based.
		}

		public override void SetUserID(string userID)
		{
			_pendingUserId = userID;
			if (!_sdkReady || string.IsNullOrEmpty(userID))
			{
				return;
			}

			try
			{
				FB.Mobile.UserID = userID;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Set UserID failed: {ex.Message}");
			}
		}

		public override void Flush()
		{
			// The Meta SDK buffers and flushes app events itself (background/foreground
			// transitions); the Unity surface exposes no manual flush.
		}

		private void ApplyPendingUserId()
		{
			if (!string.IsNullOrEmpty(_pendingUserId))
			{
				SetUserID(_pendingUserId);
			}
		}

		private static Dictionary<string, object> Sanitize(Dictionary<string, object> properties, out float? valueToSum)
		{
			valueToSum = null;
			if (properties == null || properties.Count == 0)
			{
				return null;
			}

			// Meta aggregates valueToSum separately; keeping a "value" parameter too would double-count.
			if (properties.TryGetValue("value", out object raw) && raw != null)
			{
				properties.Remove("value");
				switch (raw)
				{
					case float f: valueToSum = f; break;
					case double d: valueToSum = (float)d; break;
					case int n: valueToSum = n; break;
					case long n: valueToSum = n; break;
				}
			}

			var result = new Dictionary<string, object>(properties.Count);
			foreach (KeyValuePair<string, object> kvp in properties)
			{
				if (string.IsNullOrEmpty(kvp.Key) || kvp.Value == null)
				{
					continue;
				}

				result[kvp.Key] = kvp.Value switch
				{
					string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double => kvp.Value,
					_ => kvp.Value.ToString()
				};
			}

			return result.Count > 0 ? result : null;
		}
	}
}
#endif
