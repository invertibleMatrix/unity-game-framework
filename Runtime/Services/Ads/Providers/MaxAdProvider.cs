using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using AK.CoreDomain.Ads;
using UnityEngine;

namespace AK.Services.Ads.Providers
{
	/// <summary>
	/// AppLovin MAX implementation of IAdProvider.
	/// Supports Rewarded, Interstitial, Banner, and App Open ads.
	/// Rewarded Interstitial is not a MAX format — those placements return UnsupportedAdType.
	/// SDK key is read from the AppLovin Integration Manager; do not pass it here.
	/// </summary>
	public class MaxAdProvider : IAdProvider
	{
		private const string TAG = "[MaxAdProvider]";
		// Generous upper bound — a rewarded ad can run ~60s plus user dwell on the end card.
		private static readonly TimeSpan ShowTimeout = TimeSpan.FromMinutes(3);

		public string                ProviderName     => "AppLovin MAX";
		public int                   Priority         => 100;
		public bool                  IsInitialized    => _isInitialized;
		public IReadOnlyList<AdType> SupportedAdTypes => _supportedAdTypes;

		private static readonly List<AdType> _supportedAdTypes = new()
		{
			AdType.Rewarded,
			AdType.Interstitial,
			AdType.Banner,
			AdType.AppOpen
		};

		private bool _isInitialized;
		private bool _userCanTrack = true;
		private bool _userUnderAge;
		private bool _callbacksRegistered;

		private readonly Dictionary<string, string>                                _adUnitByPlacement = new();
		private readonly HashSet<string>                                           _loadingAdUnits    = new();
		private readonly Dictionary<string, UniTaskCompletionSource<AdLoadResult>> _loadWaiters       = new();
		private readonly Dictionary<string, ShowWaiter>                            _showWaiters       = new();

		private string         _currentBannerPlacementId;
		private string         _currentBannerAdUnitId;
		private BannerPosition _currentBannerPosition = BannerPosition.Bottom;
		private bool           _bannerHidden;
		private bool           _bannerReady;
		private bool           _bannerCreated;

		public UniTask<bool> InitializeAsync(IEnumerable<AdPlacementRegistration> placements)
		{
			if (_isInitialized)
			{
				Debug.LogWarning($"{TAG} Already initialized");
				return UniTask.FromResult(true);
			}

			if (_userUnderAge)
			{
				Debug.LogWarning($"{TAG} Refusing to initialize — AppLovin policy prohibits using MAX for child users");
				return UniTask.FromResult(false);
			}

#if MAX_ENABLED
			return InitializeMaxAsync(placements);
#else
			Debug.LogWarning($"{TAG} MAX package not present - provider stays uninitialized");
			return UniTask.FromResult(false);
#endif
		}

		public bool IsAdReady(string placementId, AdType adType)
		{
			if (string.IsNullOrEmpty(placementId))
				return false;

			if (!_adUnitByPlacement.TryGetValue(placementId, out var adUnitId) || string.IsNullOrEmpty(adUnitId))
				return false;

#if MAX_ENABLED
			return adType switch
			{
				AdType.Rewarded     => MaxSdk.IsRewardedAdReady(adUnitId),
				AdType.Interstitial => MaxSdk.IsInterstitialReady(adUnitId),
				AdType.AppOpen      => MaxSdk.IsAppOpenAdReady(adUnitId),
				AdType.Banner       => _bannerReady && adUnitId == _currentBannerAdUnitId,
				_                   => false
			};
#else
			return false;
#endif
		}

		public async UniTask<AdLoadResult> LoadAdAsync(string placementId, AdType adType, string adUnitId)
		{
			if (!_isInitialized)
				return AdLoadResult.Failed(placementId, adType, AdErrorType.NotInitialized, "Provider not initialized");

			if (string.IsNullOrEmpty(adUnitId))
				return AdLoadResult.Failed(placementId, adType, AdErrorType.InvalidPlacement, "Ad unit ID is empty");

			if (adType == AdType.RewardedInterstitial)
				return AdLoadResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, "MAX does not support Rewarded Interstitial");

			if (_loadingAdUnits.Contains(adUnitId))
				return AdLoadResult.Failed(placementId, adType, AdErrorType.InternalError, "Ad already loading");

			BindPlacement(placementId, adUnitId);

			if (IsAdReady(placementId, adType))
				return AdLoadResult.Succeeded(placementId, adType);

#if MAX_ENABLED
			_loadingAdUnits.Add(adUnitId);
			var tcs = new UniTaskCompletionSource<AdLoadResult>();
			_loadWaiters[adUnitId] = tcs;

