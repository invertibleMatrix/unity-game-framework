using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using AK.CoreDomain.Ads;
using UnityEngine;

namespace AK.Services.Ads.Providers
{
	/// <summary>
	/// Google AdMob implementation of IAdProvider.
	/// Supports Rewarded, Interstitial, Banner, App Open, and Rewarded Interstitial ads.
	/// Based on Google Mobile Ads Unity Plugin v5.4.0+ API.
	/// 
	/// IMPORTANT: All async methods ensure continuations run on the main thread by using
	/// SwitchToMainThread() after awaiting background thread callbacks.
	/// </summary>
	public class AdMobAdProvider : IAdProvider
	{
		private const string TAG = "[AdMobProvider]";

		public string ProviderName => "AdMob";
		public int Priority => 100; // Default priority
		public bool IsInitialized => _isInitialized;

		public IReadOnlyList<AdType> SupportedAdTypes => _supportedAdTypes;

		private static readonly List<AdType> _supportedAdTypes = new()
		{
			AdType.Rewarded,
			AdType.Interstitial,
			AdType.Banner,
			AdType.AppOpen,
			AdType.RewardedInterstitial
		};

		private bool _isInitialized;
		private bool _userCanTrack = true;
		private bool _userUnderAge = false;
		private List<string> _testDeviceIds = new(){"4CC6FC5A960785DC366E5E022B91C409"};

		// Ad references by placement ID
		private readonly Dictionary<string, AdMobAdInfo> _loadedAds = new();
		private readonly HashSet<string> _loadingAds = new();

		// Current banner state
		private string _currentBannerPlacementId;
		private BannerPosition _currentBannerPosition;
		private bool _bannerHidden;

		/// <summary>
		/// Sets test device IDs for testing.
		/// </summary>
		public void SetTestDeviceIds(List<string> testDeviceIds)
		{
			_testDeviceIds = testDeviceIds ?? new List<string>();
		}

		public async UniTask<bool> InitializeAsync(IEnumerable<AdPlacementRegistration> placements)
		{
			if (_isInitialized)
			{
				Debug.LogWarning($"{TAG} Already initialized");
				return true;
			}

			try
			{
#if ADMOB_ENABLED && (UNITY_ANDROID || UNITY_IOS)
				// Configure request settings before initialization
				var requestConfiguration = new GoogleMobileAds.Api.RequestConfiguration
				{
					TestDeviceIds = _testDeviceIds,
					TagForChildDirectedTreatment = _userUnderAge 
						? GoogleMobileAds.Api.TagForChildDirectedTreatment.True 
						: GoogleMobileAds.Api.TagForChildDirectedTreatment.Unspecified,
					TagForUnderAgeOfConsent = _userUnderAge 
						? GoogleMobileAds.Api.TagForUnderAgeOfConsent.True 
						: GoogleMobileAds.Api.TagForUnderAgeOfConsent.Unspecified
				};

				GoogleMobileAds.Api.MobileAds.SetRequestConfiguration(requestConfiguration);

				// Initialize using callback-based API
				var initTcs = new UniTaskCompletionSource<bool>();
				
				GoogleMobileAds.Api.MobileAds.Initialize((initStatus) =>
				{
					if (initStatus == null)
					{
						Debug.LogError($"{TAG} Initialization failed - null status");
						initTcs.TrySetResult(false);
						return;
					}

					Debug.Log($"{TAG} Initialization complete");
					initTcs.TrySetResult(true);
				});

				// Await the callback and switch back to main thread
				bool success = await initTcs.Task;
				await UniTask.SwitchToMainThread();
				
				if (!success)
				{
					return false;
				}

				_isInitialized = true;
				return true;
#else
				await UniTask.CompletedTask;
				Debug.LogWarning($"{TAG} AdMob not enabled or not on supported platform");
				_isInitialized = true;
				return true;
#endif
			}
			catch (Exception e)
			{
				Debug.LogError($"{TAG} Initialization exception: {e.Message}");
				await UniTask.SwitchToMainThread();
				return false;
			}
		}

		public bool IsAdReady(string placementId, AdType adType)
		{
			if (string.IsNullOrEmpty(placementId))
				return false;

			return _loadedAds.TryGetValue(placementId, out var adInfo) &&
			       adInfo.AdType == adType &&
			       adInfo.IsAdReady();
		}

