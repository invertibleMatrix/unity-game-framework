using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.RemoteConfig;
using AK.Services;
using UnityEngine;

namespace AK.CoreDomain.Ads
{
	/// <summary>
	/// Defines a specific placement where an ad can be shown.
	/// Each placement can have multiple reward options (e.g., watch ad for coins OR powerup).
	/// Supports remote config overrides for flexibility.
	/// </summary>
	[CreateAssetMenu(fileName = "AdPlacementDefinition", menuName = "AK/MetaData/Ads/AdPlacementDefinition")]
	public class AdPlacementDefinition : MetaDataAsset
	{
		[Header("Placement Information")] [Tooltip("Unique identifier for this ad placement.")]
		public string PlacementID;

		[Header("Ad Configuration")] [Tooltip("The type of ad for this placement.")]
		public AdType AdType;

		[Tooltip("Ad unit ID from ad network (e.g., AdMob, Unity Ads).")]
		public string AndroidAdUnitID;

		[Tooltip("Alternative ad unit ID (e.g., for A/B testing or different networks).")]
		public string AndroidAlternateAdUnitID;

		[Tooltip("Ad unit ID from ad network (e.g., AdMob, Unity Ads).")]
		public string IOSAdUnitID;

		[Tooltip("Alternative ad unit ID (e.g., for A/B testing or different networks).")]
		public string IOSAlternateAdUnitID;

		[Tooltip("Priority for this placement when multiple placements of the same type exist.")] [Range(0, 100)]
		public int Priority = 50;

		[Header("Reward Configuration")] [Tooltip("If single reward then use Plain Definition")]
		public UID Reward;

		[Tooltip("Optional reward bundle for multiple rewards.")]
		public UID RewardBundle;

		[Header("Frequency Control")] [Tooltip("Maximum times this ad can be shown per session (0 = unlimited).")] [Range(0, 100)]
		public int MaxPerSession = 0;

		[Tooltip("Maximum times this ad can be shown per day (0 = unlimited).")] [Range(0, 100)]
		public int MaxPerDay = 0;

		[Tooltip("Cooldown between showing this ad again (in seconds, 0 = no cooldown).")] [Range(0, 86400)]
		public int CooldownSeconds = 0;

		[Header("Level Requirements")] [Tooltip("Minimum player level required to see this ad (0 = no requirement).")] [Range(0, 1000)]
		public int MinPlayerLevel = 0;

		[Tooltip("Maximum player level after which this ad won't show (0 = no limit).")] [Range(0, 1000)]
		public int MaxPlayerLevel = 0;

		[Header("Remote Config Overrides")] [Tooltip("Remote bool to enable/disable this placement remotely.")]
		public RemoteBool EnabledRemote;

		[Tooltip("Remote int to override max shows per session.")]
		public RemoteInt MaxPerSessionRemote;

		[Tooltip("Remote int to override max shows per day.")]
		public RemoteInt MaxPerDayRemote;

		[Tooltip("Remote int to override cooldown seconds.")]
		public RemoteInt CooldownSecondsRemote;

		[Tooltip("Remote int to override minimum player level.")]
		public RemoteInt MinPlayerLevelRemote;

		[Header("Loading Strategy")] [Tooltip("Loading strategy for this placement. Controls auto-reload, retry behavior, etc.")]
		public AdLoadingStrategy LoadingStrategy = AdLoadingStrategy.Presets.Standard;

		[Tooltip("Strategy preset to use. Changes will override the LoadingStrategy above.")]
		public AdLoadingStrategyPreset StrategyPreset = AdLoadingStrategyPreset.Standard;

		[Header("Advanced Settings")] [Tooltip("Whether this placement is enabled.")]
		public bool IsEnabled = true;

		[Tooltip("Tags for categorization and filtering.")]
		public List<string> Tags = new();

		public UID UniqueID => this;

		public string AdUnitID
		{
			get
			{
#if UNITY_ANDROID
				return AndroidAdUnitID;
#elif UNITY_IOS
				return IOSAdUnitID;
#else
				// Editor/standalone: fall back to the Android ID so placements resolve in testing.
				return AndroidAdUnitID;
#endif
			}
		}

		public string AlternateAdUnitID
		{
			get
			{
#if UNITY_ANDROID
				return AndroidAlternateAdUnitID;
#elif UNITY_IOS
				return IOSAlternateAdUnitID;
#else
				return AndroidAlternateAdUnitID;
#endif
			}
		}

