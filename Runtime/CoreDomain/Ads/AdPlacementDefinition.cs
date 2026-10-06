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

		[Header("Reward Configuration")] [Tooltip("Identity of the reward granted when this ad completes. The framework does not know the game's reward type; resolve through the game's reward registry at grant time.")]
		public Uid Reward;

		[Tooltip("Optional identity of a reward bundle for multiple rewards.")]
		public Uid RewardBundle;

		[Header("Frequency Control")] [Tooltip("Most impressions per session (0 = no limit). Counted on this device.")] [Range(0, 100)]
		public int MaxPerSession = 0;

		[Tooltip("Most impressions per UTC day (0 = no limit). Counted on this device, across launches.")] [Range(0, 100)]
		public int MaxPerDay = 0;

		[Tooltip("Least time between two impressions, in seconds (0 = none). Counted on this device, across launches.")] [Range(0, 86400)]
		public int CooldownSeconds = 0;

		[Header("Level Requirements")] [Tooltip("Lowest player level that sees this ad (0 = no requirement).")] [Range(0, 1000)]
		public int MinPlayerLevel = 0;

		[Tooltip("Highest player level that sees this ad (0 = no limit).")] [Range(0, 1000)]
		public int MaxPlayerLevel = 0;

		[Header("Remote Config Overrides")]
		[Tooltip("Switches the placement on or off remotely. Once it has a remote value, that wins over IsEnabled either way; until then IsEnabled decides.")]
		public RemoteBool EnabledRemote;

		[Tooltip("Overrides MaxPerSession once it has a remote value.")]
		public RemoteInt MaxPerSessionRemote;

		[Tooltip("Overrides MaxPerDay once it has a remote value.")]
		public RemoteInt MaxPerDayRemote;

		[Tooltip("Overrides CooldownSeconds once it has a remote value.")]
		public RemoteInt CooldownSecondsRemote;

		[Tooltip("Overrides MinPlayerLevel once it has a remote value.")]
		public RemoteInt MinPlayerLevelRemote;

		[Header("Loading Strategy")] [Tooltip("Loading strategy for this placement. Controls auto-reload, retry behavior, etc.")]
		public AdLoadingStrategy LoadingStrategy = AdLoadingStrategy.Presets.Standard;

		[Tooltip("Strategy preset to use. Changes will override the LoadingStrategy above.")]
		public AdLoadingStrategyPreset StrategyPreset = AdLoadingStrategyPreset.Standard;

		[Header("Advanced Settings")] [Tooltip("Whether this placement is switched on. EnabledRemote wins once it has a remote value.")]
		public bool IsEnabled = true;

		[Tooltip("Tags for categorization and filtering.")]
		public List<string> Tags = new();


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
		/// Whether the placement is switched on: <see cref="IsEnabled"/>, unless
		/// <see cref="EnabledRemote"/> has a remote value, which then wins either way.
		/// </summary>
		public bool IsSwitchedOn => RemoteOverride.Resolve(EnabledRemote, IsEnabled);

		/// <summary>Whether <paramref name="level"/> is within the placement's level range.</summary>
		public bool AllowsLevel(int level) =>
			level >= GetMinPlayerLevel() && (MaxPlayerLevel <= 0 || level <= MaxPlayerLevel);

		/// <summary>
		/// Whether the placement is switched on and <paramref name="currentLevel"/> is in its
		/// range. Its ad type's switches and level live on <see cref="AdsMeta"/>.
		/// </summary>
		public bool IsAvailable(int currentLevel = 1) => IsSwitchedOn && AllowsLevel(currentLevel);

		/// <summary>The most impressions per session: the remote value once known, otherwise <see cref="MaxPerSession"/>. 0 is no limit.</summary>
		public int GetMaxPerSession() => RemoteOverride.Resolve(MaxPerSessionRemote, MaxPerSession);

		/// <summary>The most impressions per UTC day: the remote value once known, otherwise <see cref="MaxPerDay"/>. 0 is no limit.</summary>
		public int GetMaxPerDay() => RemoteOverride.Resolve(MaxPerDayRemote, MaxPerDay);

		/// <summary>The least seconds between impressions: the remote value once known, otherwise <see cref="CooldownSeconds"/>.</summary>
		public int GetCooldownSeconds() => RemoteOverride.Resolve(CooldownSecondsRemote, CooldownSeconds);

		/// <summary>The lowest player level that sees the ad: the remote value once known, otherwise <see cref="MinPlayerLevel"/>.</summary>
		public int GetMinPlayerLevel() => RemoteOverride.Resolve(MinPlayerLevelRemote, MinPlayerLevel);

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
			return Reward.IsSet || RewardBundle.IsSet;
		}

		/// <summary>
		/// Checks if this placement has a specific tag.
		/// </summary>
		public bool HasTag(string tag)
		{
			return Tags != null && Tags.Contains(tag);
		}

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