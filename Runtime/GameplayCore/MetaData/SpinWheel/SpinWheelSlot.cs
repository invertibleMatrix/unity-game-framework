using System;
using AK.CoreDomain.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain.SpinWheel
{
    /// <summary>
    /// A single slot on the spin wheel with probability weight and reward.
    /// </summary>
    [Serializable]
    public class SpinWheelSlot
    {
        [Tooltip("Unique identifier for this slot (1-8)."), Range(1, 8)]
        public int SlotNumber;

        [Tooltip("The reward granted when this slot is selected.")]
        public RewardDefinition Reward;

        [Tooltip("Probability weight for this slot. Higher = more likely."), Min(0.1f)]
        public float ProbabilityWeight = 1f;

        [Tooltip("Optional custom icon for this slot (overrides reward icon).")]
        public Sprite CustomIcon;

        [Tooltip("Optional custom label shown on the wheel.")]
        public string CustomLabel;

        [Tooltip("Color for this slot's wedge on the wheel.")]
        public Color WedgeColor = Color.white;

        [Tooltip("Is this a rare/high-value slot with special effects?")]
        public bool IsRareSlot;

        /// <summary>
        /// Gets the display name for this slot.
        /// </summary>
        public string DisplayName => string.IsNullOrEmpty(CustomLabel) 
            ? (Reward?.DisplayName ?? $"Slot {SlotNumber}") 
            : CustomLabel;

        /// <summary>
        /// Gets the icon for this slot.
        /// </summary>
        public Sprite Icon => CustomIcon ?? Reward?.Icon;
    }
}
