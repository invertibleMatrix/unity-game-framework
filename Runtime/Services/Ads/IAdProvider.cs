using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using AK.CoreDomain.Ads;

namespace AK.Services
{
	/// <summary>
	/// Interface for ad network providers (AdMob, Unity Ads, IronSource, etc.).
	/// Each provider handles the low-level SDK integration for a specific ad network.
	/// The AdService coordinates between multiple providers.
	/// </summary>
	public interface IAdProvider
	{
		/// <summary>
		/// Name of this ad provider (e.g., "AdMob", "Unity Ads").
		/// </summary>
		string ProviderName { get; }

		/// <summary>
		/// Priority of this provider. Higher priority providers are tried first.
		/// </summary>
		int Priority { get; }

		/// <summary>
		/// Whether this provider has been initialized.
		/// </summary>
		bool IsInitialized { get; }

		/// <summary>
		/// Supported ad types by this provider.
		/// </summary>
		IReadOnlyList<AdType> SupportedAdTypes { get; }

		/// <summary>
		/// Initializes the ad provider with the given placements.
		/// </summary>
		/// <param name="placements">All ad placements that may be used.</param>
		/// <returns>True if initialization succeeded.</returns>
		UniTask<bool> InitializeAsync(IEnumerable<AdPlacementRegistration> placements);

		/// <summary>
		/// Checks if an ad is ready to be shown for the given placement.
		/// </summary>
		/// <param name="placementId">The placement ID to check.</param>
		/// <param name="adType">The type of ad to check.</param>
		/// <returns>True if an ad is loaded and ready.</returns>
		bool IsAdReady(string placementId, AdType adType);

		/// <summary>
		/// Preloads an ad for the given placement.
		/// Call this to have ads ready before showing.
		/// </summary>
		/// <param name="placementId">The placement ID to load.</param>
		/// <param name="adType">The type of ad to load.</param>
		/// <param name="adUnitId">The ad unit ID from the ad network.</param>
		/// <returns>Result of the load operation.</returns>
		UniTask<AdLoadResult> LoadAdAsync(string placementId, AdType adType, string adUnitId);

		/// <summary>
		/// Shows an ad for the given placement.
		/// </summary>
		/// <param name="placementId">The placement ID to show.</param>
		/// <param name="adType">The type of ad to show.</param>
		/// <param name="adUnitId">The ad unit ID from the ad network.</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowAdAsync(string placementId, AdType adType, string adUnitId);

		/// <summary>
		/// Shows a banner ad for the given placement.
		/// </summary>
		/// <param name="placementId">The placement ID to show.</param>
		/// <param name="adUnitId">The ad unit ID from the ad network.</param>
		/// <param name="position">Banner position (top, bottom, etc.).</param>
		/// <returns>Result of the show operation.</returns>
		UniTask<AdResult> ShowBannerAsync(string placementId, string adUnitId, BannerPosition position);

		/// <summary>
		/// Hides the currently visible banner.
		/// </summary>
		void HideBanner();

		/// <summary>
		/// Destroys the banner and releases resources.
		/// </summary>
		void DestroyBanner();

		/// <summary>
		/// Sets the user consent for personalized ads (GDPR/CCPA compliance).
		/// </summary>
		/// <param name="canTrack">Whether the user has consented to tracking.</param>
		void SetUserConsent(bool canTrack);

		/// <summary>
		/// Sets whether the user is under age (COPPA compliance).
		/// </summary>
		/// <param name="isUnderAge">Whether the user is under age.</param>
		void SetUserUnderAge(bool isUnderAge);

		/// <summary>
		/// Called when the application gains focus.
		/// </summary>
		void OnApplicationPause(bool isPaused);
	}

	/// <summary>
	/// Registration data for a single ad placement to be used by providers.
	/// </summary>
	public struct AdPlacementRegistration
	{
		/// <summary>
		/// The unique placement ID (internal identifier).
		/// </summary>
		public string PlacementId;

		/// <summary>
		/// The type of ad.
		/// </summary>
		public AdType AdType;

		/// <summary>
		/// The ad unit ID from the ad network.
		/// </summary>
		public string AdUnitId;

		/// <summary>
		/// Additional configuration options (optional).
		/// </summary>
		public Dictionary<string, object> Options;

		public AdPlacementRegistration(string placementId, AdType adType, string adUnitId)
		{
			PlacementId = placementId;
			AdType = adType;
			AdUnitId = adUnitId;
			Options = null;
		}

		public AdPlacementRegistration(string placementId, AdType adType, string adUnitId, Dictionary<string, object> options)
		{
			PlacementId = placementId;
			AdType = adType;
			AdUnitId = adUnitId;
			Options = options;
		}
	}

	/// <summary>
	/// Banner position options.
	/// </summary>
	public enum BannerPosition
	{
		Top,
		Bottom,
		TopLeft,
		TopRight,
		BottomLeft,
		BottomRight,
		Center
	}
}