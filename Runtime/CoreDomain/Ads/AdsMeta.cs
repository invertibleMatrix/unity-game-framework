using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.CoreDomain.Ads;
using AK.CoreDomain.RemoteConfig;
using UnityEngine;

namespace AK.CoreDomain
{
	/// <summary>
	/// All ad placement definitions, and the rules shared by an ad type: its switches, the
	/// level it starts at, and its limits across all of its placements.
	///
	/// <para><b>Remote overrides.</b> Each remote variable here overrides its local setting
	/// once it has a remote value (<see cref="RemoteOverride"/>); until then the local setting
	/// holds. The switches are on locally. The variables, here and on the placements, are read as
	/// held, so they must be the instances remote config writes into: the registry's. A copy in
	/// another bundle never gets a value.</para>
	///
	/// <para><b>Ad types.</b> The rewarded settings cover rewarded and rewarded interstitial
	/// ads. Banners and app open ads follow only the switch for all ads.</para>
	/// </summary>
	[CreateAssetMenu(fileName = "AdsMeta", menuName = "AK/MetaData/Ads/AdsMeta")]
	public class AdsMeta : MetaDataAsset, IMetaWithRegistry
	{
		[SerializeField] private AdsRegistry _registry;

		public UidRegistryAssetBase RegistryAsset => _registry;

		[Header("Ad Placements")]
		[Tooltip("All ad placement definitions.")]
		public List<AdPlacementDefinition> Placements;

		[Header("Categories")]
		[Tooltip("Ad placement categories for UI organization.")]
		public List<AdCategory> Categories;
		
		[Tooltip("Enable test mode for ads.")]
		public bool TestMode;

		[Header("Limits Across Placements")]
		[Tooltip("Most interstitial impressions per session, across all interstitial placements (0 = no limit). Each placement's own limits apply as well.")]
		[Range(0, 100)]
		public int DefaultMaxInterstitialPerSession = 10;

		[Tooltip("Most rewarded and rewarded interstitial impressions per session, across all their placements (0 = no limit). Each placement's own limits apply as well.")]
		[Range(0, 100)]
		public int DefaultMaxRewardedPerSession = 20;

		[Tooltip("Least time between two interstitials, from any placements, in seconds (0 = none). Counted across launches. InterstitialCooldownOverride wins once it has a remote value.")]
		[Range(0, 300)]
		public int DefaultInterstitialCooldown = 60;

		[Header("Remote Config Overrides")]
		[Tooltip("Switches all ads off, or back on. Ads are on until it has a remote value.")]
		public RemoteBool AdsEnabledGlobal;

		[Tooltip("Switches interstitial ads off, or back on. They are on until it has a remote value.")]
		public RemoteBool InterstitialsEnabled;

		[Tooltip("Switches rewarded and rewarded interstitial ads off, or back on. They are on until it has a remote value.")]
		public RemoteBool RewardedAdsEnabled;

		[Tooltip("The lowest player level that sees interstitials. No level limit until it has a remote value.")]
		public RemoteInt InterstitialMinLevel;

		[Tooltip("The lowest player level that sees rewarded and rewarded interstitial ads. No level limit until it has a remote value.")]
		public RemoteInt RewardedMinLevel;

		[Tooltip("Overrides DefaultInterstitialCooldown once it has a remote value.")]
		public RemoteInt InterstitialCooldownOverride;

		[Tooltip("Remote float for ad fill rate (for testing/simulation).")]
		public RemoteFloat AdFillRate;
		
		[Serializable]
		public class AdCategory
		{
			public Uid    CategoryID;
			public string DisplayName;
			public string Description;
			public Sprite Icon;
			public int    DisplayPriority;
			public List<AdPlacementDefinition> Placements;
		}

		#region Properties

		/// <summary>Whether ads are switched on at all. On until <see cref="AdsEnabledGlobal"/> has a remote value.</summary>
		public bool AreAdsEnabled => RemoteOverride.Resolve(AdsEnabledGlobal, true);

		/// <summary>Whether interstitials are switched on: ads are, and <see cref="InterstitialsEnabled"/> doesn't say otherwise.</summary>
		public bool AreInterstitialsEnabled => AreAdsEnabled && RemoteOverride.Resolve(InterstitialsEnabled, true);

		/// <summary>Whether rewarded and rewarded interstitial ads are switched on: ads are, and <see cref="RewardedAdsEnabled"/> doesn't say otherwise.</summary>
		public bool AreRewardedAdsEnabled => AreAdsEnabled && RemoteOverride.Resolve(RewardedAdsEnabled, true);

		public void InitializeMeta() { }

		/// <summary>The lowest level that sees interstitials: the remote value once known, otherwise 0.</summary>
		public int GetInterstitialMinLevel() => RemoteOverride.Resolve(InterstitialMinLevel, 0);

		/// <summary>The lowest level that sees rewarded ads: the remote value once known, otherwise 0.</summary>
		public int GetRewardedMinLevel() => RemoteOverride.Resolve(RewardedMinLevel, 0);

