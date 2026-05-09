using System;
using Cysharp.Threading.Tasks;
using GameplayCore.MetaData;
using GameplayCore.MetaData.Ads;

namespace AK.Services
{
	/// <summary>
	/// Main interface for the Ads Service.
	/// Provides a unified API for showing ads across multiple ad networks.
	/// Supports multiple providers with priority-based waterfall mediation.
	/// </summary>
	public interface IAdsService
	{
		/// <summary>
		/// Whether the ads service has been successfully initialized.
		/// </summary>
		bool IsInitialized { get; }

		/// <summary>
		/// Whether ads are currently disabled (e.g., user purchased "Remove Ads").
		/// </summary>
		bool AdsDisabled { get; set; }

		/// <summary>
		/// Event raised when ads are disabled (e.g., after IAP purchase).
		/// </summary>
		event Action OnAdsDisabled;

		/// <summary>
		/// Event raised when an ad is shown successfully.
		/// </summary>
		event Action<AdPlacementDefinition> OnAdShown;

		/// <summary>
		/// Event raised when an ad fails to show.
		/// </summary>
		event Action<AdPlacementDefinition, AdErrorType, string> OnAdFailed;

		/// <summary>
		/// Event raised when a rewarded ad completes and reward should be granted.
		/// </summary>
		event Action<AdPlacementDefinition> OnAdRewardGranted;

		/// <summary>
		/// Initializes the ads service with ad placements from the meta data.
		/// Must be called before any other operations.
		/// </summary>
		/// <param name="playerLevel"></param>
		/// <returns>True if initialization succeeded.</returns>
		UniTask<bool> InitializeAsync(AdsMeta adsMeta, int playerLevel);

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
		/// Checks if an ad is ready for the given ad type.
		/// Uses the first available placement of that type.
		/// </summary>
		/// <param name="adType">The ad type to check.</param>
		/// <returns>True if an ad is loaded and ready.</returns>
		bool IsAdReady(AdType adType);

		/// <summary>
		/// Preloads an ad for the given placement.
		/// Call this to have ads ready before showing.
		/// </summary>
		/// <param name="placement">The ad placement to preload.</param>
		/// <returns>Result of the load operation.</returns>
		UniTask<AdLoadResult> LoadAdAsync(AdPlacementDefinition placement);

		/// <summary>
		/// Preloads an ad for the given placement ID.
		/// </summary>
		/// <param name="placementId">The placement ID to preload.</param>
		/// <returns>Result of the load operation.</returns>
		UniTask<AdLoadResult> LoadAdAsync(string placementId);

		/// <summary>
		/// Preloads all ads of the given type.
		/// </summary>
		/// <param name="adType">The ad type to preload.</param>
		UniTask PreloadAdsAsync(AdType adType);

		/// <summary>
		/// Shows an ad for the given placement.
		/// For rewarded ads, await the result and check RewardGranted.
		/// </summary>
		/// <param name="placement">The ad placement to show.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowAdAsync(AdPlacementDefinition placement);

		/// <summary>
		/// Shows an ad for the given placement ID.
		/// </summary>
		/// <param name="placementId">The placement ID to show.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowAdAsync(string placementId);

		/// <summary>
		/// Shows a rewarded ad for the given placement.
		/// Returns true if the user completed the ad and should be rewarded.
		/// </summary>
		/// <param name="placement">The ad placement to show.</param>
		/// <returns>True if reward should be granted.</returns>
		UniTask<bool> ShowRewardedAdAsync(AdPlacementDefinition placement);

		/// <summary>
		/// Shows a rewarded ad for the given placement ID.
		/// </summary>
		/// <param name="placementId">The placement ID to show.</param>
		/// <returns>True if reward should be granted.</returns>
		UniTask<bool> ShowRewardedAdAsync(string placementId);

		/// <summary>
		/// Shows an interstitial ad for the given placement.
		/// </summary>
		/// <param name="placement">The ad placement to show.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowInterstitialAsync(AdPlacementDefinition placement);

		/// <summary>
		/// Shows an interstitial ad for the given placement ID.
		/// </summary>
		/// <param name="placementId">The placement ID to show.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowInterstitialAsync(string placementId);

		/// <summary>
		/// Shows a banner ad for the given placement.
		/// Banner will remain visible until HideBanner or DestroyBanner is called.
		/// </summary>
		/// <param name="placement">The ad placement to show.</param>
		/// <param name="position">Banner position on screen.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowBannerAsync(AdPlacementDefinition placement, BannerPosition position = BannerPosition.Bottom);

		/// <summary>
		/// Shows a banner ad for the given placement ID.
		/// </summary>
		/// <param name="placementId">The placement ID to show.</param>
		/// <param name="position">Banner position on screen.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowBannerAsync(string placementId, BannerPosition position = BannerPosition.Bottom);

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
		/// Shows an app open ad if available and conditions are met.
		/// Call this when the app is opened from background.
		/// </summary>
		/// <returns>True if an app open ad was shown.</returns>
		UniTask<bool> TryShowAppOpenAdAsync();

		/// <summary>
		/// Checks if a placement can be shown based on frequency caps and cooldowns.
		/// </summary>
		/// <param name="placement">The placement to check.</param>
		/// <returns>True if the placement can be shown.</returns>
		bool CanShowPlacement(AdPlacementDefinition placement);

		/// <summary>
		/// Gets the number of times a placement has been shown this session.
		/// </summary>
		/// <param name="placementId">The placement ID to check.</param>
		/// <returns>Number of times shown this session.</returns>
		int GetSessionShowCount(string placementId);

		/// <summary>
		/// Gets the number of times a placement has been shown today.
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
		/// Called when the application gains/loses focus.
		/// Required for app open ads and proper SDK handling.
		/// </summary>
		/// <param name="isPaused">True if the application is paused.</param>
		void OnApplicationPause(bool isPaused);

		/// <summary>
		/// Updates player level (if provided) and ensures all eligible placements are loaded and ready.
		/// Call this on level completion or when player levels up to proactively load ads.
		/// </summary>
		/// <param name="newLevel">Optional new player level. If -1, uses current level.</param>
		/// <returns>True if any placements are ready (already loaded or newly loaded).</returns>
		UniTask<bool> RefreshPlacementsForLevelAsync(int newLevel = -1);

		/// <summary>
		/// Gets the current player level stored in the service.
		/// </summary>
		int CurrentPlayerLevel { get; }

		/// <summary>
		/// Cancels all pending background tasks (auto-reloads, retry attempts).
		/// Call this when the service is being destroyed or the game is closing.
		/// </summary>
		void CancelAllTasks();
	}
}