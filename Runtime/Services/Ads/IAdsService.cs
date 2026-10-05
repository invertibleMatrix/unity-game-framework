using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using AK.CoreDomain;
using AK.CoreDomain.Ads;

namespace AK.Services
{
	/// <summary>
	/// Main interface for the Ads Service.
	/// Provides a unified API for showing ads across multiple ad networks.
	/// Supports multiple providers with priority-based waterfall mediation.
	///
	/// Ads load in the background: initialization and level refreshes start loads and return
	/// without waiting for them, and a show whose ad isn't loaded yet waits a bounded time for
	/// it. Cancelling a call's token stops the caller waiting; an ad already on screen plays out.
	/// </summary>
	public interface IAdsService
	{
		/// <summary>
		/// Whether <see cref="InitializeAsync"/> has set the service up. Providers may still be
		/// starting; <see cref="IsAdReady(AdPlacementDefinition)"/> says what can show now.
		/// </summary>
		bool IsInitialized { get; }

		/// <summary>
		/// Whether ads are currently disabled (e.g., user purchased "Remove Ads"). While
		/// disabled, nothing loads in the background.
		/// </summary>
		bool AdsDisabled { get; set; }

		/// <summary>
		/// Event raised when ads are disabled (e.g., after IAP purchase).
		/// </summary>
		event Action OnAdsDisabled;

		/// <summary>
		/// Raised when an ad reached the screen: an impression, whether or not it completed.
		/// </summary>
		event Action<AdPlacementDefinition> OnAdShown;

		/// <summary>
		/// Raised when a show doesn't complete: refused, not loaded, failed, or closed before
		/// its reward.
		/// </summary>
		event Action<AdPlacementDefinition, AdErrorType, string> OnAdFailed;

		/// <summary>
		/// Raised after a show attempt finishes, success or failure, with the full result (including revenue when known).
		/// </summary>
		event Action<AdPlacementDefinition, AdResult> OnAdShowFinished;

		/// <summary>
		/// Event raised when a rewarded ad completes and reward should be granted.
		/// </summary>
		event Action<AdPlacementDefinition> OnAdRewardGranted;

		/// <summary>
		/// Initializes the ads service with ad placements from the meta data, and starts loading
		/// the placements that preload. Waits a bounded time for the ad providers to start, and
		/// never for ads to load; a provider that starts later is picked up then.
		/// Must be called before any other operations.
		/// </summary>
		/// <param name="playerLevel">The player level that placement level limits apply to.</param>
		/// <returns>True if an ad provider is ready.</returns>
		UniTask<bool> InitializeAsync(AdsMeta adsMeta, int playerLevel, CancellationToken cancellationToken = default);

		/// <summary>
		/// Checks if an ad is ready to be shown for the given placement.
		/// </summary>
		/// <param name="placement">The ad placement definition to check.</param>
		/// <returns>True if an ad is loaded and ready.</returns>
		bool IsAdReady(AdPlacementDefinition placement);

		/// <summary>
		/// Checks if an ad is ready to be shown for the given placement ID.
		/// </summary>
		/// <param name="placementId">The placement ID to check.</param>
		/// <returns>True if an ad is loaded and ready.</returns>
		bool IsAdReady(string placementId);

		/// <summary>
		/// Checks if an ad is ready for the given ad type: whether any placement of that type
		/// can show now.
		/// </summary>
		/// <param name="adType">The ad type to check.</param>
		/// <returns>True if an ad is loaded and ready.</returns>
		bool IsAdReady(AdType adType);

		/// <summary>
		/// Loads an ad for the given placement now, and waits for it. Joins a load already
		/// running for the placement's ad unit, and cuts short a wait before a retry.
		/// </summary>
		/// <param name="placement">The ad placement to preload.</param>
		/// <returns>Result of the load operation.</returns>
		UniTask<AdLoadResult> LoadAdAsync(AdPlacementDefinition placement, CancellationToken cancellationToken = default);

		/// <summary>
		/// Loads an ad for the given placement ID now, and waits for it.
		/// </summary>
		/// <param name="placementId">The placement ID to preload.</param>
		/// <returns>Result of the load operation.</returns>
		UniTask<AdLoadResult> LoadAdAsync(string placementId, CancellationToken cancellationToken = default);

		/// <summary>
		/// Starts loading every available placement of the given type that has no ad, and waits
		/// for the loads running. A placement waiting to retry keeps its wait.
		/// </summary>
		/// <param name="adType">The ad type to preload.</param>
		UniTask PreloadAdsAsync(AdType adType, CancellationToken cancellationToken = default);

		/// <summary>
		/// Shows an ad for the given placement. If its ad isn't loaded yet, waits a bounded time
		/// for it to load. One ad shows at a time; a second request fails with
		/// <see cref="AdErrorType.AlreadyShowing"/>.
		/// For rewarded ads, await the result and check RewardGranted.
		/// </summary>
		/// <param name="placement">The ad placement to show.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowAdAsync(AdPlacementDefinition placement, CancellationToken cancellationToken = default);

		/// <summary>
		/// Shows an ad for the given placement ID.
		/// </summary>
		/// <param name="placementId">The placement ID to show.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowAdAsync(string placementId, CancellationToken cancellationToken = default);

