using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.CoreDomain.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain.DailyRewards
{
    /// <summary>
    /// Container for all daily reward configurations with query methods.
    /// </summary>
    [CreateAssetMenu(fileName = "DailyRewardsMeta", menuName = "Gameplay/MetaData/DailyRewards/DailyRewardsMeta")]
    public class DailyRewardsMeta : MetaDataAsset
    {
        [Header("Daily Rewards Configuration")]
        [Tooltip("All 30 daily reward slots. Day 1-30 rewards.")]
        public List<DailyRewardSlot> RewardSlots = new();

        [Header("Streak Bonuses")]
        [Tooltip("Bonus rewards for maintaining consecutive login streaks.")]
        public List<StreakBonusDefinition> StreakBonuses = new();

        [Header("Reset Configuration")]
        [Tooltip("Hour of day (0-23) when daily rewards reset (local time or UTC based on setting).")]
        [Range(0, 23)]
        public int ResetHour = 0;

        [Tooltip("Use UTC time instead of local device time for reset calculation.")]
        public bool UseUtcTime = true;

        [Header("Grace Period")]
        [Tooltip("Hours after reset time where player can still claim previous day's reward (0 = no grace period).")]
        [Range(0, 23)]
        public int GracePeriodHours = 0;

        public int UnlocksAtLevel = 5;
        
        /// <summary>
        /// Gets the reward for a specific day (1-30).
        /// </summary>
        public DailyRewardSlot GetRewardForDay(int dayNumber)
        {
            dayNumber = Mathf.Clamp(dayNumber, 1, 30);
            return RewardSlots.FirstOrDefault(r => r.DayNumber == dayNumber);
        }

        /// <summary>
        /// Gets all rewards up to a specific day.
        /// </summary>
        public List<RewardDefinition> GetRewardsUpToDay(int dayNumber)
        {
            dayNumber = Mathf.Clamp(dayNumber, 1, 30);
            return RewardSlots
                .Where(r => r.DayNumber <= dayNumber && r.Reward != null)
                .Select(r => r.Reward)
                .ToList();
        }

        /// <summary>
        /// Gets streak bonuses that should be awarded for current streak count.
        /// </summary>
        public List<StreakBonusDefinition> GetApplicableStreakBonuses(int currentStreak)
        {
            return StreakBonuses
                .Where(b => b.IsMilestoneMet(currentStreak))
                .OrderBy(b => b.RequiredStreakDays)
                .ToList();
        }

        /// <summary>
        /// Gets the current time based on configuration (UTC or local).
        /// </summary>
        public DateTime GetCurrentTime()
        {
        	return UseUtcTime ? DateTime.UtcNow : DateTime.Now;
        }
      
        /// <summary>
        /// Calculates the time until next daily reset.
        /// </summary>
        public TimeSpan GetTimeUntilNextReset()
        {
        	DateTime now = GetCurrentTime();
        	DateTime nextReset = GetNextResetTime(now);
        	return nextReset - now;
        }

        /// <summary>
        /// Gets the next reset time from a given date.
        /// </summary>
        public DateTime GetNextResetTime(DateTime fromDate)
        {
            DateTime nextReset = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, ResetHour, 0, 0);
            
            if (UseUtcTime && fromDate.Kind != DateTimeKind.Utc)
            {
                nextReset = DateTime.SpecifyKind(nextReset, DateTimeKind.Utc);
            }
            
            if (nextReset <= fromDate)
            {
                nextReset = nextReset.AddDays(1);
            }
            
            return nextReset;
        }

        /// <summary>
        /// Checks if a claim time is within the grace period of its target day.
        /// </summary>
        public bool IsWithinGracePeriod(DateTime claimTime, DateTime targetDayReset)
        {
            if (GracePeriodHours <= 0) return false;
            
            DateTime gracePeriodEnd = targetDayReset.AddHours(GracePeriodHours);
            return claimTime >= targetDayReset && claimTime <= gracePeriodEnd;
        }

        /// <summary>
        /// Gets all available streak milestone days (for UI display).
        /// </summary>
        public List<int> GetStreakMilestoneDays()
        {
            return StreakBonuses
                .Select(b => b.RequiredStreakDays)
                .OrderBy(d => d)
                .ToList();
        }

        /// <summary>
        /// Gets the next streak milestone from current streak.
        /// </summary>
        public StreakBonusDefinition GetNextStreakMilestone(int currentStreak)
        {
            return StreakBonuses
                .Where(b => b.RequiredStreakDays > currentStreak)
                .OrderBy(b => b.RequiredStreakDays)
                .FirstOrDefault();
        }

        /// <summary>
        /// Calculates streak progress (0-1) toward next milestone.
        /// </summary>
        public float GetStreakProgress(int currentStreak)
        {
            var nextMilestone = GetNextStreakMilestone(currentStreak);
            if (nextMilestone == null) return 1f;
            
            var prevMilestone = StreakBonuses
                .Where(b => b.RequiredStreakDays <= currentStreak)
                .OrderByDescending(b => b.RequiredStreakDays)
                .FirstOrDefault();
            
            int prevDay = prevMilestone?.RequiredStreakDays ?? 0;
            int targetDay = nextMilestone.RequiredStreakDays;
            
            if (targetDay == prevDay) return 1f;
            
            return (float)(currentStreak - prevDay) / (targetDay - prevDay);
        }
    }
}
