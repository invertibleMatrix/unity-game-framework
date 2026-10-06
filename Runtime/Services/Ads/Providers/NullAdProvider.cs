using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using AK.Core.Extensions;
using AK.CoreDomain.Ads;
using AK.Kernel.Timing;
using UnityEngine;

namespace AK.Services.Ads.Providers
{
	/// <summary>
	/// A stand-in ad network for the editor, tests and builds without one. It acts like a real
	/// network: a load fills its ad unit after a short delay, and a show plays the loaded ad to
	/// the end and uses it up. A simulated rewarded ad always earns its reward. With simulation
	/// off, every load comes back with no fill.
	/// Lowest priority, so a real network gets the first try at every load and show.
	/// Delays run on unscaled time.
	/// </summary>
	public sealed class NullAdProvider : IAdProvider
	{
		private const string TAG = "[NullAdProvider]";

		private static readonly AdType[] Supported =
		{
			AdType.Rewarded,
			AdType.Interstitial,
			AdType.Banner,
			AdType.AppOpen,
			AdType.RewardedInterstitial
		};

		private readonly Dictionary<string, string> _unitByPlacement = new();
		private readonly HashSet<string> _loadedUnits = new();
		private readonly bool _simulateAds;
		private readonly float _simulateLoadDelay;
		private readonly float _simulateShowDelay;
		private bool _isInitialized;
		private bool _showing;

		/// <summary>
		/// Creates a new NullAdProvider.
		/// </summary>
		/// <param name="simulateAds">Whether loads fill and shows play. Off, every load fails with no fill.</param>
		/// <param name="simulateLoadDelay">Simulated load delay in seconds.</param>
		/// <param name="simulateShowDelay">Simulated show delay in seconds.</param>
		public NullAdProvider(bool simulateAds = true, float simulateLoadDelay = 0.1f, float simulateShowDelay = 0.5f)
		{
			_simulateAds       = simulateAds;
			_simulateLoadDelay = simulateLoadDelay;
			_simulateShowDelay = simulateShowDelay;
		}

		public string ProviderName => "NullProvider";
		public int Priority => int.MinValue; // Lowest priority
		public bool IsInitialized => _isInitialized;
		public IReadOnlyList<AdType> SupportedAdTypes => Supported;

		public UniTask<bool> InitializeAsync(IEnumerable<AdPlacementRegistration> placements)
		{
			if (placements != null)
			{
				foreach (AdPlacementRegistration placement in placements)
				{
					UnitFor(placement.PlacementId, placement.AdUnitId);
				}
			}

			if (!_isInitialized)
			{
				_isInitialized = true;
				Debug.Log($"{TAG} Initialized (simulating ads: {_simulateAds})");
			}

			return UniTask.FromResult(true);
		}

		public bool IsAdReady(string placementId, AdType adType)
		{
			return _isInitialized && !string.IsNullOrEmpty(placementId) && _loadedUnits.Contains(UnitOf(placementId));
		}

		public async UniTask<AdLoadResult> LoadAdAsync(string placementId, AdType adType, string adUnitId)
		{
			if (!_isInitialized)
			{
				return AdLoadResult.Failed(placementId, adType, AdErrorType.NotInitialized, "Provider not initialized");
			}

			string unit = UnitFor(placementId, adUnitId);
			if (unit == null)
			{
				return AdLoadResult.Failed(placementId, adType, AdErrorType.InvalidPlacement, "Placement ID and ad unit ID are empty");
			}

			if (_loadedUnits.Contains(unit))
			{
				return AdLoadResult.Succeeded(placementId, adType);
			}

			await DelayAsync(_simulateLoadDelay);

			if (!_simulateAds)
			{
				return AdLoadResult.Failed(placementId, adType, AdErrorType.NoFill, "Null provider - ads disabled");
			}

			_loadedUnits.Add(unit);
			Debug.Log($"{TAG} Simulated load success for {placementId} ({adType})");
			return AdLoadResult.Succeeded(placementId, adType);
		}

		public async UniTask<AdResult> ShowAdAsync(string placementId, AdType adType, string adUnitId)
		{
			if (!_isInitialized)
			{
				return AdResult.Failed(placementId, adType, AdErrorType.NotInitialized, "Provider not initialized");
			}

			if (adType == AdType.Banner)
			{
				return await ShowBannerAsync(placementId, adUnitId, BannerPosition.Bottom);
			}

			if (_showing)
			{
				return AdResult.Failed(placementId, adType, AdErrorType.AlreadyShowing, "Another simulated ad is showing");
			}

			string unit = UnitFor(placementId, adUnitId);
			if (unit == null || !_loadedUnits.Remove(unit))
			{
				return AdResult.Failed(placementId, adType, AdErrorType.NotReady, "No simulated ad is loaded");
			}

			_showing = true;
			try
			{
				await DelayAsync(_simulateShowDelay);
			}
			finally
			{
				_showing = false;
			}

			Debug.Log($"{TAG} Simulated show success for {placementId} ({adType})");

			// A simulated rewarded ad always plays to completion, so the reward is earned.
			bool isRewarded = adType is AdType.Rewarded or AdType.RewardedInterstitial;
			return AdResult.Succeeded(placementId, adType, ProviderName, rewardGranted: isRewarded);
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

			await DelayAsync(_simulateLoadDelay);
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

		/// <summary>Remembers which unit a placement uses; a placement without one is its own unit.</summary>
		private string UnitFor(string placementId, string adUnitId)
		{
			string unit = string.IsNullOrEmpty(adUnitId) ? placementId : adUnitId;
			if (string.IsNullOrEmpty(unit))
			{
				return null;
			}

			if (!string.IsNullOrEmpty(placementId))
			{
				_unitByPlacement[placementId] = unit;
			}

			return unit;
		}

		private string UnitOf(string placementId) =>
			_unitByPlacement.TryGetValue(placementId, out string unit) ? unit : placementId;

		private static UniTask DelayAsync(float seconds) => TimeDomain.Unscaled.Delay(seconds);
	}
}
