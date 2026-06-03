using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain.Achievements
{
	/// <summary>
	/// Definition for an achievement with completion criteria and rewards
	/// </summary>
	[CreateAssetMenu(fileName = "AchievementDefinition", menuName = "Gameplay/MetaData/Achievements/AchievementDefinition")]
	public class AchievementDefinition : MetaDataAsset
	{
		[Tooltip("Unique string ID for this achievement")]
		public string AchievementID;

		[Tooltip("Rarity/Importance of this achievement")]
		public AchievementRarity Rarity;

		[Header("Achievement Type")] [Tooltip("Type of achievement based on completion criteria")]
		public AchievementType Type;

		[Header("Completion Criteria")] [Tooltip("Target value to complete the achievement")]
		public int TargetValue;

		[Tooltip("Current progress (for tracking)")] [ReadOnly]
		public int CurrentProgress;

		[Tooltip("Is this achievement currently active?")]
		public bool IsActive;

		[Tooltip("Is this achievement hidden until discovered?")]
		public bool IsHidden;

		[Tooltip("Is this achievement repeatable?")]
		public bool IsRepeatable;

		[ShowIf("IsRepeatable")] [Tooltip("How many times can this achievement be completed? 0 = unlimited")]
		public int MaxCompletions;

		[ShowIf("IsRepeatable")] [Tooltip("Cooldown between completions in seconds")]
		public float CompletionCooldown;

		[Header("Prerequisites")] [Tooltip("Achievements that must be completed before this one")]
		public List<UID> PrerequisiteAchievements;

		[Tooltip("Minimum level required to unlock this achievement")]
		public int MinimumLevel;

		[Header("Rewards")] [Tooltip("Rewards granted on completion")]
		public List<RewardDefinition> Rewards;

		[Tooltip("Bonus rewards for first completion")] [ShowIf("IsRepeatable")]
		public List<RewardDefinition> FirstCompletionBonus;

		[Header("Progression")] [Tooltip("Milestone rewards for partial progress")]
		public List<AchievementMilestone> Milestones;

		[Header("Time Limits")] [Tooltip("Is there a time limit to complete this achievement?")]
		public bool HasTimeLimit;

		[ShowIf("HasTimeLimit")] [Tooltip("Time limit in seconds")]
		public float TimeLimit;

		[ShowIf("HasTimeLimit")] [Tooltip("Does the achievement expire after the time limit?")]
		public bool ExpiresAfterTimeLimit;

		[Header("Analytics")] [Tooltip("Analytics event to track when this achievement is completed")]
		public string CompletionEventID;

		[Header("Additional Data")] [Tooltip("Additional data for custom achievement types")]
		public Dictionary<string, string> CustomData;

		/// <summary>
		/// Check if the achievement is completed
		/// </summary>
		public bool IsCompleted => CurrentProgress >= TargetValue;

		/// <summary>
		/// Get completion percentage (0-100)
		/// </summary>
		public float CompletionPercentage => TargetValue > 0 ? (float)CurrentProgress / TargetValue * 100f : 0f;

		public UID UniqueID => this;

		/// <summary>
		/// Check if this achievement is available to the player
		/// </summary>
		public bool IsAvailable(int playerLevel, List<UID> completedAchievements)
		{
			if (!IsActive) return false;
			if (playerLevel < MinimumLevel) return false;

			foreach (var prereq in PrerequisiteAchievements)
			{
				if (!completedAchievements.Contains(prereq))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Add progress to the achievement
		/// </summary>
		public void AddProgress(int amount)
		{
			CurrentProgress = Mathf.Min(CurrentProgress + amount, TargetValue);
		}

		/// <summary>
		/// Reset progress (for repeatable achievements)
		/// </summary>
		public void ResetProgress()
		{
			CurrentProgress = 0;
		}

		/// <summary>
		/// Get milestone rewards for current progress
		/// </summary>
		public List<RewardDefinition> GetMilestoneRewards()
		{
			List<RewardDefinition> rewards = new List<RewardDefinition>();

			foreach (var milestone in Milestones)
			{
				if (CurrentProgress >= milestone.ProgressThreshold && !milestone.Rewarded)
				{
					rewards.AddRange(milestone.Rewards);
				}
			}

			return rewards;
		}
	}

	/// <summary>
	/// Rarity levels for achievements
	/// </summary>
	public enum AchievementRarity
	{
		Common,
		Uncommon,
		Rare,
		Epic,
		Legendary
	}

	/// <summary>
	/// Milestone reward for partial achievement progress
	/// </summary>
	[Serializable]
	public class AchievementMilestone
	{
		[Tooltip("Progress threshold for this milestone")]
		public int ProgressThreshold;

		[Tooltip("Rewards granted at this milestone")]
		public List<RewardDefinition> Rewards;

		[Tooltip("Has this milestone been rewarded?")] [ReadOnly]
		public bool Rewarded;
	}
}