			try
			{
				switch (adType)
				{
					case AdType.Rewarded:
						MaxSdk.LoadRewardedAd(adUnitId);
						break;
					case AdType.Interstitial:
						MaxSdk.LoadInterstitial(adUnitId);
						break;
					case AdType.AppOpen:
						MaxSdk.LoadAppOpenAd(adUnitId);
						break;
					case AdType.Banner:
						if (_bannerCreated && _currentBannerAdUnitId == adUnitId)
							MaxSdk.LoadBanner(adUnitId);
						else
							EnsureBannerCreated(placementId, adUnitId, _currentBannerPosition);
						break;
					default:
						_loadingAdUnits.Remove(adUnitId);
						_loadWaiters.Remove(adUnitId);
						return AdLoadResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, $"Unsupported ad type: {adType}");
				}

				var result = await tcs.Task;
				await UniTask.SwitchToMainThread();
				return result;
			}
			catch (Exception e)
			{
				await UniTask.SwitchToMainThread();
				_loadingAdUnits.Remove(adUnitId);
				_loadWaiters.Remove(adUnitId);
				Debug.LogError($"{TAG} LoadAdAsync exception: {e.Message}");
				return AdLoadResult.Failed(placementId, adType, AdErrorType.InternalError, e.Message);
			}
#else
			await UniTask.CompletedTask;
			return AdLoadResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, "MAX not available");
#endif
		}

		public async UniTask<AdResult> ShowAdAsync(string placementId, AdType adType, string adUnitId)
		{
			if (!_isInitialized)
				return AdResult.Failed(placementId, adType, AdErrorType.NotInitialized, "Provider not initialized");

			if (adType == AdType.Banner)
				return await ShowBannerAsync(placementId, adUnitId, BannerPosition.Bottom);

			if (adType == AdType.RewardedInterstitial)
				return AdResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, "MAX does not support Rewarded Interstitial");

			BindPlacement(placementId, adUnitId);

#if MAX_ENABLED
			try
			{
				if (!IsAdReady(placementId, adType))
				{
					var loadResult = await LoadAdAsync(placementId, adType, adUnitId);
					if (!loadResult.Success)
						return AdResult.Failed(placementId, adType, loadResult.ErrorType, loadResult.FailureReason);
				}

				if (!IsAdReady(placementId, adType))
					return AdResult.Failed(placementId, adType, AdErrorType.NotReady, $"{adType} ad not ready");

				var waiter = new ShowWaiter
				{
					Tcs = new UniTaskCompletionSource<AdResult>(),
					PlacementId = placementId,
					AdType = adType
				};
				_showWaiters[adUnitId] = waiter;

				switch (adType)
				{
					case AdType.Rewarded:
						MaxSdk.ShowRewardedAd(adUnitId, placementId);
						break;
					case AdType.Interstitial:
						MaxSdk.ShowInterstitial(adUnitId, placementId);
						break;
					case AdType.AppOpen:
						MaxSdk.ShowAppOpenAd(adUnitId, placementId);
						break;
					default:
						_showWaiters.Remove(adUnitId);
						return AdResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, $"Unsupported ad type: {adType}");
				}

				AdResult result;
				try
				{
					result = await waiter.Tcs.Task.Timeout(ShowTimeout);
				}
				catch (TimeoutException)
				{
					// Ad was shown but MAX never delivered Hidden/DisplayFailed (e.g. process
					// suspended mid-ad). Without this the caller's busy-flag wedges forever.
					_showWaiters.Remove(adUnitId);
					return AdResult.Failed(placementId, adType, AdErrorType.InternalError, "Timed out waiting for ad result");
				}

				await UniTask.SwitchToMainThread();
				return result;
			}
			catch (Exception e)
			{
				await UniTask.SwitchToMainThread();
				_showWaiters.Remove(adUnitId);
				Debug.LogError($"{TAG} ShowAdAsync exception: {e.Message}");
				return AdResult.Failed(placementId, adType, AdErrorType.InternalError, e.Message);
			}
#else
			await UniTask.CompletedTask;
			return AdResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, "MAX not available");