		/// <summary>The least seconds between two interstitials: the remote value once known, otherwise <see cref="DefaultInterstitialCooldown"/>.</summary>
		public int GetInterstitialCooldown() => RemoteOverride.Resolve(InterstitialCooldownOverride, DefaultInterstitialCooldown);

		/// <summary>Whether ads of <paramref name="adType"/> are switched on.</summary>
		public bool IsTypeEnabled(AdType adType) => adType switch
		{
			AdType.Interstitial                            => AreInterstitialsEnabled,
			AdType.Rewarded or AdType.RewardedInterstitial => AreRewardedAdsEnabled,
			_                                              => AreAdsEnabled,
		};

		/// <summary>The lowest player level that sees ads of <paramref name="adType"/>.</summary>
		public int GetMinLevel(AdType adType) => adType switch
		{
			AdType.Interstitial                            => GetInterstitialMinLevel(),
			AdType.Rewarded or AdType.RewardedInterstitial => GetRewardedMinLevel(),
			_                                              => 0,
		};

		/// <summary>The most impressions of <paramref name="adType"/> per session, across its placements. 0 is no limit.</summary>
		public int GetMaxPerSession(AdType adType) => adType switch
		{
			AdType.Interstitial                            => DefaultMaxInterstitialPerSession,
			AdType.Rewarded or AdType.RewardedInterstitial => DefaultMaxRewardedPerSession,
			_                                              => 0,
		};

		/// <summary>The least seconds between two impressions of <paramref name="adType"/>, from any placements. 0 is none.</summary>
		public int GetCooldownSeconds(AdType adType) => adType == AdType.Interstitial ? GetInterstitialCooldown() : 0;

		#endregion

		#region Query Methods

		/// <summary>
		/// Gets a placement by its PlacementID.
		/// </summary>
		public AdPlacementDefinition GetPlacementByID(string placementID)
		{
			if (string.IsNullOrEmpty(placementID))
				return null;

			return Placements?.FirstOrDefault(p => p.PlacementID == placementID);
		}

		/// <summary>Resolves a placement by identity through the registry. Null when unknown.</summary>
		public AdPlacementDefinition GetPlacement(Uid<AdPlacementDefinition> id)
		{
			return _registry != null && _registry.TryResolve(id, out AdPlacementDefinition placement) ? placement : null;
		}

		/// <summary>Resolves the registry's live instance for a held placement reference.</summary>
		public AdPlacementDefinition GetPlacement(AdPlacementDefinition asset)
		{
			return asset != null ? GetPlacement(asset.IdAs<AdPlacementDefinition>()) : null;
		}

		/// <summary>
		/// Gets all placements of a specific type.
		/// </summary>
		public List<AdPlacementDefinition> GetPlacementsByType(AdType adType)
		{
			return Placements?.Where(p => p.AdType == adType).ToList() ?? new List<AdPlacementDefinition>();
		}

		/// <summary>
		/// Gets all rewarded ad placements.
		/// </summary>
		public List<AdPlacementDefinition> GetRewardedPlacements()
		{
			return Placements?.Where(p => p.AdType == AdType.Rewarded || p.AdType == AdType.RewardedInterstitial).ToList() 
				?? new List<AdPlacementDefinition>();
		}

		/// <summary>
		/// Gets all interstitial ad placements.
		/// </summary>
		public List<AdPlacementDefinition> GetInterstitialPlacements()
		{
			return Placements?.Where(p => p.AdType == AdType.Interstitial).ToList() 
				?? new List<AdPlacementDefinition>();
		}

		/// <summary>
		/// Gets all banner ad placements.
		/// </summary>
		public List<AdPlacementDefinition> GetBannerPlacements()
		{
			return Placements?.Where(p => p.AdType == AdType.Banner).ToList() 
				?? new List<AdPlacementDefinition>();
		}

		/// <summary>
		/// Gets all app open ad placements.
		/// </summary>
		public List<AdPlacementDefinition> GetAppOpenPlacements()
		{
			return Placements?.Where(p => p.AdType == AdType.AppOpen).ToList() 
				?? new List<AdPlacementDefinition>();
		}

		/// <summary>
		/// Gets all currently available placements.
		/// </summary>
		public List<AdPlacementDefinition> GetAvailablePlacements(int currentLevel = 1)
		{
			return Placements?.Where(p => p.IsAvailable(currentLevel)).ToList() 
				?? new List<AdPlacementDefinition>();
		}

		/// <summary>
		/// Gets all available rewarded placements.
		/// </summary>
		public List<AdPlacementDefinition> GetAvailableRewardedPlacements(int currentLevel = 1)
		{
			if (!AreRewardedAdsEnabled)
				return new List<AdPlacementDefinition>();

			var minLevel = GetRewardedMinLevel();
			if (currentLevel < minLevel)
				return new List<AdPlacementDefinition>();

			return Placements?.Where(p =>
				(p.AdType == AdType.Rewarded || p.AdType == AdType.RewardedInterstitial) &&
				p.IsAvailable(currentLevel)).ToList() ?? new List<AdPlacementDefinition>();
		}

