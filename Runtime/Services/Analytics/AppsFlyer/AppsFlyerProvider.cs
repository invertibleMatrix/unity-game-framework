using System;
using System.Collections.Generic;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using AppsFlyerSDK;
using UnityEngine;

namespace AK.Services.Analytics.Providers
{
	/// <summary>
	/// AppsFlyer SDK adapter: install attribution plus the curated in-app events and
	/// impression-level ad revenue selected by <see cref="AppsFlyerEventMapper"/>. Lives in
	/// AK.Services.AppsFlyer so AK.Services does not reference the vendor assembly; compiled
	/// only when appsflyer-unity-plugin is installed (versionDefine UGFW_APPSFLYER_SDK).
	/// Credentials: Assets/Resources/AppsFlyer/AppsFlyerSettings.asset.
	///
	/// Unlike the analytics providers this one starts in <see cref="EarlyStart"/>, before
	/// auth, so a first launch that never reaches the backend still records its install.
	/// The game's user id is attached as the customer user id as soon as it is set.
	/// </summary>
	public class AppsFlyerProvider : BaseAnalyticsProvider
	{
		public override string ProviderName => "AppsFlyer";

		private bool _sdkStarted;
		private bool _unavailable;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void RegisterFactory()
		{
			AnalyticsProviderFactory.AppsFlyer = static () => new AppsFlyerProvider();
		}

		public override void EarlyStart()
		{
			StartSdk();
		}

		public override void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config)
		{
			base.Initialize(analyticsMeta, config);
			StartSdk();
			ApplyPendingUserId();
		}

		private void StartSdk()
		{
			if (_sdkStarted || _unavailable)
			{
				return;
			}

			bool isIos = Application.platform == RuntimePlatform.IPhonePlayer;
			if (Application.isEditor || (!isIos && Application.platform != RuntimePlatform.Android))
			{
				_unavailable = true;
				Debug.Log($"[{ProviderName}] Not started on {Application.platform} — mobile only.");
				return;
			}

			AppsFlyerSettings settings = AppsFlyerSettings.Load();
			if (settings == null || string.IsNullOrEmpty(settings.DevKey))
			{
				_unavailable = true;
				Debug.LogError($"[{ProviderName}] Missing dev key — create Resources/{AppsFlyerSettings.ResourcePath}.asset.");
				return;
			}

			if (isIos && string.IsNullOrEmpty(settings.AppleAppId))
			{
				_unavailable = true;
				Debug.LogError($"[{ProviderName}] Missing Apple App ID in {AppsFlyerSettings.ResourcePath}.asset — iOS installs would not attribute.");
				return;
			}

			try
			{
				AppsFlyer.setIsDebug(settings.DebugLogging || Debug.isDebugBuild);
				AppsFlyer.initSDK(settings.DevKey, isIos ? settings.AppleAppId : null, AppsFlyerCallbacks.Ensure());

				// The ATT prompt is already on screen (GameAnalyticsAtt.RequestEarly runs just
				// before EarlyStart); the native SDK holds the install until it is answered so a
				// consented install carries the IDFA. Must be called between initSDK and startSDK.
				if (isIos && settings.AttTimeoutSeconds > 0)
				{
					AppsFlyer.waitForATTUserAuthorizationWithTimeoutInterval(settings.AttTimeoutSeconds);
				}

				AppsFlyer.startSDK();
				_sdkStarted = true;
				_isInitialized = true;
				ApplyPendingUserId();
				Debug.Log($"[{ProviderName}] Started (plugin {AppsFlyer.kAppsFlyerPluginVersion})");
			}
			catch (Exception ex)
			{
				_unavailable = true;
				Debug.LogError($"[{ProviderName}] Start failed: {ex.Message}");
			}
		}

		public override void Track(AnalyticsEvent evt)
		{
			if (!CanTrack() || evt == null)
			{
				return;
			}

			try
			{
				if (AppsFlyerEventMapper.TryMapAdRevenue(evt, out AppsFlyerAdRevenue adRevenue))
				{
					var data = new AFAdRevenueData(
						adRevenue.MonetizationNetwork,
						ToMediationNetwork(adRevenue.MediationSdk),
						adRevenue.Currency,
						adRevenue.Revenue);
					AppsFlyer.logAdRevenue(data, adRevenue.AdditionalParameters);
					return;
				}

				if (AppsFlyerEventMapper.TryMapEvent(evt, _taxonomy.AppsFlyerSelector, out string eventName, out Dictionary<string, string> values))
				{
					AppsFlyer.sendEvent(eventName, values);
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
			// AppsFlyer has no per-user property model; attribution is keyed on device ids
			// plus the customer user id set below.
		}

		public override void SetUserID(string userID)
		{
			_pendingUserId = userID;
			if (!_sdkStarted || string.IsNullOrEmpty(userID))
			{
				return;
			}

			try
			{
				AppsFlyer.setCustomerUserId(userID);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Set customer user id failed: {ex.Message}");
			}
		}

		public override void Flush()
		{
			// The native SDK batches and sends on its own schedule; there is no manual flush.
		}

		public override void SetEnabled(bool enabled)
		{
			base.SetEnabled(enabled);
			if (!_sdkStarted)
			{
				return;
			}

			try
			{
				// stopSDK(true) halts all traffic to AppsFlyer (the opt-out primitive); false resumes.
				AppsFlyer.stopSDK(!enabled);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Set enabled failed: {ex.Message}");
			}
		}

		private void ApplyPendingUserId()
		{
			if (!string.IsNullOrEmpty(_pendingUserId))
			{
				SetUserID(_pendingUserId);
			}
		}

		private static MediationNetwork ToMediationNetwork(string mediationSdk)
		{
			switch ((mediationSdk ?? string.Empty).ToLowerInvariant())
			{
				case "applovin":
				case "max":
					return MediationNetwork.ApplovinMax;
				case "admob":
					return MediationNetwork.GoogleAdMob;
				case "ironsource":
				case "levelplay":
					return MediationNetwork.IronSource;
				case "unity":
					return MediationNetwork.Unity;
				default:
					return MediationNetwork.Custom;
			}
		}
	}
}
