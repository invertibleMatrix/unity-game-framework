using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using AK.Services.Analytics;
using AK.Services.Analytics.Providers;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// Facade over analytics providers. Fail-open: a missing or drifted definition
	/// logs once and still dispatches. Queue events until <see cref="Initialize()"/>.
	/// </summary>
	public class AnalyticsService : IAnalyticsService
	{
		private const string Tag = "[AnalyticsService]";
		private const int MaxQueuedEvents = 128;

		private bool _isEnabled = true;
		private bool _isInitialized;
		private AnalyticsMeta _analyticsMeta;
		private AnalyticsInitOptions _initOptions;
		private string _pendingUserId;

		private readonly List<IAnalyticsProvider> _providers = new();
		private readonly Queue<AnalyticsEvent> _queuedEvents = new();
		private readonly HashSet<string> _warnedMissing = new();
		private readonly HashSet<string> _warnedValidation = new();

		public bool IsAnalyticsEnabled => _isEnabled;
		public bool IsInitialized => _isInitialized;

		public void SetMeta(AnalyticsMeta analyticsMeta)
		{
			_analyticsMeta = analyticsMeta;
		}

		public void RegisterProvider(IAnalyticsProvider provider)
		{
			if (provider != null && !_providers.Contains(provider))
			{
				_providers.Add(provider);
				Debug.Log($"{Tag} Registered provider: {provider.ProviderName}");
			}
		}

		public void Initialize()
		{
			Initialize(_analyticsMeta, _initOptions);
		}

		public void Initialize(AnalyticsMeta meta, AnalyticsInitOptions options = null)
		{
			if (_isInitialized)
			{
				Debug.LogWarning($"{Tag} Already initialized");
				return;
			}

			_analyticsMeta = meta ?? _analyticsMeta;
			_initOptions = options ?? _initOptions ?? new AnalyticsInitOptions();

			if (!string.IsNullOrEmpty(_pendingUserId) && string.IsNullOrEmpty(_initOptions.UserId))
			{
				_initOptions.UserId = _pendingUserId;
			}

			Debug.Log($"{Tag} Initializing {_providers.Count} providers...");
		if (_analyticsMeta == null)
		{
			Debug.Log($"{Tag} No AnalyticsMeta assigned — definition overlay disabled, all events send fail-open.");
		}

			Dictionary<string, string> config = _initOptions.ProviderConfig ?? new Dictionary<string, string>();
			if (!string.IsNullOrEmpty(_initOptions.UserId) && !config.ContainsKey("userId"))
			{
				config["userId"] = _initOptions.UserId;
			}

			if (!string.IsNullOrEmpty(_initOptions.Build) && !config.ContainsKey("build"))
			{
				config["build"] = _initOptions.Build;
			}

			foreach (IAnalyticsProvider provider in _providers)
			{
				try
				{
					provider.Configure(_initOptions);
					if (!string.IsNullOrEmpty(_initOptions.UserId))
					{
						provider.SetUserID(_initOptions.UserId);
					}

					provider.Initialize(_analyticsMeta, config);
					Debug.Log($"{Tag} Initialized provider: {provider.ProviderName}");
				}
				catch (Exception ex)
				{
					Debug.LogError($"{Tag} Failed to initialize provider {provider.ProviderName}: {ex.Message}");
				}
			}

			_isInitialized = true;
			FlushQueued();
			Debug.Log($"{Tag} Initialization complete");
		}

		public void Track(AnalyticsEvent evt)
		{
			if (evt == null)
			{
				return;
			}

			if (!_isEnabled)
			{
				return;
			}

			if (!_isInitialized)
			{
				Enqueue(evt);
				return;
			}

			AnalyticsEventResolver.Result resolved = AnalyticsEventResolver.Resolve(evt, _analyticsMeta);
			if (resolved.Dropped)
			{
				if (Debug.isDebugBuild)
				{
					Debug.Log($"{Tag} Dropped '{evt.Id}' ({resolved.DropReason})");
				}

				return;
			}

			if (resolved.MissingDefinition)
			{
				// No meta assigned = overlay intentionally unused, missing definitions are the
				// expected state — stay silent. Meta assigned but event absent = schema drift,
				// which is what this warning exists to catch.
				if (_analyticsMeta != null)
				{
					WarnOnce(_warnedMissing, evt.Id, $"{Tag} No AnalyticsEventDefinition for '{evt.Id}' — sending anyway (fail-open).");
				}
			}
			else if (resolved.MissingRequired)
			{
				WarnOnce(_warnedValidation, evt.Id + ":required", $"{Tag} Required parameters missing for '{evt.Id}' — sending anyway (fail-open).");
			}
			else if (resolved.UsedDefaults)
			{
				WarnOnce(_warnedValidation, evt.Id + ":defaults", $"{Tag} Filled default parameters for '{evt.Id}'.");
			}

			Dispatch(resolved.Event);
		}

		public void TrackEvent(string eventName, Dictionary<string, object> parameters)
		{
			Track(AnalyticsEvent.Design(eventName, parameters: parameters));
		}

		public void TrackEvent(UID eventID, Dictionary<ParameterName, object> parameters)
		{
			string id = eventID;
			if (_analyticsMeta != null)
			{
				AnalyticsEventDefinition def = _analyticsMeta.GetEventByID(eventID);
				if (def != null && !string.IsNullOrEmpty(def.EventID))
				{
					id = def.EventID;
				}
			}

			var evt = new AnalyticsEvent
			{
				Id = string.IsNullOrEmpty(id) ? eventID?.ToString() : id,
				Kind = AnalyticsEventKind.Unspecified,
				Parameters = AnalyticsEventResolver.Stringify(parameters)
			};
			Track(evt);
		}

		public void TrackDesign(string eventId, float? value = null, Dictionary<string, object> parameters = null)
		{
			Track(AnalyticsEvent.Design(eventId, value, parameters));
		}

		public void TrackProgression(
			AnalyticsProgressionStatus status,
			string progression01,
			string progression02 = null,
			string progression03 = null,
			int? score = null,
			Dictionary<string, object> parameters = null)
		{
			Track(AnalyticsEvent.Progression(status, progression01, progression02, progression03, score, parameters));
		}

		public void TrackAd(
			AnalyticsAdAction action,
			string adType,
			string placement,
			string sdkName = "applovin",
			long? durationMs = null,
			string failReason = null,
			double? revenue = null,
			Dictionary<string, object> parameters = null)
		{
			Track(AnalyticsEvent.Ad(action, adType, placement, sdkName, durationMs, failReason, revenue, parameters));
		}

		public void TrackResource(
			AnalyticsResourceFlow flow,
			string currency,
			float amount,
			string itemType,
			string itemId,
			Dictionary<string, object> parameters = null)
		{
			Track(new AnalyticsEvent
			{
				Id = "resource",
				Kind = AnalyticsEventKind.Resource,
				ResourceFlow = flow,
				ResourceCurrency = currency,
				ResourceAmount = amount,
				ResourceItemType = itemType,
				ResourceItemId = itemId,
				Parameters = parameters
			});
		}

		public void TrackError(AnalyticsErrorSeverity severity, string message, Dictionary<string, object> parameters = null)
		{
			Track(new AnalyticsEvent
			{
				Id = "error",
				Kind = AnalyticsEventKind.Error,
				ErrorSeverity = severity,
				ErrorMessage = message,
				Parameters = parameters
			});
		}

		public void TrackPurchase(string itemID, double price, string currency)
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

		public void TrackAdImpression(string placementID, string adType)
		{
			TrackAd(AnalyticsAdAction.Show, adType, placementID);
		}

		public void TrackAdClick(string placementID, string adType)
		{
			TrackAd(AnalyticsAdAction.Clicked, adType, placementID);
		}

		public void TrackAdReward(string placementID, string rewardType, int rewardAmount)
		{
			var parameters = new Dictionary<string, object>
			{
				{ "reward_type", rewardType },
				{ "reward_amount", rewardAmount }
			};
			TrackAd(AnalyticsAdAction.RewardReceived, "rewarded", placementID, parameters: parameters);
		}

		public void SetUserProperty(string propertyName, string value)
		{
			if (!_isEnabled)
			{
				return;
			}

			if (!_isInitialized)
			{
				EnqueueAction(() => SetUserProperty(propertyName, value));
				return;
			}

			ForEachProvider(p => p.SetUserProperty(propertyName, value));
		}

		public void SetUserID(string userID)
		{
			_pendingUserId = userID;
			if (_initOptions == null)
			{
				_initOptions = new AnalyticsInitOptions();
			}

			_initOptions.UserId = userID;

			if (!_isInitialized)
			{
				return;
			}

			ForEachProvider(p => p.SetUserID(userID));
		}

		public void SetCustomDimension(int index, string value)
		{
			if (!_isEnabled)
			{
				return;
			}

			if (!_isInitialized)
			{
				EnqueueAction(() => SetCustomDimension(index, value));
				return;
			}

			ForEachProvider(p => p.SetCustomDimension(index, value));
		}

		public void Flush()
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			ForEachProvider(p => p.Flush());
		}

		public void SetAnalyticsEnabled(bool enabled)
		{
			_isEnabled = enabled;
			foreach (IAnalyticsProvider provider in _providers)
			{
				try
				{
					provider.SetEnabled(enabled);
				}
				catch (Exception ex)
				{
					Debug.LogError($"{Tag} Provider {provider.ProviderName} failed to set enabled: {ex.Message}");
				}
			}
		}

		private void Dispatch(AnalyticsEvent evt)
		{
			foreach (IAnalyticsProvider provider in _providers)
			{
				if (!provider.IsEnabled)
				{
					continue;
				}

				try
				{
					provider.Track(evt);
				}
				catch (Exception ex)
				{
					Debug.LogError($"{Tag} Provider {provider.ProviderName} failed to track '{evt.Id}': {ex.Message}");
				}
			}
		}

		private void Enqueue(AnalyticsEvent evt)
		{
			if (_queuedEvents.Count >= MaxQueuedEvents)
			{
				_queuedEvents.Dequeue();
			}

			_queuedEvents.Enqueue(evt.Clone());
		}

		private readonly Queue<Action> _queuedActions = new();

		private void EnqueueAction(Action action)
		{
			if (_queuedActions.Count >= MaxQueuedEvents)
			{
				_queuedActions.Dequeue();
			}

			_queuedActions.Enqueue(action);
		}

		private void FlushQueued()
		{
			while (_queuedActions.Count > 0)
			{
				try
				{
					_queuedActions.Dequeue()?.Invoke();
				}
				catch (Exception ex)
				{
					Debug.LogError($"{Tag} Queued action failed: {ex.Message}");
				}
			}

			while (_queuedEvents.Count > 0)
			{
				AnalyticsEvent evt = _queuedEvents.Dequeue();
				Track(evt);
			}
		}

		private void ForEachProvider(Action<IAnalyticsProvider> action)
		{
			foreach (IAnalyticsProvider provider in _providers)
			{
				if (!provider.IsEnabled)
				{
					continue;
				}

				try
				{
					action(provider);
				}
				catch (Exception ex)
				{
					Debug.LogError($"{Tag} Provider {provider.ProviderName} failed: {ex.Message}");
				}
			}
		}

		private static void WarnOnce(HashSet<string> seen, string key, string message)
		{
			if (string.IsNullOrEmpty(key) || !seen.Add(key))
			{
				return;
			}

			Debug.LogWarning(message);
		}
	}
}
