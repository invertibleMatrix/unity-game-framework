using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using AK.Services.Analytics.Providers;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// Main Analytics Service implementation.
	/// Acts as a facade over multiple analytics SDK providers.
	/// Integrates with the MetaData system for event definitions.
	/// </summary>
	public class AnalyticsService : MonoBehaviour, IAnalyticsService
	{
		private bool _isEnabled     = true;
		private bool _isInitialized = false;

		private readonly List<IAnalyticsProvider> _providers = new();

		private AnalyticsMeta _analyticsMeta;

		public bool IsAnalyticsEnabled => _isEnabled;

		/// <summary>
		/// Registers an analytics provider.
		/// </summary>
		public void RegisterProvider(IAnalyticsProvider provider)
		{
			if (provider != null && !_providers.Contains(provider))
			{
				_providers.Add(provider);
				Debug.Log($"[AnalyticsService] Registered provider: {provider.ProviderName}");
			}
		}

		/// <summary>
		/// Initializes all registered providers.
		/// </summary>
		public void Initialize()
		{
			if (_isInitialized)
			{
				Debug.LogWarning("[AnalyticsService] Already initialized");
				return;
			}

			Debug.Log($"[AnalyticsService] Initializing {_providers.Count} providers...");

			foreach (var provider in _providers)
			{
				try
				{
					provider.Initialize(_analyticsMeta, new Dictionary<string, string>());
					Debug.Log($"[AnalyticsService] Initialized provider: {provider.ProviderName}");
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AnalyticsService] Failed to initialize provider {provider.ProviderName}: {ex.Message}");
				}
			}

			_isInitialized = true;
			Debug.Log("[AnalyticsService] Initialization complete");
		}

		public void TrackEvent(string eventName, Dictionary<string, object> parameters)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			foreach (var provider in _providers.Where(p => p.IsEnabled))
			{
				try
				{
					provider.TrackEvent(eventName, parameters);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AnalyticsService] Provider {provider.ProviderName} failed to track event {eventName}: {ex.Message}");
				}
			}
		}

		public void TrackEvent(UID eventID, Dictionary<ParameterName, object> parameters)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			// Get event definition from MetaData
			var eventDefinition = _analyticsMeta.GetEventByID(eventID);

			if (eventDefinition == null)
			{
				Debug.LogWarning($"[AnalyticsService] Event definition not found: {eventID}");
				return;
			}

			// Check if event should be tracked
			if (!eventDefinition.ShouldTrack())
			{
				return;
			}

			// Validate parameters
			if (parameters != null && !eventDefinition.ValidateParameters(parameters))
			{
				Debug.LogError($"[AnalyticsService] Invalid or Incomplete parameters for event: {eventID}");
				return;
			}

			// Use provider-specific event name if configured, otherwise use EventID
			string eventName = !string.IsNullOrEmpty(eventDefinition.ProviderEventName)
				? eventDefinition.ProviderEventName
				: eventID;

			// Track event with all enabled providers
			foreach (var provider in _providers.Where(p => p.IsEnabled))
			{
				try
				{
					provider.TrackEvent(eventID, parameters);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AnalyticsService] Provider {provider.ProviderName} failed to track event {eventID}: {ex.Message}");
				}
			}

			if (Debug.isDebugBuild)
			{
				Debug.Log($"[AnalyticsService] Tracked event: {eventName}");
			}
		}

		public void TrackPurchase(string itemID, double price, string currency)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			foreach (var provider in _providers.Where(p => p.IsEnabled))
			{
				try
				{
					provider.TrackPurchase(itemID, price, currency);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AnalyticsService] Provider {provider.ProviderName} failed to track purchase: {ex.Message}");
				}
			}

			if (Debug.isDebugBuild)
			{
				Debug.Log($"[AnalyticsService] Tracked purchase: {itemID} for {price} {currency}");
			}
		}

		public void TrackAdImpression(string placementID, string adType)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			foreach (var provider in _providers.Where(p => p.IsEnabled))
			{
				try
				{
					provider.TrackAdImpression(placementID, adType);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AnalyticsService] Provider {provider.ProviderName} failed to track ad impression: {ex.Message}");
				}
			}

			if (Debug.isDebugBuild)
			{
				Debug.Log($"[AnalyticsService] Tracked ad impression: {placementID} ({adType})");
			}
		}

		public void TrackAdClick(string placementID, string adType)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			foreach (var provider in _providers.Where(p => p.IsEnabled))
			{
				try
				{
					provider.TrackAdClick(placementID, adType);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AnalyticsService] Provider {provider.ProviderName} failed to track ad click: {ex.Message}");
				}
			}

			if (Debug.isDebugBuild)
			{
				Debug.Log($"[AnalyticsService] Tracked ad click: {placementID} ({adType})");
			}
		}

		public void TrackAdReward(string placementID, string rewardType, int rewardAmount)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			foreach (var provider in _providers.Where(p => p.IsEnabled))
			{
				try
				{
					provider.TrackAdReward(placementID, rewardType, rewardAmount);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AnalyticsService] Provider {provider.ProviderName} failed to track ad reward: {ex.Message}");
				}
			}

			if (Debug.isDebugBuild)
			{
				Debug.Log($"[AnalyticsService] Tracked ad reward: {placementID} - {rewardType} x{rewardAmount}");
			}
		}

		public void SetUserProperty(string propertyName, string value)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			foreach (var provider in _providers.Where(p => p.IsEnabled))
			{
				try
				{
					provider.SetUserProperty(propertyName, value);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AnalyticsService] Provider {provider.ProviderName} failed to set user property: {ex.Message}");
				}
			}

			if (Debug.isDebugBuild)
			{
				Debug.Log($"[AnalyticsService] Set user property: {propertyName} = {value}");
			}
		}

		public void SetUserID(string userID)
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			foreach (var provider in _providers.Where(p => p.IsEnabled))
			{
				try
				{
					provider.SetUserID(userID);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AnalyticsService] Provider {provider.ProviderName} failed to set user ID: {ex.Message}");
				}
			}

			if (Debug.isDebugBuild)
			{
				Debug.Log($"[AnalyticsService] Set user ID: {userID}");
			}
		}

		public void Flush()
		{
			if (!_isEnabled || !_isInitialized)
			{
				return;
			}

			foreach (var provider in _providers.Where(p => p.IsEnabled))
			{
				try
				{
					provider.Flush();
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AnalyticsService] Provider {provider.ProviderName} failed to flush: {ex.Message}");
				}
			}

			if (Debug.isDebugBuild)
			{
				Debug.Log("[AnalyticsService] Flushed all providers");
			}
		}

		public void SetAnalyticsEnabled(bool enabled)
		{
			_isEnabled = enabled;

			foreach (var provider in _providers)
			{
				try
				{
					provider.SetEnabled(enabled);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AnalyticsService] Provider {provider.ProviderName} failed to set enabled state: {ex.Message}");
				}
			}

			Debug.Log($"[AnalyticsService] Analytics {(enabled ? "enabled" : "disabled")}");
		}
	}
}