		public async UniTask<AdLoadResult> LoadAdAsync(string placementId, AdType adType, string adUnitId)
		{
			if (!_isInitialized)
			{
				return AdLoadResult.Failed(placementId, adType, AdErrorType.NotInitialized, "Provider not initialized");
			}

			if (string.IsNullOrEmpty(adUnitId))
			{
				return AdLoadResult.Failed(placementId, adType, AdErrorType.InvalidPlacement, "Ad unit ID is empty");
			}

			if (_loadingAds.Contains(placementId))
			{
				return AdLoadResult.Failed(placementId, adType, AdErrorType.InternalError, "Ad already loading");
			}

			_loadingAds.Add(placementId);

			try
			{
#if ADMOB_ENABLED && (UNITY_ANDROID || UNITY_IOS)
				var adInfo = GetOrCreateAdInfo(placementId, adType, adUnitId);

				AdLoadResult result = adType switch
				{
					AdType.Rewarded => await LoadRewardedAdAsync(adInfo),
					AdType.Interstitial => await LoadInterstitialAdAsync(adInfo),
					AdType.RewardedInterstitial => await LoadRewardedInterstitialAdAsync(adInfo),
					AdType.AppOpen => await LoadAppOpenAdAsync(adInfo),
					AdType.Banner => await LoadBannerAdAsync(adInfo),
					_ => AdLoadResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, $"Unsupported ad type: {adType}")
				};

				// Ensure we're back on main thread after background callback
				await UniTask.SwitchToMainThread();
				
				_loadingAds.Remove(placementId);
				return result;
#else
				await UniTask.CompletedTask;
				return AdLoadResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, "AdMob not available on this platform");
#endif
			}
			catch (Exception e)
			{
				await UniTask.SwitchToMainThread();
				_loadingAds.Remove(placementId);
				Debug.LogError($"{TAG} LoadAdAsync exception: {e.Message}");
				return AdLoadResult.Failed(placementId, adType, AdErrorType.InternalError, e.Message);
			}
		}

		public async UniTask<AdResult> ShowAdAsync(string placementId, AdType adType, string adUnitId)
		{
			if (!_isInitialized)
			{
				return AdResult.Failed(placementId, adType, AdErrorType.NotInitialized, "Provider not initialized");
			}

			try
			{
#if ADMOB_ENABLED && (UNITY_ANDROID || UNITY_IOS)
				// Load if not ready
				if (!_loadedAds.TryGetValue(placementId, out var adInfo) || !adInfo.IsAdReady())
				{
					var loadResult = await LoadAdAsync(placementId, adType, adUnitId);
					if (!loadResult.Success)
					{
						return AdResult.Failed(placementId, adType, loadResult.ErrorType, loadResult.FailureReason);
					}
					adInfo = _loadedAds[placementId];
				}

				AdResult result = adType switch
				{
					AdType.Rewarded => await ShowRewardedAdAsync(adInfo),
					AdType.Interstitial => await ShowInterstitialAdAsync(adInfo),
					AdType.RewardedInterstitial => await ShowRewardedInterstitialAdAsync(adInfo),
					AdType.AppOpen => await ShowAppOpenAdAsync(adInfo),
					_ => AdResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, $"Unsupported ad type: {adType}")
				};

				// Ensure we're back on main thread after background callback
				await UniTask.SwitchToMainThread();
				
				return result;
#else
				await UniTask.CompletedTask;
				return AdResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, "AdMob not available on this platform");
