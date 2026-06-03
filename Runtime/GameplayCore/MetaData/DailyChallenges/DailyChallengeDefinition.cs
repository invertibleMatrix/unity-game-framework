using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain.DailyChallenges
{
	/// <summary>
	/// Definition for a daily challenge with completion criteria and rewards
	/// </summary>
	[CreateAssetMenu(fileName = "DailyChallengeDefinition", menuName = "Gameplay/MetaData/DailyChallenges/DailyChallengeDefinition")]
	public class DailyChallengeDefinition : MetaDataAsset
	{
		[Header("Identification")]
		[Tooltip("Unique string ID for this challenge")]
		public string ChallengeID;
		
		[Header("Display Information")]
		[Tooltip("Display name shown to players")]
		public string DisplayName;
		
		[Tooltip("Description shown to players")]
		[TextArea]
		public string Description;
		
		[Tooltip("Icon displayed in UI")]
		public Sprite Icon;
		
		[Tooltip("Difficulty level of this challenge")]
		public ChallengeDifficulty Difficulty;
		
		[Header("Challenge Type")]
		[Tooltip("Type of challenge based on completion criteria")]
		public ChallengeType Type;
		
		[Header("Completion Criteria")]
		[Tooltip("Target value to complete the challenge")]
		public int TargetValue;
		
		[Tooltip("Current progress (for tracking)")]
		[ReadOnly]
		public int CurrentProgress;
		
		[Tooltip("Is this challenge currently active?")]
		public bool IsActive;
		
		[Header("Requirements")]
		[Tooltip("Minimum level required to access this challenge")]
		public int MinimumLevel;
		
		[Tooltip("Maximum level for this challenge (0 = no limit)")]
		public int MaximumLevel;
		
		[Tooltip("Required powerup type (for PowerupUse challenges)")]
		public UID RequiredPowerupUID;
		
		[Tooltip("Required theme (for ThemeComplete challenges)")]
		public UID RequiredThemeUID;
		
		[Tooltip("Required booster type (for BoosterUse challenges)")]
		public UID RequiredBoosterUID;
		
		[Tooltip("Specific level number (for SpecificLevel challenges)")]
		public int SpecificLevelNumber;
		
		[Header("Rewards")]
		[Tooltip("Rewards granted on completion")]
		public List<RewardDefinition> Rewards;
		
		[Tooltip("Bonus rewards for early completion")]
		public List<RewardDefinition> EarlyCompletionBonus;
		
		[Tooltip("Time limit for early completion bonus (in hours)")]
		public float EarlyCompletionTimeLimit;
		
		[Header("Time Limits")]
		[Tooltip("Does this challenge have a time limit?")]
		public bool HasTimeLimit;
		
		[ShowIf("HasTimeLimit")]
		[Tooltip("Time limit in seconds")]
		public float TimeLimit;
		
		[Header("Scheduling")]
		[Tooltip("Days of the week this challenge is available (0 = Sunday, 6 = Saturday)")]
		public List<int> AvailableDays;
		
		[Tooltip("Is this a recurring challenge?")]
		public bool IsRecurring;
		
		[ShowIf("IsRecurring")]
		[Tooltip("Recurrence interval in days")]
		public int RecurrenceInterval;
		
		[Header("Progression")]
		[Tooltip("Milestone rewards for partial progress")]
		public List<ChallengeMilestone> Milestones;
		
		[Header("Analytics")]
		[Tooltip("Analytics event to track when this challenge is completed")]
		public string CompletionEventID;
		
		[Header("Additional Data")]
		[Tooltip("Additional data for custom challenge types")]
		public Dictionary<string, string> CustomData;
		
		/// <summary>
		/// Check if the challenge is completed
		/// </summary>
		public bool IsCompleted => CurrentProgress >= TargetValue;
		
		/// <summary>
		/// Get completion percentage (0-100)
		/// </summary>
		public float CompletionPercentage => TargetValue > 0 ? (float)CurrentProgress / TargetValue * 100f : 0f;
		
		/// <summary>
		/// Check if this challenge is available to the player
		/// </summary>
		public bool IsAvailable(int playerLevel, int currentDayOfWeek)
		{
			if (!IsActive) return false;
			if (playerLevel < MinimumLevel) return false;
			if (MaximumLevel > 0 && playerLevel > MaximumLevel) return false;
			if (AvailableDays != null && AvailableDays.Count > 0 && !AvailableDays.Contains(currentDayOfWeek)) return false;
			
			return true;
		}
		
		/// <summary>
		/// Add progress to the challenge
		/// </summary>
		public void AddProgress(int amount)
		{
			CurrentProgress = Mathf.Min(CurrentProgress + amount, TargetValue);
		}
		
		/// <summary>
		/// Reset progress (for recurring challenges)
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
		
		/// <summary>
		/// Check if eligible for early completion bonus
		/// </summary>
		public bool IsEligibleForEarlyCompletion(float elapsedTime)
		{
			return elapsedTime < EarlyCompletionTimeLimit * 3600f; // Convert hours to seconds
		}

		public UID UniqueID => this;
	}
	
	/// <summary>
	/// Difficulty levels for daily challenges
	/// </summary>
	public enum ChallengeDifficulty
	{
		Easy,
		Medium,
		Hard,
		Expert,
		Master
	}
	
	/// <summary>
	/// Milestone reward for partial challenge progress
	/// </summary>
	[Serializable]
	public class ChallengeMilestone
	{
		[Tooltip("Progress threshold for this milestone")]
		public int ProgressThreshold;
		
		[Tooltip("Rewards granted at this milestone")]
		public List<RewardDefinition> Rewards;
		
		[Tooltip("Has this milestone been rewarded?")]
		[ReadOnly]
		public bool Rewarded;
	}
}