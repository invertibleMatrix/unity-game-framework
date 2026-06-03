using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain.Progression
{
	/// <summary>
	/// Defines a player level with XP requirements and rewards.
	/// </summary>
	[CreateAssetMenu(fileName = "ProgressionLevel", menuName = "Gameplay/MetaData/Progression/ProgressionLevel")]
	public class ProgressionLevel : MetaDataAsset
	{
		[Header("Level Information")]
		[Tooltip("Player level number.")]
		public int Level = 1;

		[Tooltip("Display name for this level.")]
		public string DisplayName;

		[Tooltip("Title or rank name for this level.")]
		public string Title;

		[Tooltip("Description of this level.")]
		[TextArea(2, 4)]
		public string Description;

		[Tooltip("Icon displayed in UI.")]
		public Sprite Icon;

		[Header("XP Requirements")]
		[Tooltip("Total XP required to reach this level.")]
		public long RequiredXP = 0;

		[Tooltip("XP required to reach the next level.")]
		public long XPToNextLevel = 100;

		[Tooltip("XP multiplier for this level (1.0 = normal, 1.5 = 50% more XP).")]
		[Range(1f, 5f)]
		public float XPMultiplier = 1f;

		[Header("Level Rewards")]
		[Tooltip("Rewards granted when reaching this level.")]
		public List<LevelReward> LevelRewards;

		[Header("Unlocks")]
		[Tooltip("Features unlocked at this level.")]
		public List<string> Unlocks;

		[Header("Prestige")]
		[Tooltip("Can this level be prestiged (reset for bonuses)?")]
		public bool CanPrestige = false;

			[Tooltip("Prestige bonus multiplier (applied after prestige).")]
		[Range(1f, 5f)]
		public float PrestigeBonusMultiplier = 1.1f;

		[Header("Display")]
		[Tooltip("Display priority in UI (lower = higher priority).")]
		[Range(0, 100)]
		public int DisplayPriority = 0;

		[Header("Analytics")]
		[Tooltip("Custom analytics event name for tracking level up.")]
		public string AnalyticsEventName;

		public virtual UID UniqueID => this;
		
		/// <summary>
		/// Gets the total XP required to reach this level.
		/// </summary>
		public long GetTotalXPRequired()
		{
			return RequiredXP;
		}

		/// <summary>
		/// Gets the XP range for this level.
		/// </summary>
		public (long minXP, long maxXP) GetXPRange()
		{
			return (RequiredXP, RequiredXP + XPToNextLevel);
		}

		/// <summary>
		/// Gets all level rewards.
		/// </summary>
		public List<LevelReward> GetAllRewards()
		{
			return LevelRewards ?? new List<LevelReward>();
		}

		/// <summary>
		/// Gets all unlocks for this level.
		/// </summary>
		public List<string> GetAllUnlocks()
		{
			return Unlocks ?? new List<string>();
		}

		/// <summary>
		/// Checks if a feature is unlocked at this level.
		/// </summary>
		public bool IsUnlocked(string featureID)
		{
			return Unlocks != null && Unlocks.Contains(featureID);
		}

	}

	/// <summary>
	/// Defines a reward granted at a specific level.
	/// </summary>
	[Serializable]
	public class LevelReward
	{
		[Tooltip("The reward to grant.")]
		public RewardDefinition Reward;

		[Tooltip("Quantity of this reward to grant.")]
		public int Quantity = 1;

		[Tooltip("Is this reward optional?")]
		public bool IsOptional = false;
	}
}