		/// <summary>
		/// Shows a rewarded ad for the given placement.
		/// Returns true if the user completed the ad and should be rewarded.
		/// </summary>
		/// <param name="placement">The ad placement to show.</param>
		/// <returns>True if reward should be granted.</returns>
		UniTask<bool> ShowRewardedAdAsync(AdPlacementDefinition placement, CancellationToken cancellationToken = default);

		/// <summary>
		/// Shows a rewarded ad for the given placement ID.
		/// </summary>
		/// <param name="placementId">The placement ID to show.</param>
		/// <returns>True if reward should be granted.</returns>
		UniTask<bool> ShowRewardedAdAsync(string placementId, CancellationToken cancellationToken = default);

		/// <summary>
		/// Shows an interstitial ad for the given placement.
		/// </summary>
		/// <param name="placement">The ad placement to show.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowInterstitialAsync(AdPlacementDefinition placement, CancellationToken cancellationToken = default);

		/// <summary>
		/// Shows an interstitial ad for the given placement ID.
		/// </summary>
		/// <param name="placementId">The placement ID to show.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowInterstitialAsync(string placementId, CancellationToken cancellationToken = default);

		/// <summary>
		/// Shows a banner ad for the given placement.
		/// Banner will remain visible until HideBanner or DestroyBanner is called.
		/// </summary>
		/// <param name="placement">The ad placement to show.</param>
		/// <param name="position">Banner position on screen.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowBannerAsync(AdPlacementDefinition placement, BannerPosition position = BannerPosition.Bottom, CancellationToken cancellationToken = default);

		/// <summary>
		/// Shows a banner ad for the given placement ID.
		/// </summary>
		/// <param name="placementId">The placement ID to show.</param>
		/// <param name="position">Banner position on screen.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowBannerAsync(string placementId, BannerPosition position = BannerPosition.Bottom, CancellationToken cancellationToken = default);

		/// <summary>
		/// Hides the currently visible banner.
		/// Call ShowBanner to make it visible again.
		/// </summary>
		void HideBanner();

		/// <summary>
		/// Destroys the banner and releases resources.
		/// </summary>
		void DestroyBanner();

		/// <summary>
		/// Shows an app open ad if one is loaded now and its placement's caps allow it; it never
		/// waits for a load. Call this when the app is opened from background. Does nothing when
		/// the app is coming back from one of the service's own fullscreen ads.
		/// </summary>
		/// <returns>True if an app open ad was shown.</returns>
		UniTask<bool> TryShowAppOpenAdAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Whether the rules let the placement show now: its switches, its level range, and its
		/// frequency caps and cooldowns, its own and its ad type's, with remote config applied.
		/// Whether its ad is loaded is <see cref="IsAdReady(AdPlacementDefinition)"/>'s question.
		/// False before initialization.
		/// </summary>
		/// <param name="placement">The placement to check.</param>
		/// <returns>True if the placement can be shown.</returns>
		bool CanShowPlacement(AdPlacementDefinition placement);

		/// <summary>
		/// Gets the number of times a placement's ad reached the screen this session.
		/// </summary>
		/// <param name="placementId">The placement ID to check.</param>
		/// <returns>Number of times shown this session.</returns>
		int GetSessionShowCount(string placementId);

		/// <summary>
		/// Gets the number of times a placement's ad reached the screen on this device today,
		/// a UTC day, across launches.
		/// </summary>
		/// <param name="placementId">The placement ID to check.</param>
		/// <returns>Number of times shown today.</returns>
		int GetDailyShowCount(string placementId);

		/// <summary>
		/// Sets the user consent for personalized ads (GDPR/CCPA compliance).
		/// Must be called before initialization for compliance.
		/// </summary>
		/// <param name="canTrack">Whether the user has consented to tracking.</param>
		void SetUserConsent(bool canTrack);

		/// <summary>
		/// Sets whether the user is under age (COPPA compliance).
		/// Must be called before initialization for compliance.
		/// </summary>
		/// <param name="isUnderAge">Whether the user is under age.</param>
		void SetUserUnderAge(bool isUnderAge);

		/// <summary>
		/// Call when the application pauses and resumes. On resume, ad units that ran out of
		/// retries in the meantime start loading again, per their loading strategy.
		/// </summary>
		/// <param name="isPaused">True if the application is paused.</param>
		void OnApplicationPause(bool isPaused);

		/// <summary>
		/// Updates the player level (if provided) and starts loading the placements that
		/// preload and are available at it. Returns at once, without waiting for loads.
		/// Call this on level completion or when the player levels up.
		/// </summary>
		/// <param name="newLevel">Optional new player level. If -1, uses current level.</param>
		/// <returns>True if one of those placements has an ad loaded already.</returns>
		bool RefreshPlacementsForLevel(int newLevel = -1);

		/// <summary>
		/// Gets the current player level stored in the service.
		/// </summary>
		int CurrentPlayerLevel { get; }

		/// <summary>
		/// Stops all background loading: pending retries and reloads are dropped and every ad
		/// unit goes idle. Loads already running finish. The service stays usable; a show or a
		/// refresh starts loading again.
		/// </summary>
		void CancelAllTasks();
	}
}