#endif
		}

		public async UniTask<AdResult> ShowBannerAsync(string placementId, string adUnitId, BannerPosition position)
		{
			if (!_isInitialized)
				return AdResult.Failed(placementId, AdType.Banner, AdErrorType.NotInitialized, "Provider not initialized");

			if (string.IsNullOrEmpty(adUnitId))
				return AdResult.Failed(placementId, AdType.Banner, AdErrorType.InvalidPlacement, "Ad unit ID is empty");

			BindPlacement(placementId, adUnitId);

#if MAX_ENABLED
			try
			{
				if (_bannerCreated && _currentBannerAdUnitId != adUnitId)
					DestroyBanner();

				_currentBannerPlacementId = placementId;
				_currentBannerPosition = position;
				_bannerHidden = false;

				if (!_bannerReady)
				{
					var loadResult = await LoadAdAsync(placementId, AdType.Banner, adUnitId);
					if (!loadResult.Success)
						return AdResult.Failed(placementId, AdType.Banner, loadResult.ErrorType, loadResult.FailureReason);
				}

				if (!_bannerHidden)
					MaxSdk.ShowBanner(adUnitId);

				await UniTask.SwitchToMainThread();
				return AdResult.Succeeded(placementId, AdType.Banner, ProviderName);
			}
			catch (Exception e)
			{
				await UniTask.SwitchToMainThread();
				Debug.LogError($"{TAG} ShowBannerAsync exception: {e.Message}");
				return AdResult.Failed(placementId, AdType.Banner, AdErrorType.InternalError, e.Message);
			}
#else
			await UniTask.CompletedTask;
			return AdResult.Failed(placementId, AdType.Banner, AdErrorType.UnsupportedAdType, "MAX not available");
#endif
		}

		public void HideBanner()
		{
			_bannerHidden = true;
#if MAX_ENABLED
			if (!string.IsNullOrEmpty(_currentBannerAdUnitId))
				MaxSdk.HideBanner(_currentBannerAdUnitId);
#endif
		}

		public void DestroyBanner()
		{
#if MAX_ENABLED
			if (!string.IsNullOrEmpty(_currentBannerAdUnitId))
				MaxSdk.DestroyBanner(_currentBannerAdUnitId);
#endif
			_currentBannerPlacementId = null;
			_currentBannerAdUnitId = null;
			_bannerHidden = false;
			_bannerReady = false;
			_bannerCreated = false;
		}

		public void SetUserConsent(bool canTrack)
		{
			_userCanTrack = canTrack;
#if MAX_ENABLED
			if (_isInitialized || MaxSdk.IsInitialized())
				MaxSdk.SetHasUserConsent(canTrack);
#endif
		}

		public void SetUserUnderAge(bool isUnderAge)
		{
			_userUnderAge = isUnderAge;
		}

		public void OnApplicationPause(bool isPaused)
		{
			// App-open display is owned by AdService.TryShowAppOpenAdAsync so frequency caps still apply.
		}

		private void BindPlacement(string placementId, string adUnitId)
		{
			if (!string.IsNullOrEmpty(placementId) && !string.IsNullOrEmpty(adUnitId))
				_adUnitByPlacement[placementId] = adUnitId;
		}