#endif
			}
			catch (Exception e)
			{
				await UniTask.SwitchToMainThread();
				Debug.LogError($"{TAG} ShowAdAsync exception: {e.Message}");
				return AdResult.Failed(placementId, adType, AdErrorType.InternalError, e.Message);
			}
		}

		public async UniTask<AdResult> ShowBannerAsync(string placementId, string adUnitId, BannerPosition position)
		{
			if (!_isInitialized)
			{
				return AdResult.Failed(placementId, AdType.Banner, AdErrorType.NotInitialized, "Provider not initialized");
			}

			if (string.IsNullOrEmpty(adUnitId))
			{
				return AdResult.Failed(placementId, AdType.Banner, AdErrorType.InvalidPlacement, "Ad unit ID is empty");
			}

			_currentBannerPlacementId = placementId;
			_currentBannerPosition = position;
			_bannerHidden = false;

#if ADMOB_ENABLED && (UNITY_ANDROID || UNITY_IOS)
			var loadResult = await LoadAdAsync(placementId, AdType.Banner, adUnitId);
			await UniTask.SwitchToMainThread();
			return loadResult.Success 
				? AdResult.Succeeded(placementId, AdType.Banner, ProviderName) 
				: AdResult.Failed(placementId, AdType.Banner, loadResult.ErrorType, loadResult.FailureReason);
#else
			await UniTask.CompletedTask;
			return AdResult.Failed(placementId, AdType.Banner, AdErrorType.UnsupportedAdType, "AdMob not available on this platform");
#endif
		}

		public void HideBanner()
		{
			_bannerHidden = true;

#if ADMOB_ENABLED && (UNITY_ANDROID || UNITY_IOS)
			if (!string.IsNullOrEmpty(_currentBannerPlacementId) && 
			    _loadedAds.TryGetValue(_currentBannerPlacementId, out var adInfo) && 
			    adInfo.BannerView != null)
			{
				adInfo.BannerView.Hide();
			}
#endif
		}

		public void DestroyBanner()
		{
#if ADMOB_ENABLED && (UNITY_ANDROID || UNITY_IOS)
			if (!string.IsNullOrEmpty(_currentBannerPlacementId) && 
			    _loadedAds.TryGetValue(_currentBannerPlacementId, out var adInfo))
			{
				adInfo.BannerView?.Destroy();
				adInfo.BannerView = null;
			}
#endif
			_currentBannerPlacementId = null;
			_bannerHidden = false;
		}

		public void SetUserConsent(bool canTrack)
		{
			_userCanTrack = canTrack;
		}

		public void SetUserUnderAge(bool isUnderAge)
		{
			_userUnderAge = isUnderAge;
		}

		public void OnApplicationPause(bool isPaused)
		{
			// Handle app open ad logic when app resumes
		}

