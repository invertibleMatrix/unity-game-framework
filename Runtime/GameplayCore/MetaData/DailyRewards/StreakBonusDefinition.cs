using System;
using AK.CoreDomain.Rewards;
using UnityEngine;

namespace AK.CoreDomain.DailyRewards
{
    /// <summary>
    /// Bonus reward granted for maintaining a consecutive login streak.
    /// </summary>
    [Serializable]
    public class StreakBonusDefinition
    {
        [Tooltip("Streak day count required to receive this bonus (e.g., 3 for 3-day streak)."), Min(2)]
        public int RequiredStreakDays;

        [Tooltip("The bonus reward granted when streak milestone is reached.")]
        public RewardDefinition BonusReward;

        [Tooltip("Optional display label for this streak bonus.")]
        public string DisplayLabel;

        [Tooltip("Optional custom icon for this streak milestone.")]
        public Sprite CustomIcon;

        public bool IsMilestoneMet(int currentStreak) => currentStreak >= RequiredStreakDays;
    }
}
