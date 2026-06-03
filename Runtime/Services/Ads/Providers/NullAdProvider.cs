using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using AK.CoreDomain.Ads;
using UnityEngine;

namespace AK.Services.Ads.Providers
{
	/// <summary>
	/// A null/no-op implementation of IAdProvider.
	/// Useful for testing, development builds, or as a fallback when no other providers are available.
	/// Always returns success for rewarded ads (simulates successful ad completion).
	/// </summary>
	public class NullAdProvider : IAdProvider
	{
		private const string TAG = "[NullAdProvider]";

		public string ProviderName => "NullProvider";
		public int Priority => int.MinValue; // Lowest priority
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
		private bool _simulateAds = true;
		private float _simulateLoadDelay = 0.1f;
		private float _simulateShowDelay = 0.5f;

		/// <summary>
		/// Creates a new NullAdProvider.
		/// </summary>
		/// <param name="simulateAds">Whether to simulate successful ad operations.</param>
		/// <param name="simulateLoadDelay">Simulated load delay in seconds.</param>
		/// <param name="simulateShowDelay">Simulated show delay in seconds.</param>
		public NullAdProvider(bool simulateAds = true, float simulateLoadDelay = 0.1f, float simulateShowDelay = 0.5f)
		{
			_simulateAds = simulateAds;
			_simulateLoadDelay = simulateLoadDelay;
			_simulateShowDelay = simulateShowDelay;
		}

		public UniTask<bool> InitializeAsync(IEnumerable<AdPlacementRegistration> placements)
		{
			if (_isInitialized)
			{
				Debug.LogWarning($"{TAG} Already initialized");
				return UniTask.FromResult(true);
			}

			_isInitialized = true;
			Debug.Log($"{TAG} Initialized (simulating ads: {_simulateAds})");
			return UniTask.FromResult(true);
		}

		public bool IsAdReady(string placementId, AdType adType)
		{
			return _isInitialized && _simulateAds;
		}

		public async UniTask<AdLoadResult> LoadAdAsync(string placementId, AdType adType, string adUnitId)
		{
			if (!_isInitialized)
			{
				return AdLoadResult.Failed(placementId, adType, AdErrorType.NotInitialized, "Provider not initialized");
			}

			if (_simulateLoadDelay > 0)
			{
				await UniTask.Delay((int)(_simulateLoadDelay * 1000));
			}

			if (_simulateAds)
			{
				Debug.Log($"{TAG} Simulated load success for {placementId} ({adType})");
				return AdLoadResult.Succeeded(placementId, adType);
			}

			return AdLoadResult.Failed(placementId, adType, AdErrorType.NoFill, "Null provider - ads disabled");
		}

		public async UniTask<AdResult> ShowAdAsync(string placementId, AdType adType, string adUnitId)
		{
			if (!_isInitialized)
			{
				return AdResult.Failed(placementId, adType, AdErrorType.NotInitialized, "Provider not initialized");
			}

			if (!_simulateAds)
			{
				return AdResult.Failed(placementId, adType, AdErrorType.NoFill, "Null provider - ads disabled");
			}

			if (_simulateShowDelay > 0)
			{
				await UniTask.Delay((int)(_simulateShowDelay * 1000));
			}

			Debug.Log($"{TAG} Simulated show success for {placementId} ({adType})");
			return AdResult.Succeeded(placementId, adType, ProviderName);
		}

		public async UniTask<AdResult> ShowBannerAsync(string placementId, string adUnitId, BannerPosition position)
		{
			if (!_isInitialized)
			{
				return AdResult.Failed(placementId, AdType.Banner, AdErrorType.NotInitialized, "Provider not initialized");
			}

			if (!_simulateAds)
			{
				return AdResult.Failed(placementId, AdType.Banner, AdErrorType.NoFill, "Null provider - ads disabled");
			}

			await UniTask.Delay((int)(_simulateLoadDelay * 1000));
			Debug.Log($"{TAG} Simulated banner shown for {placementId}");
			return AdResult.Succeeded(placementId, AdType.Banner, ProviderName);
		}

		public void HideBanner()
		{
			Debug.Log($"{TAG} HideBanner called");
		}

		public void DestroyBanner()
		{
			Debug.Log($"{TAG} DestroyBanner called");
		}

		public void SetUserConsent(bool canTrack)
		{
			Debug.Log($"{TAG} SetUserConsent: {canTrack}");
		}

		public void SetUserUnderAge(bool isUnderAge)
		{
			Debug.Log($"{TAG} SetUserUnderAge: {isUnderAge}");
		}

		public void OnApplicationPause(bool isPaused)
		{
			// No-op
		}
	}
}