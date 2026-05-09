using System;
using GameplayCore.MetaData.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.MetaData.DailyRewards
{
    /// <summary>
    /// Configuration for a single day in the daily rewards calendar.
    /// </summary>
    [Serializable]
    public class DailyRewardSlot
    {
        [Tooltip("Day number (1-30)."), Range(1, 30)]
        public int DayNumber;

        [Tooltip("The reward granted for this day.")]
        [InlineEditor]
        public RewardDefinition Reward;

        [Tooltip("Optional display override for this day's reward.")]
        public Sprite CustomIcon;

        [Tooltip("Optional label shown on this day (e.g., 'Bonus!', 'Jackpot!').")]
        public string CustomLabel;

        [Tooltip("Is this a milestone day with special visuals?")]
        public bool IsMilestone;
    }
}
