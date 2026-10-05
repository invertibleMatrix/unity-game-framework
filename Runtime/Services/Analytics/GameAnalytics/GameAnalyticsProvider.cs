using System;
using System.Collections.Generic;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using GameAnalyticsSDK;
using GameAnalyticsSDK.Events;
using UnityEngine;

namespace AK.Services.Analytics.Providers
{
	/// <summary>
	/// GameAnalytics SDK adapter. Lives in AK.Services.GameAnalytics so AK.Services
	/// does not reference the vendor assembly. This assembly is compiled only when
	/// com.gameanalytics.sdk is installed (versionDefine UGFW_GAME_ANALYTICS_SDK).
	/// </summary>
	public class GameAnalyticsProvider : BaseAnalyticsProvider, IGameAnalyticsATTListener
	{
		public override string ProviderName => "GameAnalytics";

		private const string DefaultAdSdk = "applovin";
		private const int MaxCustomDimensionValues = 20;
		private const int MaxPreInitEvents = 128;

		// True once GameAnalytics.Initialize() has actually run. On iOS/tvOS that call is
		// deferred until the ATT prompt is answered; events buffer in _preInitEvents so the
		// consent dialog costs zero data.
		private bool _sdkReady;
		private readonly Queue<AnalyticsEvent> _preInitEvents = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void RegisterFactory()
		{
			AnalyticsProviderFactory.GameAnalytics = static () => new GameAnalyticsProvider();
		}