		/// <summary>
		/// Gets all available interstitial placements.
		/// </summary>
		public List<AdPlacementDefinition> GetAvailableInterstitialPlacements(int currentLevel = 1)
		{
			if (!AreInterstitialsEnabled)
				return new List<AdPlacementDefinition>();

			var minLevel = GetInterstitialMinLevel();
			if (currentLevel < minLevel)
				return new List<AdPlacementDefinition>();

			return Placements?.Where(p =>
				p.AdType == AdType.Interstitial &&
				p.IsAvailable(currentLevel)).ToList() ?? new List<AdPlacementDefinition>();
		}

		/// <summary>
		/// Gets the highest priority placement of a specific type.
		/// </summary>
		public AdPlacementDefinition GetHighestPriorityPlacement(AdType adType, int currentLevel = 1)
		{
			return Placements?
				.Where(p => p.AdType == adType && p.IsAvailable(currentLevel))
				.OrderByDescending(p => p.Priority)
				.FirstOrDefault();
		}

		/// <summary>
		/// Gets all placements in a specific category.
		/// </summary>
		public List<AdPlacementDefinition> GetPlacementsByCategory(Uid categoryID)
		{
			if (categoryID.IsNone)
				return new List<AdPlacementDefinition>();

			var category = Categories?.FirstOrDefault(c => c.CategoryID == categoryID);
			if (category?.Placements == null)
				return new List<AdPlacementDefinition>();

			return category.Placements.Where(p => p != null).ToList();
		}

		/// <summary>
		/// Gets all categories sorted by display priority.
		/// </summary>
		public List<AdCategory> GetCategoriesSorted()
		{
			return Categories?.OrderBy(c => c.DisplayPriority).ToList() 
				?? new List<AdCategory>();
		}

		/// <summary>
		/// Checks if a placement exists by PlacementID.
		/// </summary>
		public bool HasPlacement(string placementID)
		{
			return Placements?.Any(p => p.PlacementID == placementID) ?? false;
		}

		/// <summary>
		/// Gets all placements that have rewards.
		/// </summary>
		public List<AdPlacementDefinition> GetPlacementsWithRewards(int currentLevel = 1)
		{
			return Placements?.Where(p => p.HasRewards() && p.IsAvailable(currentLevel)).ToList() 
				?? new List<AdPlacementDefinition>();
		}

		/// <summary>
		/// Gets all placements with a specific tag.
		/// </summary>
		public List<AdPlacementDefinition> GetPlacementsByTag(string tag)
		{
			return Placements?.Where(p => p.HasTag(tag)).ToList() 
				?? new List<AdPlacementDefinition>();
		}

		/// <summary>
		/// Checks if interstitial ads should be shown at the given level.
		/// </summary>
		public bool ShouldShowInterstitials(int currentLevel)
		{
			if (!AreInterstitialsEnabled)
				return false;

			return currentLevel >= GetInterstitialMinLevel();
		}

		/// <summary>
		/// Checks if rewarded ads should be shown at the given level.
		/// </summary>
		public bool ShouldShowRewardedAds(int currentLevel)
		{
			if (!AreRewardedAdsEnabled)
				return false;

			return currentLevel >= GetRewardedMinLevel();
		}

		#endregion

		#region Editor Helpers

#if UNITY_EDITOR
		public void RefreshRegistry()
		{
			if (_registry != null)
			{
				_registry.Editor_RefreshFromProject();
				UnityEditor.EditorUtility.SetDirty(this);
			}
		}

		public void ValidatePlacements()
		{
			if (Placements == null || Placements.Count == 0)
			{
				Debug.LogWarning("AdsMeta: No placements defined!");
				return;
			}

			var placementIds = new HashSet<string>();
			int validCount = 0;
			int enabledCount = 0;

			foreach (var placement in Placements)
			{
				// Check for missing placement ID
				if (string.IsNullOrEmpty(placement.PlacementID))
				{
					Debug.LogWarning($"AdsMeta: Placement '{placement.name}' has no PlacementID set.");
					continue;
				}

				// Check for duplicate placement IDs
				if (placementIds.Contains(placement.PlacementID))
				{
					Debug.LogError($"AdsMeta: Duplicate PlacementID '{placement.PlacementID}' found!");
					continue;
				}

				// Check for missing ad unit ID
				if (string.IsNullOrEmpty(placement.AdUnitID))
				{
					Debug.LogWarning($"AdsMeta: Placement '{placement.PlacementID}' has no AdUnitID set.");
				}

				placementIds.Add(placement.PlacementID);
				validCount++;

				if (placement.IsEnabled)
					enabledCount++;
			}

			Debug.Log($"AdsMeta: Validation complete. {validCount} valid placements, {enabledCount} enabled.");
		}

		public void ListAllPlacements()
		{
			if (Placements == null || Placements.Count == 0)
			{
				Debug.Log("AdsMeta: No placements defined.");
				return;
			}

			Debug.Log($"AdsMeta: {Placements.Count} placements:");
			foreach (var p in Placements)
			{
				Debug.Log($"  - {p.PlacementID} ({p.AdType}) - {(p.IsEnabled ? "Enabled" : "Disabled")}");
			}
		}
#endif

		#endregion
	}
}