using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.CoreDomain.Ads;
using AK.CoreDomain.RemoteConfig;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain
{
	/// <summary>
	/// Container for all ad placement definitions with powerful query methods.
	/// Supports remote config integration for dynamic ad behavior control.
	/// </summary>
	[CreateAssetMenu(fileName = "AdsMeta", menuName = "Gameplay/MetaData/Ads/AdsMeta")]
	public class AdsMeta : MetaDataAsset, IMeta
	{
		[Serializable]
		public struct AdIds
		{
			public AdPlacementDefinition Rewarded;
			public AdPlacementDefinition Interstitial;
			public AdPlacementDefinition Banner;
		}
		
		[SerializeField] private AdsRegistry _registry;

		[Header("Ad Placements")]
		[Tooltip("All ad placement definitions.")]
		public List<AdPlacementDefinition> Placements;

		[Header("Categories")]
		[Tooltip("Ad placement categories for UI organization.")]
		public List<AdCategory> Categories;

		[Header("Global Settings")]
		[Tooltip("Default ad network to use.")]
		public string DefaultNetwork = "AdMob";

		[Tooltip("Enable test mode for ads.")]
		public bool TestMode;

		[Header("Global Frequency Limits")]
		[Tooltip("Maximum interstitial ads per session (applies if placement doesn't have its own).")]
		[Range(0, 100)]
		public int DefaultMaxInterstitialPerSession = 10;

		[Tooltip("Maximum rewarded ads per session.")]
		[Range(0, 100)]
		public int DefaultMaxRewardedPerSession = 20;

		[Tooltip("Global cooldown between interstitials in seconds.")]
		[Range(0, 300)]
		public int DefaultInterstitialCooldown = 60;

		[Header("Remote Config Global Overrides")]
		[Tooltip("Remote bool to globally enable/disable all ads.")]
		public RemoteBool AdsEnabledGlobal;

		[Tooltip("Remote bool to enable/disable interstitial ads.")]
		public RemoteBool InterstitialsEnabled;

		[Tooltip("Remote bool to enable/disable rewarded ads.")]
		public RemoteBool RewardedAdsEnabled;

		[Tooltip("Remote int for minimum level to show interstitials.")]
		public RemoteInt InterstitialMinLevel;

		[Tooltip("Remote int for minimum level to show rewarded ads.")]
		public RemoteInt RewardedMinLevel;

		[Tooltip("Remote int to override global interstitial cooldown.")]
		public RemoteInt InterstitialCooldownOverride;

		[Tooltip("Remote float for ad fill rate (for testing/simulation).")]
		public RemoteFloat AdFillRate;

		public AdIds Ids;
		
		[Serializable]
		public class AdCategory
		{
			public UID CategoryID;
			public string DisplayName;
			public string Description;
			public Sprite Icon;
			public int DisplayPriority;
			public List<UID> PlacementIDs;
		}

		#region Properties

		/// <summary>
		/// Checks if ads are globally enabled via remote config.
		/// </summary>
		public bool AreAdsEnabled
		{
			get
			{
				if (AdsEnabledGlobal != null && AdsEnabledGlobal.HasRemoteValue)
					return AdsEnabledGlobal.Value;
				return true; // Default to enabled
			}
		}

		/// <summary>
		/// Checks if interstitial ads are enabled.
		/// </summary>
		public bool AreInterstitialsEnabled
		{
			get
			{
				if (!AreAdsEnabled)
					return false;
				if (InterstitialsEnabled != null && InterstitialsEnabled.HasRemoteValue)
					return InterstitialsEnabled.Value;
				return true;
			}
		}

		/// <summary>
		/// Checks if rewarded ads are enabled.
		/// </summary>
		public bool AreRewardedAdsEnabled
		{
			get
			{
				if (!AreAdsEnabled)
					return false;
				if (RewardedAdsEnabled != null && RewardedAdsEnabled.HasRemoteValue)
					return RewardedAdsEnabled.Value;
				return true;
			}
		}

		/// <summary>
		/// Gets the minimum level for interstitials (remote override or default).
		/// </summary>
		public int GetInterstitialMinLevel()
		{
			if (InterstitialMinLevel != null && InterstitialMinLevel.HasRemoteValue)
				return InterstitialMinLevel.Value;
			return 0;
		}

		/// <summary>
		/// Gets the minimum level for rewarded ads (remote override or default).
		/// </summary>
		public int GetRewardedMinLevel()
		{
			if (RewardedMinLevel != null && RewardedMinLevel.HasRemoteValue)
				return RewardedMinLevel.Value;
			return 0;
		}

		/// <summary>
		/// Gets the interstitial cooldown (remote override or default).
		/// </summary>
		public int GetInterstitialCooldown()
		{
			if (InterstitialCooldownOverride != null && InterstitialCooldownOverride.HasRemoteValue)
				return InterstitialCooldownOverride.Value;
			return DefaultInterstitialCooldown;
		}

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

		/// <summary>
		/// Gets a placement by its UID.
		/// </summary>
		public AdPlacementDefinition GetPlacementByID(UID uid)
		{
			if (uid == null || uid.IsEmpty())
				return null;

			return _registry?.GetObjectByUID(uid);
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
		public List<AdPlacementDefinition> GetPlacementsByCategory(UID categoryID)
		{
			if (categoryID == null || categoryID.IsEmpty())
				return new List<AdPlacementDefinition>();

			var category = Categories?.FirstOrDefault(c => c.CategoryID == categoryID);
			if (category?.PlacementIDs == null)
				return new List<AdPlacementDefinition>();

			return Placements?.Where(p => category.PlacementIDs.Contains(p.UniqueID)).ToList() 
				?? new List<AdPlacementDefinition>();
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
		/// Gets placements that can be shown based on frequency limits.
		/// </summary>
		public List<AdPlacementDefinition> GetPlacementsWithinFrequencyLimits(
			Dictionary<string, int> sessionCounts,
			Dictionary<string, int> dailyCounts,
			Dictionary<string, DateTime> lastShownTimes,
			int currentLevel = 1)
		{
			return Placements?.Where(p =>
			{
				// Check availability
				if (!p.IsAvailable(currentLevel)) return false;

				// Check session limit
				int maxSession = p.GetMaxPerSession();
				if (maxSession > 0)
				{
					int sessionCount = 0;
					sessionCounts?.TryGetValue(p.PlacementID, out sessionCount);
					if (sessionCount >= maxSession) return false;
				}

				// Check daily limit
				int maxDay = p.GetMaxPerDay();
				if (maxDay > 0)
				{
					int dailyCount = 0;
					dailyCounts?.TryGetValue(p.PlacementID, out dailyCount);
					if (dailyCount >= maxDay) return false;
				}

				// Check cooldown
				int cooldown = p.GetCooldownSeconds();
				if (cooldown > 0 && lastShownTimes != null)
				{
					if (lastShownTimes.TryGetValue(p.PlacementID, out DateTime lastShown))
					{
						TimeSpan timeSinceLast = DateTime.UtcNow - lastShown;
						if (timeSinceLast.TotalSeconds < cooldown) return false;
					}
				}

				return true;
			}).ToList() ?? new List<AdPlacementDefinition>();
		}

		/// <summary>
		/// Gets the next available interstitial placement based on priority and frequency.
		/// </summary>
		public AdPlacementDefinition GetNextInterstitial(
			Dictionary<string, int> sessionCounts,
			Dictionary<string, int> dailyCounts,
			Dictionary<string, DateTime> lastShownTimes,
			int currentLevel = 1)
		{
			var available = GetAvailableInterstitialPlacements(currentLevel);
			if (available.Count == 0)
				return null;

			var withinLimits = available.Where(p =>
			{
				// Check session limit
				int maxSession = p.GetMaxPerSession();
				if (maxSession > 0)
				{
					int sessionCount = 0;
					sessionCounts?.TryGetValue(p.PlacementID, out sessionCount);
					if (sessionCount >= maxSession) return false;
				}

				// Check daily limit
				int maxDay = p.GetMaxPerDay();
				if (maxDay > 0)
				{
					int dailyCount = 0;
					dailyCounts?.TryGetValue(p.PlacementID, out dailyCount);
					if (dailyCount >= maxDay) return false;
				}

				// Check cooldown
				int cooldown = p.GetCooldownSeconds();
				if (cooldown > 0 && lastShownTimes != null)
				{
					if (lastShownTimes.TryGetValue(p.PlacementID, out DateTime lastShown))
					{
						TimeSpan timeSinceLast = DateTime.UtcNow - lastShown;
						if (timeSinceLast.TotalSeconds < cooldown) return false;
					}
				}

				return true;
			}).OrderByDescending(p => p.Priority);

			return withinLimits.FirstOrDefault();
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
		[Button("Refresh Registry"), PropertyOrder(100)]
		public void RefreshRegistry()
		{
			if (_registry != null)
			{
				_registry.RefreshAllObjects();
				UnityEditor.EditorUtility.SetDirty(this);
			}
		}

		[Button("Validate Placements"), PropertyOrder(101)]
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

		[Button("List All Placements"), PropertyOrder(102)]
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