#if ADMOB_ENABLED && (UNITY_ANDROID || UNITY_IOS)
		private AdMobAdInfo GetOrCreateAdInfo(string placementId, AdType adType, string adUnitId)
		{
			if (!_loadedAds.TryGetValue(placementId, out var adInfo))
			{
				adInfo = new AdMobAdInfo
				{
					PlacementId = placementId,
					AdType = adType,
					AdUnitId = adUnitId
				};
				_loadedAds[placementId] = adInfo;
			}
			return adInfo;
		}

		private async UniTask<AdLoadResult> LoadRewardedAdAsync(AdMobAdInfo adInfo)
		{
			var tcs = new UniTaskCompletionSource<AdLoadResult>();

			var adRequest = new GoogleMobileAds.Api.AdRequest();
			
			GoogleMobileAds.Api.RewardedAd.Load(adInfo.AdUnitId, adRequest, (ad, error) =>
			{
				if (error != null || ad == null)
				{
					string errorMsg = error?.GetMessage() ?? "Unknown error";
					Debug.LogWarning($"{TAG} Failed to load rewarded ad {adInfo.PlacementId}: {errorMsg}");
					tcs.TrySetResult(AdLoadResult.Failed(adInfo.PlacementId, AdType.Rewarded, AdErrorType.NoFill, errorMsg));
					return;
				}

				adInfo.RewardedAd = ad;
				adInfo.LoadTime = DateTime.UtcNow;
				Debug.Log($"{TAG} Loaded rewarded ad for {adInfo.PlacementId}");
				tcs.TrySetResult(AdLoadResult.Succeeded(adInfo.PlacementId, AdType.Rewarded));
			});

			return await tcs.Task;
		}

		private async UniTask<AdLoadResult> LoadInterstitialAdAsync(AdMobAdInfo adInfo)
		{
			var tcs = new UniTaskCompletionSource<AdLoadResult>();

			var adRequest = new GoogleMobileAds.Api.AdRequest();
			
			GoogleMobileAds.Api.InterstitialAd.Load(adInfo.AdUnitId, adRequest, (ad, error) =>
			{
				if (error != null || ad == null)
				{
					string errorMsg = error?.GetMessage() ?? "Unknown error";
					Debug.LogWarning($"{TAG} Failed to load interstitial ad {adInfo.PlacementId}: {errorMsg}");
					tcs.TrySetResult(AdLoadResult.Failed(adInfo.PlacementId, AdType.Interstitial, AdErrorType.NoFill, errorMsg));
					return;
				}

				adInfo.InterstitialAd = ad;
				adInfo.LoadTime = DateTime.UtcNow;
				Debug.Log($"{TAG} Loaded interstitial ad for {adInfo.PlacementId}");
				tcs.TrySetResult(AdLoadResult.Succeeded(adInfo.PlacementId, AdType.Interstitial));
			});

			return await tcs.Task;
		}

		private async UniTask<AdLoadResult> LoadRewardedInterstitialAdAsync(AdMobAdInfo adInfo)
		{
			var tcs = new UniTaskCompletionSource<AdLoadResult>();

			var adRequest = new GoogleMobileAds.Api.AdRequest();
			
			GoogleMobileAds.Api.RewardedInterstitialAd.Load(adInfo.AdUnitId, adRequest, (ad, error) =>
			{
				if (error != null || ad == null)
				{
					string errorMsg = error?.GetMessage() ?? "Unknown error";
					Debug.LogWarning($"{TAG} Failed to load rewarded interstitial ad {adInfo.PlacementId}: {errorMsg}");
					tcs.TrySetResult(AdLoadResult.Failed(adInfo.PlacementId, AdType.RewardedInterstitial, AdErrorType.NoFill, errorMsg));
					return;
				}

				adInfo.RewardedInterstitialAd = ad;
				adInfo.LoadTime = DateTime.UtcNow;
				Debug.Log($"{TAG} Loaded rewarded interstitial ad for {adInfo.PlacementId}");
				tcs.TrySetResult(AdLoadResult.Succeeded(adInfo.PlacementId, AdType.RewardedInterstitial));
			});

			return await tcs.Task;
		}

		private async UniTask<AdLoadResult> LoadAppOpenAdAsync(AdMobAdInfo adInfo)
		{
			var tcs = new UniTaskCompletionSource<AdLoadResult>();

			var adRequest = new GoogleMobileAds.Api.AdRequest();
			
			GoogleMobileAds.Api.AppOpenAd.Load(adInfo.AdUnitId, adRequest, (ad, error) =>
			{
				if (error != null || ad == null)
				{
					string errorMsg = error?.GetMessage() ?? "Unknown error";
					Debug.LogWarning($"{TAG} Failed to load app open ad {adInfo.PlacementId}: {errorMsg}");
					tcs.TrySetResult(AdLoadResult.Failed(adInfo.PlacementId, AdType.AppOpen, AdErrorType.NoFill, errorMsg));
					return;
				}

				adInfo.AppOpenAd = ad;
				adInfo.LoadTime = DateTime.UtcNow;
				Debug.Log($"{TAG} Loaded app open ad for {adInfo.PlacementId}");
				tcs.TrySetResult(AdLoadResult.Succeeded(adInfo.PlacementId, AdType.AppOpen));
			});

			return await tcs.Task;
		}

		private async UniTask<AdLoadResult> LoadBannerAdAsync(AdMobAdInfo adInfo)
		{
			var tcs = new UniTaskCompletionSource<AdLoadResult>();

			// Create banner view
			var adSize = GoogleMobileAds.Api.AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(GoogleMobileAds.Api.AdSize.FullWidth);
			adInfo.BannerView = new GoogleMobileAds.Api.BannerView(adInfo.AdUnitId, adSize, MapBannerPosition(_currentBannerPosition));

			// Listen for banner events
			adInfo.BannerView.OnBannerAdLoaded += () =>
			{
				Debug.Log($"{TAG} Banner ad loaded for {adInfo.PlacementId}");
				adInfo.LoadTime = DateTime.UtcNow;
				tcs.TrySetResult(AdLoadResult.Succeeded(adInfo.PlacementId, AdType.Banner));
			};

			adInfo.BannerView.OnBannerAdLoadFailed += (error) =>
			{
				Debug.LogWarning($"{TAG} Failed to load banner ad {adInfo.PlacementId}: {error.GetMessage()}");
				tcs.TrySetResult(AdLoadResult.Failed(adInfo.PlacementId, AdType.Banner, AdErrorType.NoFill, error.GetMessage()));
			};

			// Load the ad
			var adRequest = new GoogleMobileAds.Api.AdRequest();
			adInfo.BannerView.LoadAd(adRequest);

			return await tcs.Task;
		}

		private async UniTask<AdResult> ShowRewardedAdAsync(AdMobAdInfo adInfo)
		{
			if (adInfo.RewardedAd == null || !adInfo.RewardedAd.CanShowAd())
			{
				return AdResult.Failed(adInfo.PlacementId, AdType.Rewarded, AdErrorType.NotReady, "Rewarded ad not ready");
			}

			var tcs = new UniTaskCompletionSource<AdResult>();
			bool rewardEarned = false;

			// Register events before showing
			adInfo.RewardedAd.OnAdFullScreenContentClosed += () =>
			{
				adInfo.RewardedAd?.Destroy();
				adInfo.RewardedAd = null;
				tcs.TrySetResult(AdResult.Succeeded(adInfo.PlacementId, AdType.Rewarded, ProviderName, rewardEarned ? 0.01 : 0));
			};

			adInfo.RewardedAd.OnAdFullScreenContentFailed += (error) =>
			{
				adInfo.RewardedAd?.Destroy();
				adInfo.RewardedAd = null;
				tcs.TrySetResult(AdResult.Failed(adInfo.PlacementId, AdType.Rewarded, AdErrorType.InternalError, error.GetMessage()));
			};

			// Show with reward callback
			adInfo.RewardedAd.Show((reward) =>
			{
				Debug.Log($"{TAG} Rewarded ad earned reward: {reward.Amount} {reward.Type}");
				rewardEarned = true;
			});

			return await tcs.Task;
		}

		private async UniTask<AdResult> ShowInterstitialAdAsync(AdMobAdInfo adInfo)
		{
			if (adInfo.InterstitialAd == null || !adInfo.InterstitialAd.CanShowAd())
			{
				return AdResult.Failed(adInfo.PlacementId, AdType.Interstitial, AdErrorType.NotReady, "Interstitial ad not ready");
			}

			var tcs = new UniTaskCompletionSource<AdResult>();

			adInfo.InterstitialAd.OnAdFullScreenContentClosed += () =>
			{
				adInfo.InterstitialAd?.Destroy();
				adInfo.InterstitialAd = null;
				tcs.TrySetResult(AdResult.Succeeded(adInfo.PlacementId, AdType.Interstitial, ProviderName));
			};

			adInfo.InterstitialAd.OnAdFullScreenContentFailed += (error) =>
			{
				adInfo.InterstitialAd?.Destroy();
				adInfo.InterstitialAd = null;
				tcs.TrySetResult(AdResult.Failed(adInfo.PlacementId, AdType.Interstitial, AdErrorType.InternalError, error.GetMessage()));
			};

			adInfo.InterstitialAd.Show();

			return await tcs.Task;
		}

		private async UniTask<AdResult> ShowRewardedInterstitialAdAsync(AdMobAdInfo adInfo)
		{
			if (adInfo.RewardedInterstitialAd == null || !adInfo.RewardedInterstitialAd.CanShowAd())
			{
				return AdResult.Failed(adInfo.PlacementId, AdType.RewardedInterstitial, AdErrorType.NotReady, "Rewarded interstitial ad not ready");
			}

			var tcs = new UniTaskCompletionSource<AdResult>();
			bool rewardEarned = false;

			adInfo.RewardedInterstitialAd.OnAdFullScreenContentClosed += () =>
			{
				adInfo.RewardedInterstitialAd?.Destroy();
				adInfo.RewardedInterstitialAd = null;
				tcs.TrySetResult(AdResult.Succeeded(adInfo.PlacementId, AdType.RewardedInterstitial, ProviderName, rewardEarned ? 0.01 : 0));
			};

			adInfo.RewardedInterstitialAd.OnAdFullScreenContentFailed += (error) =>
			{
				adInfo.RewardedInterstitialAd?.Destroy();
				adInfo.RewardedInterstitialAd = null;
				tcs.TrySetResult(AdResult.Failed(adInfo.PlacementId, AdType.RewardedInterstitial, AdErrorType.InternalError, error.GetMessage()));
			};

			adInfo.RewardedInterstitialAd.Show((reward) =>
			{
				Debug.Log($"{TAG} Rewarded interstitial earned reward: {reward.Amount} {reward.Type}");
				rewardEarned = true;
			});

			return await tcs.Task;
		}

		private async UniTask<AdResult> ShowAppOpenAdAsync(AdMobAdInfo adInfo)
		{
			// Check if app open ad is still valid (usually 4 hours)
			if (adInfo.AppOpenAd == null || !adInfo.AppOpenAd.CanShowAd())
			{
				return AdResult.Failed(adInfo.PlacementId, AdType.AppOpen, AdErrorType.NotReady, "App open ad not ready");
			}

			if ((DateTime.UtcNow - adInfo.LoadTime).TotalHours > 4)
			{
				adInfo.AppOpenAd?.Destroy();
				adInfo.AppOpenAd = null;
				return AdResult.Failed(adInfo.PlacementId, AdType.AppOpen, AdErrorType.NotReady, "App open ad expired");
			}

			var tcs = new UniTaskCompletionSource<AdResult>();

			adInfo.AppOpenAd.OnAdFullScreenContentClosed += () =>
			{
				adInfo.AppOpenAd?.Destroy();
				adInfo.AppOpenAd = null;
				tcs.TrySetResult(AdResult.Succeeded(adInfo.PlacementId, AdType.AppOpen, ProviderName));
			};

			adInfo.AppOpenAd.OnAdFullScreenContentFailed += (error) =>
			{
				adInfo.AppOpenAd?.Destroy();
				adInfo.AppOpenAd = null;
				tcs.TrySetResult(AdResult.Failed(adInfo.PlacementId, AdType.AppOpen, AdErrorType.InternalError, error.GetMessage()));
			};

			adInfo.AppOpenAd.Show();

			return await tcs.Task;
		}

		private static GoogleMobileAds.Api.AdPosition MapBannerPosition(BannerPosition position)
		{
			return position switch
			{
				BannerPosition.Top => GoogleMobileAds.Api.AdPosition.Top,
				BannerPosition.Bottom => GoogleMobileAds.Api.AdPosition.Bottom,
				BannerPosition.TopLeft => GoogleMobileAds.Api.AdPosition.TopLeft,
				BannerPosition.TopRight => GoogleMobileAds.Api.AdPosition.TopRight,
				BannerPosition.BottomLeft => GoogleMobileAds.Api.AdPosition.BottomLeft,
				BannerPosition.BottomRight => GoogleMobileAds.Api.AdPosition.BottomRight,
				BannerPosition.Center => GoogleMobileAds.Api.AdPosition.Center,
				_ => GoogleMobileAds.Api.AdPosition.Bottom
			};
		}
