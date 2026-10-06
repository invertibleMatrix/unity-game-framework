using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core.Threading;
using AK.CoreDomain.Ads;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services.Ads.Providers
{
	/// <summary>
	/// AppLovin MAX implementation of IAdProvider.
	/// Supports Rewarded, Interstitial, Banner, and App Open ads.
	/// Rewarded Interstitial is not a MAX format — those placements return UnsupportedAdType.
	/// SDK key is read from the AppLovin Integration Manager; do not pass it here.
	/// Lives in AK.Services.MaxAds so AK.Services does not reference MaxSdk.Scripts.
	/// This assembly is compiled only when com.applovin.mediation.ads is installed
	/// (versionDefine UGFW_MAX_SDK).
	///
	/// Fullscreen ads run through a <see cref="FullscreenAdDriver"/>. Every MAX callback is posted
	/// through a <see cref="MainThreadInbox"/>, because MAX raises some on a background thread
	/// (ad revenue, unless <c>MaxSdkBase.InvokeEventsOnUnityMainThread</c> is set) and this
	/// provider's state is main-thread only.
	/// </summary>
	public sealed class MaxAdProvider : IAdProvider, IDisposable
	{
		private const string TAG = "[MaxAdProvider]";

		private static readonly AdType[] Supported =
		{
			AdType.Rewarded,
			AdType.Interstitial,
			AdType.Banner,
			AdType.AppOpen
		};

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void RegisterFactory()
		{
			AdsProviderFactory.Max = static () => new MaxAdProvider();
		}

		private readonly MainThreadInbox _inbox = new();
		private readonly FullscreenAdDriver _driver;
		private readonly IAdsClock _clock;
		private readonly FullscreenAdTimeouts _timeouts;
		private readonly Dictionary<string, string> _unitByPlacement = new();

		private UniTaskCompletionSource<bool> _initializing;
		private bool _isInitialized;
		private bool _subscribed;
		private bool _disposed;
		private bool _userCanTrack = true;
		private bool _userUnderAge;

		// MAX shows one banner at a time.
		private string _bannerAdUnitId;
		private BannerPosition _bannerPosition = BannerPosition.Bottom;
		private bool _bannerCreated;
		private bool _bannerReady;
		private bool _bannerHidden;
		private UniTaskCompletionSource<AdLoadResult> _bannerLoad;
		private string _bannerLoadPlacementId;
		private CancellationTokenSource _bannerWatchdog;

		public MaxAdProvider()
			: this(new ForegroundAdsClock(), FullscreenAdTimeouts.Default)
		{
		}

		public MaxAdProvider(IAdsClock clock, FullscreenAdTimeouts timeouts)
		{
			_clock    = clock ?? throw new ArgumentNullException(nameof(clock));
			_timeouts = timeouts;
			_driver   = new FullscreenAdDriver(new Sdk(), clock, timeouts, "AppLovin MAX");
		}

		public string                ProviderName     => "AppLovin MAX";
		public int                   Priority         => 100;
		public bool                  IsInitialized    => _isInitialized && !_disposed;
		public IReadOnlyList<AdType> SupportedAdTypes => Supported;

		/// <summary>
		/// Starts MAX, and completes when MAX reports back, however long that takes. A second
		/// call while it starts joins the first.
		/// </summary>
		public UniTask<bool> InitializeAsync(IEnumerable<AdPlacementRegistration> placements)
		{
			if (_disposed)
			{
				return UniTask.FromResult(false);
			}

			if (_isInitialized)
			{
				Debug.LogWarning($"{TAG} Already initialized");
				return UniTask.FromResult(true);
			}

			if (_initializing != null)
			{
				return _initializing.Task;
			}

			if (_userUnderAge)
			{
				Debug.LogWarning($"{TAG} Refusing to initialize — AppLovin policy prohibits using MAX for child users");
				return UniTask.FromResult(false);
			}

			var unitIds = new List<string>();
			if (placements != null)
			{
				foreach (AdPlacementRegistration placement in placements)
				{
					if (string.IsNullOrEmpty(placement.AdUnitId))
					{
						continue;
					}

					Bind(placement.PlacementId, placement.AdUnitId);
					if (!unitIds.Contains(placement.AdUnitId))
					{
						unitIds.Add(placement.AdUnitId);
					}
				}
			}

			try
			{
				Subscribe();
				MaxSdk.SetHasUserConsent(_userCanTrack);

				if (MaxSdk.IsInitialized())
				{
					_isInitialized = true;
					Debug.Log($"{TAG} SDK already initialized ({MaxSdk.Version})");
					return UniTask.FromResult(true);
				}

				_initializing = new UniTaskCompletionSource<bool>();

				// Taken before starting MAX, which may answer before InitializeSdk returns.
				UniTask<bool> initialized = _initializing.Task;
				MaxSdk.InitializeSdk(unitIds.Count > 0 ? unitIds.ToArray() : null);
				return initialized;
			}
			catch (Exception e)
			{
				Debug.LogError($"{TAG} Initialization exception: {e.Message}");
				FinishInitialization(false);
				return UniTask.FromResult(false);
			}
		}

		public bool IsAdReady(string placementId, AdType adType)
		{
			if (!IsInitialized || string.IsNullOrEmpty(placementId) || !_unitByPlacement.TryGetValue(placementId, out string adUnitId))
			{
				return false;
			}

			return adType == AdType.Banner
				? _bannerReady && adUnitId == _bannerAdUnitId
				: _driver.IsReady(adType, adUnitId);
		}

		public UniTask<AdLoadResult> LoadAdAsync(string placementId, AdType adType, string adUnitId)
		{
			if (!IsInitialized)
			{
				return UniTask.FromResult(AdLoadResult.Failed(placementId, adType, AdErrorType.NotInitialized, "Provider not initialized"));
			}

			if (!Supports(adType))
			{
				return UniTask.FromResult(AdLoadResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, $"MAX has no {adType} format"));
			}

			if (string.IsNullOrEmpty(adUnitId))
			{
				return UniTask.FromResult(AdLoadResult.Failed(placementId, adType, AdErrorType.InvalidPlacement, "Ad unit ID is empty"));
			}

			Bind(placementId, adUnitId);

			return adType == AdType.Banner
				? LoadBannerAsync(placementId, adUnitId)
				: _driver.LoadAsync(placementId, adType, adUnitId);
		}

		public UniTask<AdResult> ShowAdAsync(string placementId, AdType adType, string adUnitId)
		{
			if (!IsInitialized)
			{
				return UniTask.FromResult(AdResult.Failed(placementId, adType, AdErrorType.NotInitialized, "Provider not initialized"));
			}

			if (adType == AdType.Banner)
			{
				return ShowBannerAsync(placementId, adUnitId, BannerPosition.Bottom);
			}

			if (!Supports(adType))
			{
				return UniTask.FromResult(AdResult.Failed(placementId, adType, AdErrorType.UnsupportedAdType, $"MAX has no {adType} format"));
			}

			Bind(placementId, adUnitId);
			return _driver.ShowAsync(placementId, adType, adUnitId);
		}

		public async UniTask<AdResult> ShowBannerAsync(string placementId, string adUnitId, BannerPosition position)
		{
			if (!IsInitialized)
			{
				return AdResult.Failed(placementId, AdType.Banner, AdErrorType.NotInitialized, "Provider not initialized");
			}

			if (string.IsNullOrEmpty(adUnitId))
			{
				return AdResult.Failed(placementId, AdType.Banner, AdErrorType.InvalidPlacement, "Ad unit ID is empty");
			}

			Bind(placementId, adUnitId);

			try
			{
				if (_bannerCreated && _bannerAdUnitId == adUnitId && _bannerPosition != position)
				{
					MaxSdk.UpdateBannerPosition(adUnitId, MapBannerPosition(position));
				}

				_bannerPosition = position;
				_bannerHidden   = false;

				if (!_bannerReady || _bannerAdUnitId != adUnitId)
				{
					AdLoadResult load = await LoadBannerAsync(placementId, adUnitId);
					if (!load.Success)
					{
						return AdResult.Failed(placementId, AdType.Banner, load.ErrorType, load.FailureReason);
					}
				}

				// Hidden while it loaded: it shows on the next ShowBannerAsync.
				if (!_bannerHidden && _bannerAdUnitId == adUnitId)
				{
					MaxSdk.ShowBanner(adUnitId);
				}

				return AdResult.Succeeded(placementId, AdType.Banner, ProviderName);
			}
			catch (Exception e)
			{
				Debug.LogError($"{TAG} ShowBannerAsync exception: {e.Message}");
				return AdResult.Failed(placementId, AdType.Banner, AdErrorType.InternalError, e.Message);
			}
		}

		public void HideBanner()
		{
			_bannerHidden = true;
			if (_bannerCreated)
			{
				MaxSdk.HideBanner(_bannerAdUnitId);
			}
		}

		public void DestroyBanner()
		{
			if (_bannerCreated)
			{
				MaxSdk.DestroyBanner(_bannerAdUnitId);
			}

			_bannerAdUnitId = null;
			_bannerCreated  = false;
			_bannerReady    = false;
			_bannerHidden   = false;

			FinishBannerLoad(AdLoadResult.Failed(_bannerLoadPlacementId, AdType.Banner, AdErrorType.NotReady, "The banner was destroyed"));
		}

		public void SetUserConsent(bool canTrack)
		{
			_userCanTrack = canTrack;
			if (_isInitialized || MaxSdk.IsInitialized())
				MaxSdk.SetHasUserConsent(canTrack);
		}

		public void SetUserUnderAge(bool isUnderAge)
		{
			_userUnderAge = isUnderAge;
		}

		public void OnApplicationPause(bool isPaused)
		{
			// App-open display is owned by AdService.TryShowAppOpenAdAsync so frequency caps still apply.
		}

		/// <summary>
		/// Unhooks every MAX callback, fails whatever is still loading or showing, and
		/// destroys the banner.
		/// </summary>
		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			Unsubscribe();
			_driver.Dispose();
			FinishInitialization(false);
			FinishBannerLoad(AdLoadResult.Failed(_bannerLoadPlacementId, AdType.Banner, AdErrorType.NotInitialized, "The ad provider was disposed."));

			try
			{
				DestroyBanner();
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		private bool Supports(AdType adType)
		{
			foreach (AdType supported in Supported)
			{
				if (supported == adType)
				{
					return true;
				}
			}

			return false;
		}

		private void Bind(string placementId, string adUnitId)
		{
			if (!string.IsNullOrEmpty(placementId) && !string.IsNullOrEmpty(adUnitId))
				_unitByPlacement[placementId] = adUnitId;
		}

		private void FinishInitialization(bool succeeded)
		{
			UniTaskCompletionSource<bool> initializing = _initializing;
			if (initializing == null)
			{
				return;
			}

			_initializing  = null;
			_isInitialized = succeeded && !_disposed;

			if (_isInitialized)
			{
				Debug.Log($"{TAG} Initialization complete ({MaxSdk.Version})");
			}
			else if (!_disposed)
			{
				Debug.LogError($"{TAG} Initialization failed");
			}

			initializing.TrySetResult(_isInitialized);
		}

		#region Banner

		/// <summary>Loads the banner, joining a load already running for it.</summary>
		private UniTask<AdLoadResult> LoadBannerAsync(string placementId, string adUnitId)
		{
			if (_bannerAdUnitId == adUnitId)
			{
				if (_bannerLoad != null)
				{
					return _bannerLoad.Task;
				}

				if (_bannerReady)
				{
					return UniTask.FromResult(AdLoadResult.Succeeded(placementId, AdType.Banner));
				}
			}
			else if (_bannerCreated)
			{
				DestroyBanner();
			}

			var load = new UniTaskCompletionSource<AdLoadResult>();
			_bannerLoad            = load;
			_bannerLoadPlacementId = placementId;

			// Taken before calling MAX, which may answer before it returns.
			UniTask<AdLoadResult> loaded = load.Task;
			StartBannerWatchdog(load);

			try
			{
				if (_bannerCreated)
				{
					MaxSdk.LoadBanner(adUnitId);
				}
				else
				{
					// Set first: creating the banner starts its load, whose callback checks the unit.
					_bannerAdUnitId = adUnitId;
					_bannerReady    = false;
					MaxSdk.CreateBanner(adUnitId, new MaxSdkBase.AdViewConfiguration(MapBannerPosition(_bannerPosition)));
					_bannerCreated = true;
					MaxSdk.SetBannerBackgroundColor(adUnitId, Color.black);
				}
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				if (_bannerLoad == load)
				{
					FinishBannerLoad(AdLoadResult.Failed(placementId, AdType.Banner, AdErrorType.InternalError, e.Message));
				}
			}

			return loaded;
		}

		private void StartBannerWatchdog(UniTaskCompletionSource<AdLoadResult> load)
		{
			StopBannerWatchdog();
			var watchdog = new CancellationTokenSource();
			_bannerWatchdog = watchdog;
			ExpireBannerLoadAsync(load, watchdog).Forget();
		}

		private async UniTaskVoid ExpireBannerLoadAsync(UniTaskCompletionSource<AdLoadResult> load, CancellationTokenSource watchdog)
		{
			if (await _clock.Delay(_timeouts.Load, watchdog.Token).SuppressCancellationThrow() || _bannerLoad != load)
			{
				return;
			}

			Debug.LogWarning($"{TAG} Banner {_bannerAdUnitId}: no load result within {_timeouts.Load:0.#}s.");
			FinishBannerLoad(AdLoadResult.Failed(_bannerLoadPlacementId, AdType.Banner, AdErrorType.Timeout, $"No load result within {_timeouts.Load:0.#}s"));
		}

		private void StopBannerWatchdog()
		{
			CancellationTokenSource watchdog = _bannerWatchdog;
			if (watchdog == null)
			{
				return;
			}

			_bannerWatchdog = null;
			watchdog.Cancel();
			watchdog.Dispose();
		}

		private void FinishBannerLoad(AdLoadResult result)
		{
			UniTaskCompletionSource<AdLoadResult> load = _bannerLoad;
			if (load == null)
			{
				return;
			}

			_bannerLoad = null;
			StopBannerWatchdog();
			load.TrySetResult(result);
		}

		private void BannerLoaded(string adUnitId)
		{
			if (adUnitId != _bannerAdUnitId)
			{
				return;
			}

			_bannerReady = true;
			FinishBannerLoad(AdLoadResult.Succeeded(_bannerLoadPlacementId, AdType.Banner));
		}

		private void BannerLoadFailed(string adUnitId, AdErrorType errorType, string message)
		{
			if (adUnitId != _bannerAdUnitId)
			{
				return;
			}

			_bannerReady = false;
			FinishBannerLoad(AdLoadResult.Failed(_bannerLoadPlacementId, AdType.Banner, errorType, message ?? "The banner failed to load"));
		}

		#endregion

		#region MAX callbacks

		// Each callback reads what it needs from MAX's objects on the thread it arrives on, then
		// posts the rest to the main thread.

		private void Subscribe()
		{
			if (_subscribed)
			{
				return;
			}

			_subscribed = true;

			MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;

			MaxSdkCallbacks.Rewarded.OnAdLoadedEvent         += OnAdLoaded;
			MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent     += OnAdLoadFailed;
			MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent      += OnAdDisplayed;
			MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent  += OnAdDisplayFailed;
			MaxSdkCallbacks.Rewarded.OnAdHiddenEvent         += OnAdHidden;
			MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnAdReceivedReward;
			MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent    += OnAdRevenuePaid;

			MaxSdkCallbacks.Interstitial.OnAdLoadedEvent        += OnAdLoaded;
			MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent    += OnAdLoadFailed;
			MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent     += OnAdDisplayed;
			MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnAdDisplayFailed;
			MaxSdkCallbacks.Interstitial.OnAdHiddenEvent        += OnAdHidden;
			MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent   += OnAdRevenuePaid;

			MaxSdkCallbacks.AppOpen.OnAdLoadedEvent        += OnAdLoaded;
			MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent    += OnAdLoadFailed;
			MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent     += OnAdDisplayed;
			MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += OnAdDisplayFailed;
			MaxSdkCallbacks.AppOpen.OnAdHiddenEvent        += OnAdHidden;
			MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent   += OnAdRevenuePaid;

			MaxSdkCallbacks.Banner.OnAdLoadedEvent     += OnBannerLoaded;
			MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerLoadFailed;
		}

		private void Unsubscribe()
		{
			if (!_subscribed)
			{
				return;
			}

			_subscribed = false;

			MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;

			MaxSdkCallbacks.Rewarded.OnAdLoadedEvent         -= OnAdLoaded;
			MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent     -= OnAdLoadFailed;
			MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent      -= OnAdDisplayed;
			MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent  -= OnAdDisplayFailed;
			MaxSdkCallbacks.Rewarded.OnAdHiddenEvent         -= OnAdHidden;
			MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnAdReceivedReward;
			MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent    -= OnAdRevenuePaid;

			MaxSdkCallbacks.Interstitial.OnAdLoadedEvent        -= OnAdLoaded;
			MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent    -= OnAdLoadFailed;
			MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent     -= OnAdDisplayed;
			MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= OnAdDisplayFailed;
			MaxSdkCallbacks.Interstitial.OnAdHiddenEvent        -= OnAdHidden;
			MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent   -= OnAdRevenuePaid;

			MaxSdkCallbacks.AppOpen.OnAdLoadedEvent        -= OnAdLoaded;
			MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent    -= OnAdLoadFailed;
			MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent     -= OnAdDisplayed;
			MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent -= OnAdDisplayFailed;
			MaxSdkCallbacks.AppOpen.OnAdHiddenEvent        -= OnAdHidden;
			MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent   -= OnAdRevenuePaid;

			MaxSdkCallbacks.Banner.OnAdLoadedEvent     -= OnBannerLoaded;
			MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= OnBannerLoadFailed;
		}

		private void OnSdkInitialized(MaxSdkBase.SdkConfiguration configuration)
		{
			bool succeeded = configuration != null && configuration.IsSuccessfullyInitialized;
			_inbox.Post(() => FinishInitialization(succeeded));
		}

		private void OnAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
		{
			_inbox.Post(() => _driver.OnLoaded(adUnitId));
		}

		private void OnAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
		{
			AdErrorType errorType = MapError(errorInfo);
			string      message   = errorInfo?.Message;
			_inbox.Post(() => _driver.OnLoadFailed(adUnitId, errorType, message));
		}

		private void OnAdDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
		{
			string network = adInfo?.NetworkName;
			_inbox.Post(() => _driver.OnDisplayed(adUnitId, network));
		}

		private void OnAdDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
		{
			AdErrorType errorType = MapError(errorInfo);
			string      message   = errorInfo?.Message;
			_inbox.Post(() => _driver.OnDisplayFailed(adUnitId, errorType, message));
		}

		private void OnAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
		{
			string network = adInfo?.NetworkName;
			_inbox.Post(() => _driver.OnHidden(adUnitId, network));
		}

		private void OnAdReceivedReward(string adUnitId, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo)
		{
			_inbox.Post(() => _driver.OnRewardEarned(adUnitId));
		}

		private void OnAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
		{
			if (adInfo == null)
			{
				return;
			}

			double revenue = adInfo.Revenue;
			string network = adInfo.NetworkName;
			_inbox.Post(() => _driver.OnRevenuePaid(adUnitId, revenue, network));
		}

		private void OnBannerLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
		{
			_inbox.Post(() => BannerLoaded(adUnitId));
		}

		private void OnBannerLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
		{
			AdErrorType errorType = MapError(errorInfo);
			string      message   = errorInfo?.Message;
			_inbox.Post(() => BannerLoadFailed(adUnitId, errorType, message));
		}

		#endregion

		/// <summary>MAX's error codes for loads and shows, in the service's terms. Codes it doesn't know are <see cref="AdErrorType.Unknown"/>.</summary>
		private static AdErrorType MapError(MaxSdkBase.ErrorInfo errorInfo)
		{
			if (errorInfo == null)
				return AdErrorType.Unknown;

			switch (errorInfo.Code)
			{
				case MaxSdkBase.ErrorCode.NoFill:
				// Every mediated network was tried and none loaded: no ad to be had right now.
				case MaxSdkBase.ErrorCode.AdLoadFailed:
					return AdErrorType.NoFill;

				case MaxSdkBase.ErrorCode.NetworkError:
				case MaxSdkBase.ErrorCode.NetworkTimeout:
				case MaxSdkBase.ErrorCode.NoNetwork:
					return AdErrorType.NetworkError;

				case MaxSdkBase.ErrorCode.InvalidAdUnitId:
					return AdErrorType.InvalidPlacement;

				case MaxSdkBase.ErrorCode.FullscreenAdAlreadyShowing:
				case MaxSdkBase.ErrorCode.FullscreenAdLoadWhileShowing:
					return AdErrorType.AlreadyShowing;

				case MaxSdkBase.ErrorCode.FullscreenAdNotReady:
					return AdErrorType.NotReady;

				case MaxSdkBase.ErrorCode.FullscreenAdAlreadyLoading:
				case MaxSdkBase.ErrorCode.AdDisplayFailed:
					return AdErrorType.InternalError;

				default:
					return AdErrorType.Unknown;
			}
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

		/// <summary>MAX's fullscreen calls, for the driver.</summary>
		private sealed class Sdk : IFullscreenAdSdk
		{
			public bool IsLoaded(AdType adType, string adUnitId) => adType switch
			{
				AdType.Rewarded     => MaxSdk.IsRewardedAdReady(adUnitId),
				AdType.Interstitial => MaxSdk.IsInterstitialReady(adUnitId),
				AdType.AppOpen      => MaxSdk.IsAppOpenAdReady(adUnitId),
				_                   => false
			};

			public void Load(AdType adType, string adUnitId)
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
					default:
						throw new NotSupportedException($"MAX has no {adType} format.");
				}
			}

			public void Show(AdType adType, string adUnitId, string placementId)
			{
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
						throw new NotSupportedException($"MAX has no {adType} format.");
				}
			}
		}
	}
}
