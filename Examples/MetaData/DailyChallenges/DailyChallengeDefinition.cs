using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Examples.DailyChallenges
{
    /// <summary>
    /// Definition for a daily challenge with completion criteria and rewards.
    /// Contains universal parameters; game-specific settings go in CustomData.
    /// </summary>
    [CreateAssetMenu(fileName = "DailyChallengeDefinition", menuName = "AK/Examples/MetaData/DailyChallenges/DailyChallengeDefinition")]
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
        [Tooltip("Game-specific data (e.g., required powerup, theme, booster UIDs)")]
        public Dictionary<string, string> CustomData;

        public bool IsCompleted => CurrentProgress >= TargetValue;
        public float CompletionPercentage => TargetValue > 0 ? (float)CurrentProgress / TargetValue * 100f : 0f;

        public bool IsAvailable(int playerLevel, int currentDayOfWeek)
        {
            if (!IsActive) return false;
            if (playerLevel < MinimumLevel) return false;
            if (MaximumLevel > 0 && playerLevel > MaximumLevel) return false;
            if (AvailableDays != null && AvailableDays.Count > 0 && !AvailableDays.Contains(currentDayOfWeek)) return false;
            return true;
        }

        public void AddProgress(int amount) => CurrentProgress = Mathf.Min(CurrentProgress + amount, TargetValue);
        public void ResetProgress() => CurrentProgress = 0;

        public List<RewardDefinition> GetMilestoneRewards()
        {
            List<RewardDefinition> rewards = new List<RewardDefinition>();
            foreach (var milestone in Milestones)
            {
                if (CurrentProgress >= milestone.ProgressThreshold && !milestone.Rewarded)
                    rewards.AddRange(milestone.Rewards);
            }
            return rewards;
        }

        public bool IsEligibleForEarlyCompletion(float elapsedTime) => elapsedTime < EarlyCompletionTimeLimit * 3600f;

        public UID UniqueID => this;
    }

    public enum ChallengeDifficulty { Easy, Medium, Hard, Expert, Master }

    [Serializable]
    public class ChallengeMilestone
    {
        public int ProgressThreshold;
        public List<RewardDefinition> Rewards;
        [ReadOnly] public bool Rewarded;
    }
}