#endif

		/// <summary>
		/// Internal class to track loaded ad information.
		/// </summary>
		private class AdMobAdInfo
		{
			public string PlacementId;
			public AdType AdType;
			public string AdUnitId;
			public DateTime LoadTime;

#if ADMOB_ENABLED && (UNITY_ANDROID || UNITY_IOS)
			public GoogleMobileAds.Api.RewardedAd RewardedAd;
			public GoogleMobileAds.Api.InterstitialAd InterstitialAd;
			public GoogleMobileAds.Api.RewardedInterstitialAd RewardedInterstitialAd;
			public GoogleMobileAds.Api.AppOpenAd AppOpenAd;
			public GoogleMobileAds.Api.BannerView BannerView;

			public bool IsAdReady()
			{
				return AdType switch
				{
					AdType.Rewarded => RewardedAd != null && RewardedAd.CanShowAd(),
					AdType.Interstitial => InterstitialAd != null && InterstitialAd.CanShowAd(),
					AdType.RewardedInterstitial => RewardedInterstitialAd != null && RewardedInterstitialAd.CanShowAd(),
					AdType.AppOpen => AppOpenAd != null && AppOpenAd.CanShowAd() && (DateTime.UtcNow - LoadTime).TotalHours <= 4,
					AdType.Banner => BannerView != null,
					_ => false
				};
			}
#else
			public bool IsAdReady() => false;
#endif
		}
	}
}