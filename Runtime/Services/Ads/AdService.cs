using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using AK.CoreDomain;
using AK.CoreDomain.Ads;
using AK.CoreDomain.RemoteConfig;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// Main implementation of IAdsService.
	/// Coordinates between multiple ad providers with priority-based waterfall mediation.
	/// Handles frequency capping, cooldowns, and remote config integration.
	/// </summary>
	public class AdService : IAdsService
	{
		private const string TAG = "[AdService]";

		private readonly List<IAdProvider> _providers = new();
		private readonly Dictionary<string, int> _sessionShowCounts = new();
		private readonly Dictionary<string, DailyShowTracker> _dailyShowCounts = new();
		private readonly Dictionary<string, DateTime> _lastShowTimes = new();
		private readonly Dictionary<string, int> _retryAttempts = new();
		private readonly Dictionary<string, UniTaskCompletionSource<AdLoadResult>> _loadingTasks = new();
		private readonly HashSet<string> _autoReloadPlacements = new();

		// Cancellation support for background tasks
		private CancellationTokenSource _cancellationTokenSource;

		private AdsMeta             _adsMeta;
		private int                 _playerLevel;
		private bool                _isInitialized;
		private bool                _adsDisabled;
		private bool                _userCanTrack = true;
		private bool                _userUnderAge;

		public bool IsInitialized => _isInitialized;
		public bool AdsDisabled
		{
			get => _adsDisabled;
			set
			{
				if (_adsDisabled != value)
				{
					_adsDisabled = value;
					if (_adsDisabled)
					{
						OnAdsDisabled?.Invoke();
						DestroyBanner();
					}
				}
			}
		}

		public event Action OnAdsDisabled;
		public event Action<AdPlacementDefinition> OnAdShown;
		public event Action<AdPlacementDefinition, AdErrorType, string> OnAdFailed;
		public event Action<AdPlacementDefinition> OnAdRewardGranted;

		/// <summary>
		/// Creates a new AdService with the specified providers.
		/// Providers will be used in priority order (highest first).
		/// </summary>
		/// <param name="providers">The ad providers to use.</param>
		public AdService(params IAdProvider[] providers)
		{
			if (providers != null && providers.Length > 0)
			{
				_providers.AddRange(providers);
				_providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
			}
		}

		/// <summary>
		/// Adds a provider to the service.
		/// </summary>
		public void AddProvider(IAdProvider provider)
		{
			if (provider == null || _providers.Contains(provider))
				return;

			_providers.Add(provider);
			_providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
		}

		/// <summary>
		/// Removes a provider from the service.
		/// </summary>
		public void RemoveProvider(IAdProvider provider)
		{
			_providers.Remove(provider);
		}

		public async UniTask<bool> InitializeAsync(AdsMeta adsMeta, int playerLevel)
		{
			if (_isInitialized)
			{
				Debug.LogWarning($"{TAG} Already initialized");
				return true;
			}

			if (adsMeta == null)
			{
				Debug.LogError($"{TAG} MetaDataRepository is null");
				return false;
			}

			_adsMeta = adsMeta;
			_playerLevel = playerLevel;

			if (_adsMeta == null)
			{
				Debug.LogError($"{TAG} AdsMeta is null in MetaDataRepository");
				return false;
			}

			// Initialize cancellation token for background tasks
			_cancellationTokenSource?.Cancel();
			_cancellationTokenSource?.Dispose();
			_cancellationTokenSource = new CancellationTokenSource();

			// Cleanup old daily trackers
			CleanupOldDailyTrackers();

			// Register placements
			List<AdPlacementRegistration> registrations = BuildPlacementsList();

			// Initialize all providers
			bool anyProviderInitialized = false;

			foreach (var provider in _providers)
			{
				try
				{
					// Apply user consent settings before initialization
					provider.SetUserConsent(_userCanTrack);
					provider.SetUserUnderAge(_userUnderAge);

					bool success = await provider.InitializeAsync(registrations);
					if (success)
					{
						Debug.Log($"{TAG} Provider '{provider.ProviderName}' initialized successfully");
						anyProviderInitialized = true;
					}
					else
					{
						Debug.LogWarning($"{TAG} Provider '{provider.ProviderName}' failed to initialize");
					}
				}
				catch (Exception e)
				{
					Debug.LogError($"{TAG} Provider '{provider.ProviderName}' threw exception: {e.Message}");
				}
			}

			if (!anyProviderInitialized && _providers.Count > 0)
			{
				Debug.LogError($"{TAG} No ad providers initialized successfully");
				return false;
			}

			_isInitialized = true;
			Debug.Log($"{TAG} Initialization complete");

			// Preload placements that have PreloadOnInitialize enabled
			await PreloadPlacementsAsync();

			return true;
		}

		private List<AdPlacementRegistration> BuildPlacementsList()
		{
			var registrations = new List<AdPlacementRegistration>();

			if (_adsMeta?.Placements == null)
				return registrations;

			foreach (var placement in _adsMeta.Placements)
			{
				if (string.IsNullOrEmpty(placement.AdUnitID))
				{
					Debug.LogWarning($"{TAG} Placement '{placement.PlacementID}' has no ad unit ID, skipping");
					continue;
				}

				registrations.Add(new AdPlacementRegistration(
					placement.PlacementID,
					placement.AdType,
					placement.AdUnitID
				));
			}

			return registrations;
		}

		private void CleanupOldDailyTrackers()
		{
			var today = DateTime.UtcNow.Date;
			var keysToRemove = new List<string>();

			foreach (var kvp in _dailyShowCounts)
			{
				if (kvp.Value.Date != today)
				{
					keysToRemove.Add(kvp.Key);
				}
			}

			foreach (var key in keysToRemove)
			{
				_dailyShowCounts.Remove(key);
			}
		}

		public bool IsAdReady(AdPlacementDefinition placement)
		{
			if (!_isInitialized || _adsDisabled || placement == null)
				return false;

			if (!CanShowPlacement(placement))
				return false;

			return IsProviderReady(placement.PlacementID, placement.AdType);
		}

		public bool IsAdReady(string placementId)
		{
			if (!_isInitialized || _adsDisabled || string.IsNullOrEmpty(placementId))
				return false;

			var placement = _adsMeta?.GetPlacementByID(placementId);
			return placement != null && IsAdReady(placement);
		}

		public bool IsAdReady(AdType adType)
		{
			if (!_isInitialized || _adsDisabled)
				return false;

			var placement = GetFirstAvailablePlacement(adType);
			return placement != null && IsProviderReady(placement.PlacementID, adType);
		}

		private bool IsProviderReady(string placementId, AdType adType)
		{
			foreach (var provider in _providers)
			{
				if (provider.IsInitialized && provider.IsAdReady(placementId, adType))
					return true;
			}
			return false;
		}

		public async UniTask<AdLoadResult> LoadAdAsync(AdPlacementDefinition placement)
		{
			if (!_isInitialized)
			{
				return AdLoadResult.Failed(placement?.PlacementID, placement?.AdType ?? AdType.Rewarded, AdErrorType.NotInitialized, "Service not initialized");
			}

			if (placement == null)
			{
				return AdLoadResult.Failed(null, AdType.Rewarded, AdErrorType.InvalidPlacement, "Placement is null");
			}

			if (string.IsNullOrEmpty(placement.AdUnitID))
			{
				return AdLoadResult.Failed(placement.PlacementID, placement.AdType, AdErrorType.InvalidPlacement, "Ad unit ID is empty");
			}

			// Try each provider in priority order
			foreach (var provider in _providers)
			{
				if (!provider.IsInitialized)
					continue;

				if (!SupportsAdType(provider, placement.AdType))
					continue;

				var result = await provider.LoadAdAsync(placement.PlacementID, placement.AdType, placement.AdUnitID);
				if (result.Success)
				{
					return result;
				}
			}

			return AdLoadResult.Failed(placement.PlacementID, placement.AdType, AdErrorType.NoFill, "No provider could load the ad");
		}

		public async UniTask<AdLoadResult> LoadAdAsync(string placementId)
		{
			var placement = _adsMeta?.GetPlacementByID(placementId);
			if (placement == null)
			{
				return AdLoadResult.Failed(placementId, AdType.Rewarded, AdErrorType.InvalidPlacement, $"Placement '{placementId}' not found");
			}
			return await LoadAdAsync(placement);
		}

		public async UniTask PreloadAdsAsync(AdType adType)
		{
			if (!_isInitialized || _adsMeta?.Placements == null)
				return;

			var placements = _adsMeta.GetPlacementsByType(adType);
			foreach (var placement in placements)
			{
				await LoadAdAsync(placement);
			}
		}

		public async UniTask<AdResult> ShowAdAsync(AdPlacementDefinition placement)
		{
			if (!_isInitialized)
			{
				var result = AdResult.Failed(placement?.PlacementID, placement?.AdType ?? AdType.Rewarded, AdErrorType.NotInitialized, "Service not initialized");
				FireAdFailedEvent(placement, result.ErrorType, result.FailureReason);
				return result;
			}

			if (_adsDisabled)
			{
				var result = AdResult.AdsDisabled(placement?.PlacementID, placement?.AdType ?? AdType.Rewarded);
				FireAdFailedEvent(placement, result.ErrorType, result.FailureReason);
				return result;
			}

			if (placement == null)
			{
				var result = AdResult.Failed(null, AdType.Rewarded, AdErrorType.InvalidPlacement, "Placement is null");
				FireAdFailedEvent(null, result.ErrorType, result.FailureReason);
				return result;
			}

			// Check frequency caps and cooldowns
			if (!CanShowPlacement(placement))
			{
				var errorType = GetFrequencyErrorType(placement);
				var result = AdResult.Failed(placement.PlacementID, placement.AdType, errorType, "Frequency cap or cooldown active");
				FireAdFailedEvent(placement, result.ErrorType, result.FailureReason);
				return result;
			}

			// Try each provider in priority order
			foreach (var provider in _providers)
			{
				if (!provider.IsInitialized)
					continue;

				if (!SupportsAdType(provider, placement.AdType))
					continue;

				var result = await provider.ShowAdAsync(placement.PlacementID, placement.AdType, placement.AdUnitID);
				
				if (result.Success)
				{
					RecordAdShown(placement);
					OnAdShown?.Invoke(placement);

					if (result.RewardGranted)
					{
						OnAdRewardGranted?.Invoke(placement);
					}

					Debug.Log($"{TAG} Ad shown successfully: {placement.PlacementID} via {provider.ProviderName}");
					return result;
				}

				Debug.LogWarning($"{TAG} Provider '{provider.ProviderName}' failed to show ad: {result.FailureReason}");
			}

			var failedResult = AdResult.Failed(placement.PlacementID, placement.AdType, AdErrorType.NoFill, "No provider could show the ad");
			FireAdFailedEvent(placement, failedResult.ErrorType, failedResult.FailureReason);
			return failedResult;
		}

		public async UniTask<AdResult> ShowAdAsync(string placementId)
		{
			var placement = _adsMeta?.GetPlacementByID(placementId);
			if (placement == null)
			{
				var result = AdResult.Failed(placementId, AdType.Rewarded, AdErrorType.InvalidPlacement, $"Placement '{placementId}' not found");
				FireAdFailedEvent(null, result.ErrorType, result.FailureReason);
				return result;
			}
			return await ShowAdAsync(placement);
		}

		public async UniTask<bool> ShowRewardedAdAsync(AdPlacementDefinition placement)
		{
			if (placement == null || (placement.AdType != AdType.Rewarded && placement.AdType != AdType.RewardedInterstitial))
			{
				Debug.LogWarning($"{TAG} ShowRewardedAdAsync called with non-rewarded placement");
				return false;
			}

			var result = await ShowAdAsync(placement);
			return result.RewardGranted;
		}

		public async UniTask<bool> ShowRewardedAdAsync(string placementId)
		{
			var placement = _adsMeta?.GetPlacementByID(placementId);
			return await ShowRewardedAdAsync(placement);
		}

		public async UniTask<AdResult> ShowInterstitialAsync(AdPlacementDefinition placement)
		{
			if (placement == null || placement.AdType != AdType.Interstitial)
			{
				return AdResult.Failed(placement?.PlacementID, AdType.Interstitial, AdErrorType.InvalidPlacement, "Invalid interstitial placement");
			}
			return await ShowAdAsync(placement);
		}

		public async UniTask<AdResult> ShowInterstitialAsync(string placementId)
		{
			var placement = _adsMeta?.GetPlacementByID(placementId);
			return await ShowInterstitialAsync(placement);
		}

		public async UniTask<AdResult> ShowBannerAsync(AdPlacementDefinition placement, BannerPosition position = BannerPosition.Bottom)
		{
			if (!_isInitialized)
			{
				return AdResult.Failed(placement?.PlacementID, AdType.Banner, AdErrorType.NotInitialized, "Service not initialized");
			}

			if (_adsDisabled)
			{
				return AdResult.AdsDisabled(placement?.PlacementID, AdType.Banner);
			}

			if (placement == null || placement.AdType != AdType.Banner)
			{
				return AdResult.Failed(placement?.PlacementID, AdType.Banner, AdErrorType.InvalidPlacement, "Invalid banner placement");
			}

			// Try each provider for banner
			foreach (var provider in _providers)
			{
				if (!provider.IsInitialized || !SupportsAdType(provider, AdType.Banner))
					continue;

				var result = await provider.ShowBannerAsync(placement.PlacementID, placement.AdUnitID, position);
				if (result.Success)
				{
					Debug.Log($"{TAG} Banner shown: {placement.PlacementID} via {provider.ProviderName}");
					return result;
				}
			}

			return AdResult.Failed(placement.PlacementID, AdType.Banner, AdErrorType.NoFill, "No provider could show banner");
		}

		public async UniTask<AdResult> ShowBannerAsync(string placementId, BannerPosition position = BannerPosition.Bottom)
		{
			var placement = _adsMeta?.GetPlacementByID(placementId);
			return await ShowBannerAsync(placement, position);
		}

		public void HideBanner()
		{
			foreach (var provider in _providers)
			{
				if (provider.IsInitialized)
				{
					provider.HideBanner();
				}
			}
		}

		public void DestroyBanner()
		{
			foreach (var provider in _providers)
			{
				if (provider.IsInitialized)
				{
					provider.DestroyBanner();
				}
			}
		}

		public async UniTask<bool> TryShowAppOpenAdAsync()
		{
			if (!_isInitialized || _adsDisabled)
				return false;

			var appOpenPlacements = _adsMeta?.GetPlacementsByType(AdType.AppOpen);
			if (appOpenPlacements == null || appOpenPlacements.Count == 0)
				return false;

			foreach (var placement in appOpenPlacements)
			{
				if (!CanShowPlacement(placement))
					continue;

				var result = await ShowAdAsync(placement);
				if (result.Success)
					return true;
			}

			return false;
		}

		public bool CanShowPlacement(AdPlacementDefinition placement)
		{
			if (placement == null)
				return false;

			// Check if placement is available for current player level
			if (!placement.IsAvailable(_playerLevel))
				return false;

			// Check session cap
			if (placement.MaxPerSession > 0)
			{
				var sessionCount = GetSessionShowCount(placement.PlacementID);
				if (sessionCount >= placement.MaxPerSession)
					return false;
			}

			// Check daily cap
			if (placement.MaxPerDay > 0)
			{
				var dailyCount = GetDailyShowCount(placement.PlacementID);
				if (dailyCount >= placement.MaxPerDay)
					return false;
			}

			// Check cooldown
			if (placement.CooldownSeconds > 0)
			{
				if (_lastShowTimes.TryGetValue(placement.PlacementID, out var lastShown))
				{
					var timeSinceLast = DateTime.UtcNow - lastShown;
					if (timeSinceLast.TotalSeconds < placement.CooldownSeconds)
						return false;
				}
			}

			return true;
		}

		public int GetSessionShowCount(string placementId)
		{
			return _sessionShowCounts.TryGetValue(placementId, out var count) ? count : 0;
		}

		public int GetDailyShowCount(string placementId)
		{
			var today = DateTime.UtcNow.Date;
			if (_dailyShowCounts.TryGetValue(placementId, out var tracker) && tracker.Date == today)
			{
				return tracker.Count;
			}
			return 0;
		}

		public void SetUserConsent(bool canTrack)
		{
			_userCanTrack = canTrack;
			foreach (var provider in _providers)
			{
				provider.SetUserConsent(canTrack);
			}
		}

		public void SetUserUnderAge(bool isUnderAge)
		{
			_userUnderAge = isUnderAge;
			foreach (var provider in _providers)
			{
				provider.SetUserUnderAge(isUnderAge);
			}
		}

		public void OnApplicationPause(bool isPaused)
		{
			foreach (var provider in _providers)
			{
				provider.OnApplicationPause(isPaused);
			}
		}

		/// <summary>
		/// Updates player level (if provided) and ensures all eligible placements are loaded and ready.
		/// Call this on level completion or when player levels up to proactively load ads.
		/// </summary>
		/// <param name="newLevel">Optional new player level. If -1, uses current level.</param>
		/// <returns>True if any placements are ready (already loaded or newly loaded).</returns>
		public async UniTask<bool> RefreshPlacementsForLevelAsync(int newLevel = -1)
		{
			if (!_isInitialized || _adsMeta?.Placements == null)
				return false;

			int previousLevel = _playerLevel;
			
			// Update level if provided
			if (newLevel >= 0 && newLevel != _playerLevel)
			{
				_playerLevel = newLevel;
				Debug.Log($"{TAG} Player level updated from {previousLevel} to {newLevel}");
			}

			// Find placements available for current level that should be preloaded
			var eligiblePlacements = _adsMeta.Placements
				.Where(p => p.IsEnabled && !string.IsNullOrEmpty(p.AdUnitID))
				.Where(p => p.IsAvailable(_playerLevel))
				.Where(p => p.GetEffectiveLoadingStrategy().PreloadOnInitialize || p.GetEffectiveLoadingStrategy().AutoReloadAfterShow)
				.ToList();

			if (eligiblePlacements.Count == 0)
			{
				Debug.Log($"{TAG} No eligible placements for level {_playerLevel}");
				return false;
			}

			// Separate into already ready vs needs loading
			var placementsToLoad = new List<AdPlacementDefinition>();
			bool anyReady = false;

			foreach (var placement in eligiblePlacements)
			{
				if (IsProviderReady(placement.PlacementID, placement.AdType))
				{
					anyReady = true;
					continue;
				}

				// Skip if already loading
				if (!_loadingTasks.ContainsKey(placement.PlacementID))
				{
					placementsToLoad.Add(placement);
				}
			}

			if (placementsToLoad.Count == 0)
			{
				Debug.Log($"{TAG} All {eligiblePlacements.Count} eligible placements already ready for level {_playerLevel}");
				return anyReady;
			}

			Debug.Log($"{TAG} Loading {placementsToLoad.Count} placements for level {_playerLevel}");

			// Track auto-reload placements and load in parallel
			foreach (var placement in placementsToLoad)
			{
				var strategy = placement.GetEffectiveLoadingStrategy();
				if (strategy.AutoReloadAfterShow || strategy.AutoReloadOnFail)
				{
					_autoReloadPlacements.Add(placement.PlacementID);
				}
			}

			var results = await UniTask.WhenAll(placementsToLoad.Select(p => LoadAdWithRetryAsync(p)));

			int successCount = results.Count(r => r.Success);
			Debug.Log($"{TAG} Loaded {successCount}/{placementsToLoad.Count} placements for level {_playerLevel}");

			return anyReady || successCount > 0;
		}

		/// <summary>
		/// Gets the current player level stored in the service.
		/// </summary>
		public int CurrentPlayerLevel => _playerLevel;

		/// <summary>
		/// Cancels all pending background tasks and cleans up resources.
		/// Call this when the service is being destroyed or the game is closing.
		/// </summary>
		public void CancelAllTasks()
		{
			if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
			{
				_cancellationTokenSource.Cancel();
				Debug.Log($"{TAG} All background tasks cancelled");
			}

			// Clear loading tasks
			foreach (var kvp in _loadingTasks)
			{
				kvp.Value.TrySetCanceled();
			}
			_loadingTasks.Clear();

			// Clear auto-reload tracking
			_autoReloadPlacements.Clear();

			// Reset retry attempts
			_retryAttempts.Clear();
		}

		/// <summary>
		/// Checks if the service has been cancelled (for background tasks).
		/// </summary>
		protected bool IsCancellationRequested => _cancellationTokenSource?.IsCancellationRequested ?? false;

		/// <summary>
		/// Preloads placements that have PreloadOnInitialize enabled.
		/// Called automatically during initialization.
		/// Uses parallel loading to avoid blocking on slow placements.
		/// </summary>
		private async UniTask PreloadPlacementsAsync()
		{
			if (_adsMeta?.Placements == null)
				return;

			var placementsToPreload = _adsMeta.Placements
				.Where(p => p.IsEnabled && !string.IsNullOrEmpty(p.AdUnitID))
				.Where(p => p.IsAvailable(_playerLevel)) // Only preload placements available for current level
				.Where(p => p.GetEffectiveLoadingStrategy().PreloadOnInitialize)
				.ToList();

			if (placementsToPreload.Count == 0)
			{
				Debug.Log($"{TAG} No Placements is ready for preloading");
				return;
			}

			Debug.Log($"{TAG} Preloading {placementsToPreload.Count} placements for player level {_playerLevel}...");

			// Track auto-reload placements first
			foreach (var placement in placementsToPreload)
			{
				var strategy = placement.GetEffectiveLoadingStrategy();
				if (strategy.AutoReloadAfterShow || strategy.AutoReloadOnFail)
				{
					_autoReloadPlacements.Add(placement.PlacementID);
				}
			}

			// Load all placements in parallel to avoid blocking
			var loadTasks = placementsToPreload.Select(async placement =>
			{
				var result = await LoadAdWithRetryAsync(placement);
				if (result.Success)
				{
					Debug.Log($"{TAG} Preloaded: {placement.PlacementID} ({placement.AdType})");
				}
				else
				{
					Debug.LogWarning($"{TAG} Failed to preload {placement.PlacementID}: {result.FailureReason}");
				}
			}).ToList();

			// Wait for all loads to complete (but don't block on individual failures)
			await UniTask.WhenAll(loadTasks);
		}

		/// <summary>
		/// Loads an ad with retry logic based on the placement's loading strategy.
		/// </summary>
		private async UniTask<AdLoadResult> LoadAdWithRetryAsync(AdPlacementDefinition placement)
		{
			if (placement == null)
				return AdLoadResult.Failed(null, AdType.Rewarded, AdErrorType.InvalidPlacement, "Placement is null");

			if (IsCancellationRequested)
				return AdLoadResult.Failed(placement.PlacementID, placement.AdType, AdErrorType.InternalError, "Load cancelled");

			var strategy = placement.GetEffectiveLoadingStrategy();
			var result = await LoadAdInternalAsync(placement);

			// Handle auto-reload on failure with iterative retry (not recursive)
			while (!result.Success && strategy.AutoReloadOnFail && !IsCancellationRequested)
			{
				int currentAttempt = GetRetryAttempt(placement.PlacementID);
				int maxAttempts = strategy.MaxRetryAttempts;

				// MaxRetryAttempts of 0 means unlimited retries
				if (maxAttempts > 0 && currentAttempt >= maxAttempts)
				{
					Debug.LogWarning($"{TAG} Max retry attempts ({maxAttempts}) reached for {placement.PlacementID}");
					break;
				}

				float delay = strategy.GetRetryDelay(currentAttempt);
				Debug.Log($"{TAG} Scheduling retry #{currentAttempt + 1} for {placement.PlacementID} in {delay}s");

				// Use cancellation token to avoid thread stalling
				try
				{
					await UniTask.Delay(
						(int)(delay * 1000),
						cancellationToken: _cancellationTokenSource.Token);
				}
				catch (OperationCanceledException)
				{
					Debug.Log($"{TAG} Retry cancelled for {placement.PlacementID}");
					return AdLoadResult.Failed(placement.PlacementID, placement.AdType, AdErrorType.InternalError, "Load cancelled");
				}

				if (IsCancellationRequested)
					return AdLoadResult.Failed(placement.PlacementID, placement.AdType, AdErrorType.InternalError, "Load cancelled");

				IncrementRetryAttempt(placement.PlacementID);
				result = await LoadAdInternalAsync(placement);
			}

			// Reset retry counter on success
			if (result.Success)
			{
				ResetRetryAttempt(placement.PlacementID);
			}

			return result;
		}

		/// <summary>
		/// Internal load method with deduplication support.
		/// If the same placement is already being loaded, waits for that operation instead of creating duplicate requests.
		/// </summary>
		private async UniTask<AdLoadResult> LoadAdInternalAsync(AdPlacementDefinition placement)
		{
			if (placement == null)
				return AdLoadResult.Failed(null, AdType.Rewarded, AdErrorType.InvalidPlacement, "Placement is null");

			string placementId = placement.PlacementID;

			// Check if already loading this placement - deduplicate requests
			if (_loadingTasks.TryGetValue(placementId, out var existingTcs))
			{
				Debug.Log($"{TAG} Waiting for existing load operation for {placementId}");
				return await existingTcs.Task;
			}

			// Create new load operation
			var tcs = new UniTaskCompletionSource<AdLoadResult>();
			_loadingTasks[placementId] = tcs;

			try
			{
				var result = await LoadAdAsync(placement);
				tcs.TrySetResult(result);
				return result;
			}
			catch (Exception ex)
			{
				var failedResult = AdLoadResult.Failed(placementId, placement.AdType, AdErrorType.InternalError, ex.Message);
				tcs.TrySetResult(failedResult);
				return failedResult;
			}
			finally
			{
				_loadingTasks.Remove(placementId);
			}
		}

		/// <summary>
		/// Schedules an automatic reload after showing an ad.
		/// </summary>
		private async UniTaskVoid ScheduleAutoReloadAsync(AdPlacementDefinition placement)
		{
			if (placement == null || !_autoReloadPlacements.Contains(placement.PlacementID))
				return;

			var strategy = placement.GetEffectiveLoadingStrategy();
			if (!strategy.AutoReloadAfterShow)
				return;

			// Check for cancellation before starting
			if (IsCancellationRequested)
				return;

			// Wait for the configured delay with cancellation support
			if (strategy.ReloadDelaySeconds > 0)
			{
				try
				{
					await UniTask.Delay(
						(int)(strategy.ReloadDelaySeconds * 1000),
						cancellationToken: _cancellationTokenSource.Token);
				}
				catch (OperationCanceledException)
				{
					Debug.Log($"{TAG} Auto-reload cancelled for {placement.PlacementID}");
					return;
				}
			}

			// Check again after delay
			if (IsCancellationRequested || !_isInitialized)
				return;

			// Reset retry counter since this is a fresh load after successful show
			ResetRetryAttempt(placement.PlacementID);

			Debug.Log($"{TAG} Auto-reloading {placement.PlacementID} after show");

			try
			{
				var result = await LoadAdWithRetryAsync(placement);

				if (result.Success)
				{
					Debug.Log($"{TAG} Auto-reload succeeded for {placement.PlacementID}");
				}
				else
				{
					Debug.LogWarning($"{TAG} Auto-reload failed for {placement.PlacementID}: {result.FailureReason}");
				}
			}
			catch (OperationCanceledException)
			{
				Debug.Log($"{TAG} Auto-reload cancelled for {placement.PlacementID}");
			}
		}

		private int GetRetryAttempt(string placementId)
		{
			return _retryAttempts.TryGetValue(placementId, out var attempt) ? attempt : 0;
		}

		private void IncrementRetryAttempt(string placementId)
		{
			if (!_retryAttempts.ContainsKey(placementId))
				_retryAttempts[placementId] = 0;
			_retryAttempts[placementId]++;
		}

		private void ResetRetryAttempt(string placementId)
		{
			_retryAttempts.Remove(placementId);
		}

		private void RecordAdShown(AdPlacementDefinition placement)
		{
			if (placement == null)
				return;

			// Schedule auto-reload after successful show
			ScheduleAutoReloadAsync(placement).Forget();

			// Update session count
			if (!_sessionShowCounts.ContainsKey(placement.PlacementID))
			{
				_sessionShowCounts[placement.PlacementID] = 0;
			}
			_sessionShowCounts[placement.PlacementID]++;

			// Update daily count
			var today = DateTime.UtcNow.Date;
			if (!_dailyShowCounts.TryGetValue(placement.PlacementID, out var tracker) || tracker.Date != today)
			{
				tracker = new DailyShowTracker { Date = today, Count = 0 };
			}
			tracker.Count++;
			_dailyShowCounts[placement.PlacementID] = tracker;

			// Update last shown time
			_lastShowTimes[placement.PlacementID] = DateTime.UtcNow;
		}

		private AdPlacementDefinition GetFirstAvailablePlacement(AdType adType)
		{
			if (_adsMeta?.Placements == null)
				return null;

			foreach (var placement in _adsMeta.Placements)
			{
				if (placement.AdType == adType && CanShowPlacement(placement))
				{
					return placement;
				}
			}
			return null;
		}

		private bool SupportsAdType(IAdProvider provider, AdType adType)
		{
			return provider.SupportedAdTypes.Contains(adType);
		}

		private AdErrorType GetFrequencyErrorType(AdPlacementDefinition placement)
		{
			if (placement.MaxPerSession > 0 && GetSessionShowCount(placement.PlacementID) >= placement.MaxPerSession)
				return AdErrorType.FrequencyCapReached;

			if (placement.MaxPerDay > 0 && GetDailyShowCount(placement.PlacementID) >= placement.MaxPerDay)
				return AdErrorType.FrequencyCapReached;

			if (placement.CooldownSeconds > 0 && _lastShowTimes.TryGetValue(placement.PlacementID, out var lastShown))
			{
				var timeSinceLast = DateTime.UtcNow - lastShown;
				if (timeSinceLast.TotalSeconds < placement.CooldownSeconds)
					return AdErrorType.CooldownActive;
			}

			return AdErrorType.InternalError;
		}

		private void FireAdFailedEvent(AdPlacementDefinition placement, AdErrorType errorType, string reason)
		{
			OnAdFailed?.Invoke(placement, errorType, reason);
		}

		/// <summary>
		/// Tracks daily show counts for a placement.
		/// </summary>
		private class DailyShowTracker
		{
			public DateTime Date;
			public int Count;
		}
	}
}