		/// <summary>
		/// Checks if this placement is currently available.
		/// Takes into account remote config overrides.
		/// </summary>
		/// <param name="currentLevel">Current player level.</param>
		/// <returns>True if the placement is available.</returns>
		public bool IsAvailable(int currentLevel = 1)
		{
			// Check if disabled
			if (!IsEnabled)
				return false;

			// Check remote enabled override
			if (EnabledRemote != null && !EnabledRemote.Value)
				return false;

			// Check level requirements
			int minLevel = GetMinPlayerLevel();
			int maxLevel = MaxPlayerLevel > 0 ? MaxPlayerLevel : int.MaxValue;

			if (currentLevel < minLevel || currentLevel > maxLevel)
				return false;

			return true;
		}

		/// <summary>
		/// Gets the effective max per session value (remote override or local).
		/// </summary>
		public int GetMaxPerSession()
		{
			if (MaxPerSessionRemote != null && MaxPerSessionRemote.HasRemoteValue)
				return MaxPerSessionRemote.Value;
			return MaxPerSession;
		}

		/// <summary>
		/// Gets the effective max per day value (remote override or local).
		/// </summary>
		public int GetMaxPerDay()
		{
			if (MaxPerDayRemote != null && MaxPerDayRemote.HasRemoteValue)
				return MaxPerDayRemote.Value;
			return MaxPerDay;
		}

		/// <summary>
		/// Gets the effective cooldown seconds (remote override or local).
		/// </summary>
		public int GetCooldownSeconds()
		{
			if (CooldownSecondsRemote != null && CooldownSecondsRemote.HasRemoteValue)
				return CooldownSecondsRemote.Value;
			return CooldownSeconds;
		}

		/// <summary>
		/// Gets the effective minimum player level (remote override or local).
		/// </summary>
		public int GetMinPlayerLevel()
		{
			if (MinPlayerLevelRemote != null && MinPlayerLevelRemote.HasRemoteValue)
				return MinPlayerLevelRemote.Value;
			return MinPlayerLevel;
		}

		/// <summary>
		/// Gets the ad unit ID to use (primary or alternate).
		/// </summary>
		/// <param name="useAlternate">Whether to use the alternate ad unit ID.</param>
		/// <returns>The ad unit ID to use.</returns>
		public string GetAdUnitID(bool useAlternate = false)
		{
#if UNITY_IOS
			return useAlternate && !string.IsNullOrEmpty(IOSAlternateAdUnitID)
				? IOSAlternateAdUnitID
				: IOSAdUnitID;
#else
			// Android, and editor/standalone fallback.
			return useAlternate && !string.IsNullOrEmpty(AndroidAlternateAdUnitID)
				? AndroidAlternateAdUnitID
				: AndroidAdUnitID;
#endif
		}

		/// <summary>
		/// Checks if this placement has any rewards.
		/// </summary>
		public bool HasRewards()
		{
			return Reward != null;
		}

		/// <summary>
		/// Checks if this placement has a specific tag.
		/// </summary>
		public bool HasTag(string tag)
		{
			return Tags != null && Tags.Contains(tag);
		}

		/// <summary>
		/// Gets a debug-friendly name for this placement.
		/// <summary>
		/// Gets the effective loading strategy based on the preset or custom settings.
		/// </summary>
		public AdLoadingStrategy GetEffectiveLoadingStrategy()
		{
			// If using a preset, return the preset strategy
			if (StrategyPreset != AdLoadingStrategyPreset.Custom)
			{
				return StrategyPreset switch
				{
					AdLoadingStrategyPreset.Aggressive => AdLoadingStrategy.Presets.Aggressive,
					AdLoadingStrategyPreset.Standard   => AdLoadingStrategy.Presets.Standard,
					AdLoadingStrategyPreset.Lazy       => AdLoadingStrategy.Presets.Lazy,
					AdLoadingStrategyPreset.Manual     => AdLoadingStrategy.Presets.Manual,
					_                                  => AdLoadingStrategy.Presets.Standard
				};
			}

			// Use custom strategy
			return LoadingStrategy ?? AdLoadingStrategy.Presets.Standard;
		}

		/// <summary>
		/// Gets a debug-friendly name for this placement.
		/// </summary>
		public string GetDebugName()
		{
			return string.IsNullOrEmpty(DisplayName)
				? PlacementID
				: DisplayName;
		}

		public override string ToString()
		{
			return $"[AdPlacementDefinition] {PlacementID} ({AdType})";
		}
	}
}