#if MAX_ENABLED
		private async UniTask<bool> InitializeMaxAsync(IEnumerable<AdPlacementRegistration> placements)
		{
			try
			{
				RegisterCallbacks();
				MaxSdk.SetHasUserConsent(_userCanTrack);

				if (MaxSdk.IsInitialized())
				{
					_isInitialized = true;
					Debug.Log($"{TAG} SDK already initialized ({MaxSdk.Version})");
					return true;
				}

				var unitIds = new List<string>();
				if (placements != null)
				{
					foreach (var placement in placements)
					{
						if (string.IsNullOrEmpty(placement.AdUnitId))
							continue;
						BindPlacement(placement.PlacementId, placement.AdUnitId);
						if (!unitIds.Contains(placement.AdUnitId))
							unitIds.Add(placement.AdUnitId);
					}
				}

				var tcs = new UniTaskCompletionSource<bool>();
				Action<MaxSdkBase.SdkConfiguration> handler = null;
				handler = config =>
				{
					MaxSdkCallbacks.OnSdkInitializedEvent -= handler;
					tcs.TrySetResult(config != null && config.IsSuccessfullyInitialized);
				};
				MaxSdkCallbacks.OnSdkInitializedEvent += handler;

				MaxSdk.InitializeSdk(unitIds.Count > 0 ? unitIds.ToArray() : null);

				bool success = await tcs.Task;
				await UniTask.SwitchToMainThread();

				if (!success)
				{
					Debug.LogError($"{TAG} Initialization failed");
					return false;
				}

				_isInitialized = true;
				Debug.Log($"{TAG} Initialization complete ({MaxSdk.Version})");
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError($"{TAG} Initialization exception: {e.Message}");
				await UniTask.SwitchToMainThread();
				return false;
			}
		}

		private void RegisterCallbacks()
		{
			if (_callbacksRegistered)
				return;

			MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoaded;
			MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailed;
			MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialDisplayFailed;
			MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHidden;
			MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialRevenuePaid;

			MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedLoaded;
			MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedLoadFailed;
			MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedDisplayFailed;
			MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedHidden;
			MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedReceivedReward;
			MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedRevenuePaid;

			MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += OnAppOpenLoaded;
			MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += OnAppOpenLoadFailed;
			MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += OnAppOpenDisplayFailed;
			MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += OnAppOpenHidden;
			MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += OnAppOpenRevenuePaid;

			MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerLoaded;
			MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerLoadFailed;
			MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerRevenuePaid;

			_callbacksRegistered = true;
		}

		private void EnsureBannerCreated(string placementId, string adUnitId, BannerPosition position)
		{
			if (_bannerCreated && _currentBannerAdUnitId == adUnitId)
				return;

			if (_bannerCreated)
				DestroyBanner();

			_currentBannerPlacementId = placementId;
			_currentBannerAdUnitId = adUnitId;
			MaxSdk.CreateBanner(adUnitId, new MaxSdkBase.AdViewConfiguration(MapBannerPosition(position)));
			MaxSdk.SetBannerBackgroundColor(adUnitId, Color.black);
			_bannerCreated = true;
		}

		private void CompleteLoad(string adUnitId, AdLoadResult result)
		{
			_loadingAdUnits.Remove(adUnitId);
			if (_loadWaiters.TryGetValue(adUnitId, out var tcs))
			{
				_loadWaiters.Remove(adUnitId);
				tcs.TrySetResult(result);
			}
		}

		private void CompleteShow(string adUnitId, AdResult result)
		{
			if (_showWaiters.TryGetValue(adUnitId, out var waiter))
			{
				_showWaiters.Remove(adUnitId);
				waiter.Tcs.TrySetResult(result);
			}
		}

		private void OnLoaded(string adUnitId, AdType adType)
		{
			if (adType == AdType.Banner && adUnitId == _currentBannerAdUnitId)
				_bannerReady = true;

			if (!_loadWaiters.ContainsKey(adUnitId))
				return;

			CompleteLoad(adUnitId, AdLoadResult.Succeeded(FindPlacement(adUnitId), adType));
		}

		private void OnLoadFailed(string adUnitId, AdType adType, MaxSdkBase.ErrorInfo errorInfo)
		{
			var placementId = FindPlacement(adUnitId);
			var errorType = MapLoadError(errorInfo);
			var message = errorInfo?.Message ?? "Unknown error";
			Debug.LogWarning($"{TAG} Failed to load {adType} {placementId}: {message}");
			CompleteLoad(adUnitId, AdLoadResult.Failed(placementId, adType, errorType, message));
		}

		private void OnDisplayFailed(string adUnitId, AdType adType, MaxSdkBase.ErrorInfo errorInfo)
		{
			var placementId = FindPlacement(adUnitId);
			var message = errorInfo?.Message ?? "Display failed";
			CompleteShow(adUnitId, AdResult.Failed(placementId, adType, AdErrorType.InternalError, message));
		}

		private void OnHidden(string adUnitId, AdType adType, MaxSdkBase.AdInfo adInfo)
		{
			if (!_showWaiters.TryGetValue(adUnitId, out var waiter))
				return;

			var isRewarded = adType == AdType.Rewarded;
			if (isRewarded && !waiter.RewardEarned)
			{
				CompleteShow(adUnitId,
					AdResult.Failed(waiter.PlacementId, adType, AdErrorType.UserCancelled, "Ad closed before the reward was earned"));
				return;
			}

			CompleteShow(adUnitId, AdResult.Succeeded(
				waiter.PlacementId,
				adType,
				string.IsNullOrEmpty(waiter.NetworkName) ? adInfo?.NetworkName ?? ProviderName : waiter.NetworkName,
				waiter.Revenue,
				rewardGranted: isRewarded && waiter.RewardEarned));
		}

		private void OnRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
		{
			if (adInfo == null)
				return;

			if (_showWaiters.TryGetValue(adUnitId, out var waiter))
			{
				waiter.Revenue = adInfo.Revenue;
				waiter.NetworkName = adInfo.NetworkName;
			}
		}

		private void OnInterstitialLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo) => OnLoaded(adUnitId, AdType.Interstitial);

		private void OnInterstitialLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo) =>
			OnLoadFailed(adUnitId, AdType.Interstitial, errorInfo);

		private void OnInterstitialDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo) =>
			OnDisplayFailed(adUnitId, AdType.Interstitial, errorInfo);

		private void OnInterstitialHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)      => OnHidden(adUnitId, AdType.Interstitial, adInfo);
		private void OnInterstitialRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo) => OnRevenuePaid(adUnitId, adInfo);

		private void OnRewardedLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)           => OnLoaded(adUnitId, AdType.Rewarded);
		private void OnRewardedLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo) => OnLoadFailed(adUnitId, AdType.Rewarded, errorInfo);

		private void OnRewardedDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo) =>
			OnDisplayFailed(adUnitId, AdType.Rewarded, errorInfo);

		private void OnRewardedHidden(string adUnitId, MaxSdkBase.AdInfo adInfo) => OnHidden(adUnitId, AdType.Rewarded, adInfo);

		private void OnRewardedReceivedReward(string adUnitId, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo)
		{
			if (_showWaiters.TryGetValue(adUnitId, out var waiter))
				waiter.RewardEarned = true;
		}

		private void OnRewardedRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo) => OnRevenuePaid(adUnitId, adInfo);

		private void OnAppOpenLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)           => OnLoaded(adUnitId, AdType.AppOpen);
		private void OnAppOpenLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo) => OnLoadFailed(adUnitId, AdType.AppOpen, errorInfo);

		private void OnAppOpenDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo) =>
			OnDisplayFailed(adUnitId, AdType.AppOpen, errorInfo);

		private void OnAppOpenHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)      => OnHidden(adUnitId, AdType.AppOpen, adInfo);
		private void OnAppOpenRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo) => OnRevenuePaid(adUnitId, adInfo);

		private void OnBannerLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo) => OnLoaded(adUnitId, AdType.Banner);

		private void OnBannerLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
		{
			if (adUnitId == _currentBannerAdUnitId)
				_bannerReady = false;
			OnLoadFailed(adUnitId, AdType.Banner, errorInfo);
		}

		private void OnBannerRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo) => OnRevenuePaid(adUnitId, adInfo);

		private string FindPlacement(string adUnitId)
		{
			foreach (var kvp in _adUnitByPlacement)
			{
				if (kvp.Value == adUnitId)
					return kvp.Key;
			}

			return adUnitId;
		}

		private static AdErrorType MapLoadError(MaxSdkBase.ErrorInfo errorInfo)
		{
			if (errorInfo == null)
				return AdErrorType.Unknown;

			return errorInfo.Code switch
			{
				MaxSdkBase.ErrorCode.NoFill         => AdErrorType.NoFill,
				MaxSdkBase.ErrorCode.NetworkError   => AdErrorType.NetworkError,
				MaxSdkBase.ErrorCode.NetworkTimeout => AdErrorType.NetworkError,
				MaxSdkBase.ErrorCode.NoNetwork      => AdErrorType.NetworkError,
				_                                   => AdErrorType.NoFill
			};
		}

		private static MaxSdkBase.AdViewPosition MapBannerPosition(BannerPosition position)
		{
			return position switch
			{
				BannerPosition.Top         => MaxSdkBase.AdViewPosition.TopCenter,
				BannerPosition.Bottom      => MaxSdkBase.AdViewPosition.BottomCenter,
				BannerPosition.TopLeft     => MaxSdkBase.AdViewPosition.TopLeft,
				BannerPosition.TopRight    => MaxSdkBase.AdViewPosition.TopRight,
				BannerPosition.BottomLeft  => MaxSdkBase.AdViewPosition.BottomLeft,
				BannerPosition.BottomRight => MaxSdkBase.AdViewPosition.BottomRight,
				BannerPosition.Center      => MaxSdkBase.AdViewPosition.Centered,
				_                          => MaxSdkBase.AdViewPosition.BottomCenter
			};
		}
#endif

		private class ShowWaiter
		{
			public UniTaskCompletionSource<AdResult> Tcs;
			public string                            PlacementId;
			public AdType                            AdType;
			public bool                              RewardEarned;
			public double                            Revenue;
			public string                            NetworkName;
		}
	}
}