		public override void Initialize(AnalyticsMeta analyticsMeta, Dictionary<string, string> config)
		{
			base.Initialize(analyticsMeta, config);

			try
			{
				EnsureHostObject();

				if (GameAnalytics.Initialized)
				{
					_isInitialized = true;
					_sdkReady = true;
					if (!string.IsNullOrEmpty(_pendingUserId))
					{
						GameAnalytics.SetExternalUserId(_pendingUserId);
					}

					ApplyPendingIdentity();
					Debug.Log($"[{ProviderName}] Already initialized by Settings GameObject");
					return;
				}

				string build = _options?.Build;
				if (string.IsNullOrEmpty(build) && config != null)
				{
					config.TryGetValue("build", out build);
				}

				if (!string.IsNullOrEmpty(build))
				{
					GameAnalytics.SetBuildAllPlatforms(build);
				}

				ApplyDimensionWhitelist(_taxonomy);

				if (!string.IsNullOrEmpty(_pendingUserId))
				{
					GameAnalytics.SetCustomId(_pendingUserId);
					GameAnalytics.SetExternalUserId(_pendingUserId);
				}

				// iOS 14.5+ ATT: the prompt must be answered before GameAnalytics.Initialize()
				// so every event carries the consent status. GA still initializes on deny
				// (IDFV fallback) — the prompt only gates IDFA.
				if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.tvOS)
				{
					_isInitialized = true;
					try
					{
						GameAnalytics.RequestTrackingAuthorization(this);
						return;
					}
					catch (Exception ex)
					{
						Debug.LogError($"[{ProviderName}] ATT request failed, initializing without prompt: {ex.Message}");
					}
				}

				CompleteSdkInitialize("Initialized");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] Initialization failed: {ex.Message}");
				_isInitialized = true;
				_sdkReady = true;
			}
		}

		public void GameAnalyticsATTListenerNotDetermined()
		{
			AttConsentStatus.Report(false);
			CompleteSdkInitialize("Initialized (ATT not determined)");
		}

		public void GameAnalyticsATTListenerRestricted()
		{
			AttConsentStatus.Report(false);
			CompleteSdkInitialize("Initialized (ATT restricted)");
		}

		public void GameAnalyticsATTListenerDenied()
		{
			AttConsentStatus.Report(false);
			CompleteSdkInitialize("Initialized (ATT denied)");
		}

		public void GameAnalyticsATTListenerAuthorized()
		{
			AttConsentStatus.Report(true);
			CompleteSdkInitialize("Initialized (ATT authorized)");
		}

		private void CompleteSdkInitialize(string logMessage)
		{
			if (_sdkReady)
			{
				return;
			}

			try
			{
				GameAnalytics.Initialize();
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] GameAnalytics.Initialize failed: {ex.Message}");
			}

			_sdkReady = true;
			_isInitialized = true;
			ApplyPendingIdentity();
			FlushPreInitEvents();
			Debug.Log($"[{ProviderName}] {logMessage}");
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
			if (!CanTrack() || evt == null)
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
			AnalyticsEventKind kind = AnalyticsEventResolver.InferKind(evt);

			try
			{
				IDictionary<string, object> fields = evt.Parameters;
				switch (kind)
				{
					case AnalyticsEventKind.Progression:
						TrackProgression(evt, fields);
						break;
					case AnalyticsEventKind.Ad:
						TrackAd(evt, fields);
						break;
					case AnalyticsEventKind.Resource:
						TrackResource(evt, fields);
						break;
					case AnalyticsEventKind.Business:
						TrackBusiness(evt, fields);
						break;
					case AnalyticsEventKind.Error:
						TrackError(evt, fields);
						break;
					default:
						TrackDesign(evt, fields);
						break;
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
			Track(AnalyticsEvent.Ad(AnalyticsAdAction.Show, adType, placementID, DefaultAdSdk));
		}

		public override void TrackAdClick(string placementID, string adType)
		{
			Track(AnalyticsEvent.Ad(AnalyticsAdAction.Clicked, adType, placementID, DefaultAdSdk));
		}

		public override void TrackAdReward(string placementID, string rewardType, int rewardAmount)
		{
			Track(AnalyticsEvent.Ad(
				AnalyticsAdAction.RewardReceived,
				"rewarded",
				placementID,
				DefaultAdSdk,
				parameters: new Dictionary<string, object>
				{
					{ "reward_type", rewardType },
					{ "reward_amount", rewardAmount }
				}));
		}

		public override void SetUserProperty(string propertyName, string value)
		{
			if (string.IsNullOrEmpty(propertyName))
			{
				return;
			}

			if (_taxonomy.TryGetDimensionSlot(propertyName, out int dimension))
			{
				SetCustomDimension(dimension, value);
				return;
			}

			if (Debug.isDebugBuild)
			{
				Debug.Log($"[{ProviderName}] User property '{propertyName}'={value} (GA has no arbitrary user properties; use custom fields / dimensions)");
			}
		}

		public override void SetUserID(string userID)
		{
			_pendingUserId = userID;
			try
			{
				if (string.IsNullOrEmpty(userID))
				{
					return;
				}

				if (GameAnalytics.Initialized)
				{
					GameAnalytics.SetExternalUserId(userID);
					return;
				}

				GameAnalytics.SetCustomId(userID);
				GameAnalytics.SetExternalUserId(userID);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] SetUserID failed: {ex.Message}");
			}
		}

		public override void SetCustomDimension(int index, string value)
		{
			try
			{
				switch (index)
				{
					case 1:
						GameAnalytics.SetCustomDimension01(value);
						break;
					case 2:
						GameAnalytics.SetCustomDimension02(value);
						break;
					case 3:
						GameAnalytics.SetCustomDimension03(value);
						break;
					default:
						Debug.LogWarning($"[{ProviderName}] GA only supports custom dimensions 1-3 (got {index})");
						break;
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[{ProviderName}] SetCustomDimension failed: {ex.Message}");
			}
		}

		public override void Flush()
		{
			if (Debug.isDebugBuild)
			{
				Debug.Log($"[{ProviderName}] Flush (GA batches automatically)");
			}
		}

		private static bool HasFields(IDictionary<string, object> fields)
		{
			return fields != null && fields.Count > 0;
		}

		private static void EnsureHostObject()
		{
			if (UnityEngine.Object.FindFirstObjectByType<GameAnalytics>() != null)
			{
				return;
			}

			var go = new GameObject("GameAnalytics");
			go.AddComponent<GA_SpecialEvents>();
			go.AddComponent<GameAnalytics>();
			UnityEngine.Object.DontDestroyOnLoad(go);
		}

		private void TrackDesign(AnalyticsEvent evt, IDictionary<string, object> fields)
		{
			string eventId = GameAnalyticsEventMapper.BuildDesignEventId(evt.Id);
			bool withFields = HasFields(fields);
			if (evt.Value.HasValue && withFields)
			{
				GameAnalytics.NewDesignEvent(eventId, evt.Value.Value, fields, mergeFields: true);
			}
			else if (evt.Value.HasValue)
			{
				GameAnalytics.NewDesignEvent(eventId, evt.Value.Value);
			}
			else if (withFields)
			{
				GameAnalytics.NewDesignEvent(eventId, fields, mergeFields: true);
			}
			else
			{
				GameAnalytics.NewDesignEvent(eventId);
			}
		}

		private static void TrackProgression(AnalyticsEvent evt, IDictionary<string, object> fields)
		{
			var status = evt.ProgressionStatus switch
			{
				AnalyticsProgressionStatus.Complete => GAProgressionStatus.Complete,
				AnalyticsProgressionStatus.Fail => GAProgressionStatus.Fail,
				_ => GAProgressionStatus.Start
			};

			string p1 = GameAnalyticsEventMapper.SanitizeSegment(evt.Progression01 ?? evt.Id);
			string p2 = string.IsNullOrEmpty(evt.Progression02) ? null : GameAnalyticsEventMapper.SanitizeSegment(evt.Progression02);
			string p3 = string.IsNullOrEmpty(evt.Progression03) ? null : GameAnalyticsEventMapper.SanitizeSegment(evt.Progression03);
			bool withFields = HasFields(fields);

			if (!string.IsNullOrEmpty(p3))
			{
				if (evt.Score.HasValue && withFields)
					GameAnalytics.NewProgressionEvent(status, p1, p2, p3, evt.Score.Value, fields, true);
				else if (evt.Score.HasValue)
					GameAnalytics.NewProgressionEvent(status, p1, p2, p3, evt.Score.Value);
				else if (withFields)
					GameAnalytics.NewProgressionEvent(status, p1, p2, p3, fields, true);
				else
					GameAnalytics.NewProgressionEvent(status, p1, p2, p3);
			}
			else if (!string.IsNullOrEmpty(p2))
			{
				if (evt.Score.HasValue && withFields)
					GameAnalytics.NewProgressionEvent(status, p1, p2, evt.Score.Value, fields, true);
				else if (evt.Score.HasValue)
					GameAnalytics.NewProgressionEvent(status, p1, p2, evt.Score.Value);
				else if (withFields)
					GameAnalytics.NewProgressionEvent(status, p1, p2, fields, true);
				else
					GameAnalytics.NewProgressionEvent(status, p1, p2);
			}
			else
			{
				if (evt.Score.HasValue && withFields)
					GameAnalytics.NewProgressionEvent(status, p1, evt.Score.Value, fields, true);
				else if (evt.Score.HasValue)
					GameAnalytics.NewProgressionEvent(status, p1, evt.Score.Value);
				else if (withFields)
					GameAnalytics.NewProgressionEvent(status, p1, fields, true);
				else
					GameAnalytics.NewProgressionEvent(status, p1);
			}
		}

		private static void TrackAd(AnalyticsEvent evt, IDictionary<string, object> fields)
		{
			var action = evt.AdAction switch
			{
				AnalyticsAdAction.Clicked => GAAdAction.Clicked,
				AnalyticsAdAction.FailedShow => GAAdAction.FailedShow,
				AnalyticsAdAction.RewardReceived => GAAdAction.RewardReceived,
				AnalyticsAdAction.Request => GAAdAction.Request,
				AnalyticsAdAction.Loaded => GAAdAction.Loaded,
				_ => GAAdAction.Show
			};

			var adType = MapAdType(evt.AdType);
			string sdk = string.IsNullOrEmpty(evt.AdSdkName) ? DefaultAdSdk : evt.AdSdkName.ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty);
			string placement = GameAnalyticsEventMapper.SanitizeSegment(evt.AdPlacement ?? "unknown");
			bool withFields = HasFields(fields);

			if (action == GAAdAction.FailedShow)
			{
				var reason = MapAdError(evt.AdFailReason);
				if (withFields)
					GameAnalytics.NewAdEvent(action, adType, sdk, placement, reason, fields, true);
				else
					GameAnalytics.NewAdEvent(action, adType, sdk, placement, reason);
			}
			else if (evt.AdDurationMs.HasValue)
			{
				if (withFields)
					GameAnalytics.NewAdEvent(action, adType, sdk, placement, evt.AdDurationMs.Value, fields, true);
				else
					GameAnalytics.NewAdEvent(action, adType, sdk, placement, evt.AdDurationMs.Value);
			}
			else if (withFields)
			{
				GameAnalytics.NewAdEvent(action, adType, sdk, placement, fields, true);
			}
			else
			{
				GameAnalytics.NewAdEvent(action, adType, sdk, placement);
			}
		}

		private static void TrackResource(AnalyticsEvent evt, IDictionary<string, object> fields)
		{
			var flow = evt.ResourceFlow == AnalyticsResourceFlow.Sink
				? GAResourceFlowType.Sink
				: GAResourceFlowType.Source;
			if (HasFields(fields))
			{
				GameAnalytics.NewResourceEvent(
					flow,
					evt.ResourceCurrency ?? "None",
					evt.ResourceAmount ?? 0f,
					evt.ResourceItemType ?? "Gameplay",
					evt.ResourceItemId ?? "unknown",
					fields,
					true);
			}
			else
			{
				GameAnalytics.NewResourceEvent(
					flow,
					evt.ResourceCurrency ?? "None",
					evt.ResourceAmount ?? 0f,
					evt.ResourceItemType ?? "Gameplay",
					evt.ResourceItemId ?? "unknown");
			}
		}

		private static void TrackBusiness(AnalyticsEvent evt, IDictionary<string, object> fields)
		{
			string currency = string.IsNullOrEmpty(evt.Currency) ? "USD" : evt.Currency.ToUpperInvariant();
			int cents = (int)Math.Round((evt.Price ?? 0) * 100.0);
			if (HasFields(fields))
			{
				GameAnalytics.NewBusinessEvent(
					currency,
					cents,
					evt.ResourceItemType ?? "iap",
					evt.ItemId ?? "unknown",
					evt.CartType ?? "shop",
					fields,
					true);
			}
			else
			{
				GameAnalytics.NewBusinessEvent(
					currency,
					cents,
					evt.ResourceItemType ?? "iap",
					evt.ItemId ?? "unknown",
					evt.CartType ?? "shop");
			}
		}

		private static void TrackError(AnalyticsEvent evt, IDictionary<string, object> fields)
		{
			var severity = evt.ErrorSeverity switch
			{
				AnalyticsErrorSeverity.Debug => GAErrorSeverity.Debug,
				AnalyticsErrorSeverity.Info => GAErrorSeverity.Info,
				AnalyticsErrorSeverity.Warning => GAErrorSeverity.Warning,
				AnalyticsErrorSeverity.Critical => GAErrorSeverity.Critical,
				_ => GAErrorSeverity.Error
			};
			if (HasFields(fields))
			{
				GameAnalytics.NewErrorEvent(severity, evt.ErrorMessage, fields, true);
			}
			else
			{
				GameAnalytics.NewErrorEvent(severity, evt.ErrorMessage);
			}
		}

		private static GAAdType MapAdType(string adType)
		{
			return adType?.ToLowerInvariant() switch
			{
				"rewarded" or "rewardedvideo" or "rewarded_video" => GAAdType.RewardedVideo,
				"interstitial" => GAAdType.Interstitial,
				"video" => GAAdType.Video,
				"banner" => GAAdType.Banner,
				"offerwall" or "offer_wall" => GAAdType.OfferWall,
				"appopen" or "app_open" => GAAdType.AppOpen,
				"playable" => GAAdType.Playable,
				_ => GAAdType.RewardedVideo
			};
		}

		private static GAAdError MapAdError(string reason)
		{
			if (string.IsNullOrEmpty(reason))
			{
				return GAAdError.Unknown;
			}

			string lower = reason.ToLowerInvariant();
			if (lower.Contains("fill")) return GAAdError.NoFill;
			if (lower.Contains("offline") || lower.Contains("network")) return GAAdError.Offline;
			if (lower.Contains("invalid")) return GAAdError.InvalidRequest;
			if (lower.Contains("precache") || lower.Contains("ready")) return GAAdError.UnableToPrecache;
			if (lower.Contains("internal")) return GAAdError.InternalError;
			return GAAdError.Unknown;
		}

		private void ApplyPendingIdentity()
		{
			if (!string.IsNullOrEmpty(_options?.CustomDimension01))
			{
				GameAnalytics.SetCustomDimension01(_options.CustomDimension01);
			}

			if (!string.IsNullOrEmpty(_options?.CustomDimension02))
			{
				GameAnalytics.SetCustomDimension02(_options.CustomDimension02);
			}

			if (!string.IsNullOrEmpty(_options?.CustomDimension03))
			{
				GameAnalytics.SetCustomDimension03(_options.CustomDimension03);
			}
		}

		// GA drops a dimension value it was not told about before it started.
		private static void ApplyDimensionWhitelist(AnalyticsTaxonomy taxonomy)
		{
			var settings = GameAnalytics.SettingsGA;
			if (settings == null)
			{
				return;
			}

			TryFillDimensionList(settings.CustomDimensions01, taxonomy.AllowedValues(1));
			TryFillDimensionList(settings.CustomDimensions02, taxonomy.AllowedValues(2));
			TryFillDimensionList(settings.CustomDimensions03, taxonomy.AllowedValues(3));
		}

		private static void TryFillDimensionList(IList<string> target, IReadOnlyList<string> values)
		{
			if (target == null || values.Count == 0)
			{
				return;
			}

			for (int i = 0; i < values.Count; i++)
			{
				if (string.IsNullOrEmpty(values[i]))
				{
					continue;
				}

				bool exists = false;
				for (int t = 0; t < target.Count; t++)
				{
					if (target[t] == values[i])
					{
						exists = true;
						break;
					}
				}

				if (exists)
				{
					continue;
				}

				if (target.Count >= MaxCustomDimensionValues)
				{
					break;
				}

				target.Add(values[i]);
			}
		}
	}
}
