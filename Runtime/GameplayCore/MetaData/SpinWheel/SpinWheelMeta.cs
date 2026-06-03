using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.CoreDomain.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain.SpinWheel
{
    /// <summary>
    /// Container for spin wheel configuration with probability evaluation and spin calculation.
    /// </summary>
    [CreateAssetMenu(fileName = "SpinWheelMeta", menuName = "Gameplay/MetaData/SpinWheel/SpinWheelMeta")]
    public class SpinWheelMeta : MetaDataAsset
    {
        [Header("Spin Wheel Slots")]
        [Tooltip("All 8 slots on the wheel. Slot 1 is at 12 o'clock (0 degrees), proceeding clockwise.")]
        public List<SpinWheelSlot> Slots = new();

        [Header("Spin Animation Configuration")]
        [Tooltip("Minimum number of full rotations before landing on target.")]
        [Range(2, 10)]
        public int MinRotations = 5;

        [Tooltip("Maximum number of full rotations before landing on target.")]
        [Range(2, 15)]
        public int MaxRotations = 8;

        [Tooltip("Base duration of spin animation in seconds.")]
        [Range(1f, 10f)]
        public float SpinDuration = 4f;

        [Tooltip("Duration variance (+/-) in seconds.")]
        [Range(0f, 2f)]
        public float DurationVariance = 0.5f;

        [Tooltip("Easing curve for spin animation.")]
        public AnimationCurve SpinEasing = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Reset Configuration")]
        [Tooltip("Spin cooldown in hours (0 = unlimited spins).")]
        [Range(0, 24)]
        public int SpinCooldownHours = 24;

        [Tooltip("Maximum free spins per day (0 = unlimited).")]
        [Range(0, 10)]
        public int MaxFreeSpinsPerDay = 1;

        [Tooltip("Allow watching ad for extra spin after free spin used.")]
        public bool AllowAdForExtraSpin = true;

        [Tooltip("Maximum ad-supported extra spins per day.")]
        [Range(1, 10)]
        public int MaxAdSpinsPerDay = 3;

        [Header("Timing")]
        [Tooltip("Use UTC time for cooldown calculation.")]
        public bool UseUtcTime = true;

        public int UnlocksAtLevel = 5;

        /// <summary>
        /// Result of a spin evaluation containing all data needed for UI animation.
        /// </summary>
        public class SpinResult
        {
            /// <summary>
            /// The selected slot (1-8).
            /// </summary>
            public int SelectedSlot;

            /// <summary>
            /// The reward for the selected slot.
            /// </summary>
            public RewardDefinition Reward;

            /// <summary>
            /// Target rotation angle in degrees (0-360, where 0 = 12 o'clock).
            /// </summary>
            public float TargetAngle;

            /// <summary>
            /// Rotation delta to add to current rotation (full spins + rotation to bring slot to 12).
            /// </summary>
            public float RotationDelta;

            /// <summary>
            /// Duration of spin animation in seconds.
            /// </summary>
            public float Duration;

            /// <summary>
            /// Easing curve for the spin.
            /// </summary>
            public AnimationCurve Easing;

            /// <summary>
            /// Number of full rotations performed.
            /// </summary>
            public int FullRotations;
        }

        /// <summary>
        /// Evaluates weighted probability and returns spin result with rotation data for UI.
        /// The wheel will rotate so that the selected slot ends up at 12 o'clock (0 degrees).
        /// </summary>
        /// <param name="currentWheelRotation">Current rotation of the wheel in degrees</param>
        public SpinResult EvaluateSpin(float currentWheelRotation = 0f)
        {
            if (Slots == null || Slots.Count == 0)
            {
                Debug.LogError("SpinWheelMeta has no slots configured!");
                return null;
            }

            // Select slot based on probability weights
            var selectedSlot = SelectWeightedSlot();
            if (selectedSlot == null)
            {
                Debug.LogError("Failed to select a slot!");
                return null;
            }

            // Get the target slot's angle on the wheel
            float slotAngle = GetSlotAngle(selectedSlot.SlotNumber);
            
            // Calculate current effective rotation (0-360)
            float currentEffectiveRotation = currentWheelRotation % 360f;
            if (currentEffectiveRotation < 0) currentEffectiveRotation += 360f;
            
            // Calculate how much more we need to rotate to bring the slot to 12 o'clock
            float rotationNeeded = (slotAngle - currentEffectiveRotation + 360f) % 360f;
            
            // Add full rotations for visual effect
            int fullRotations = UnityEngine.Random.Range(MinRotations, MaxRotations + 1);
            float rotationDelta = (fullRotations * 360f) + rotationNeeded;
            
            float duration = SpinDuration + UnityEngine.Random.Range(-DurationVariance, DurationVariance);

            return new SpinResult
            {
                SelectedSlot = selectedSlot.SlotNumber,
                Reward = selectedSlot.Reward,
                TargetAngle = slotAngle,
                RotationDelta = rotationDelta,
                Duration = Mathf.Max(1f, duration),
                Easing = SpinEasing,
                FullRotations = fullRotations
            };
        }

        /// <summary>
        /// Gets the angle for a specific slot (1-8) where 0 = 12 o'clock.
        /// Returns the center angle of the slot wedge.
        /// </summary>
        public float GetSlotAngle(int slotNumber)
        {
            // Slot 1 center = 0° (12 o'clock)
            // Slot 2 center = 45° (clockwise)
            // ...
            // Slot 8 center = 315°
            slotNumber = Mathf.Clamp(slotNumber, 1, 8);
            return (slotNumber - 1) * 45f;
        }

        /// <summary>
        /// Gets the slot at a specific angle (0-360 degrees).
        /// </summary>
        public SpinWheelSlot GetSlotAtAngle(float angle)
        {
            // Normalize angle to 0-360
            angle = angle % 360f;
            if (angle < 0) angle += 360f;

            // Calculate slot number (1-8)
            // Each slot spans 45°, centered on its angle
            // Slot 1: 337.5° to 22.5° (centered at 0°)
            // Add 22.5° to shift to slot boundaries, then divide by 45°
            float adjustedAngle = angle + 22.5f;
            if (adjustedAngle >= 360f) adjustedAngle -= 360f;

            int slotNumber = Mathf.FloorToInt(adjustedAngle / 45f) + 1;
            if (slotNumber > 8) slotNumber = 1;

            return Slots.FirstOrDefault(s => s.SlotNumber == slotNumber);
        }

        /// <summary>
        /// Calculates time until next available spin.
        /// </summary>
        public TimeSpan GetTimeUntilNextSpin(DateTime lastSpinTime)
        {
            if (SpinCooldownHours <= 0)
                return TimeSpan.Zero;

            DateTime nextSpinTime = lastSpinTime.AddHours(SpinCooldownHours);
            DateTime now = UseUtcTime ? DateTime.UtcNow : DateTime.Now;

            if (nextSpinTime <= now)
                return TimeSpan.Zero;

            return nextSpinTime - now;
        }

        /// <summary>
        /// Checks if a spin is available based on last spin time.
        /// </summary>
        public bool IsSpinAvailable(DateTime lastSpinTime)
        {
            return GetTimeUntilNextSpin(lastSpinTime) <= TimeSpan.Zero;
        }

        /// <summary>
        /// Gets total probability weight of all slots.
        /// </summary>
        public float GetTotalWeight()
        {
            return Slots.Sum(s => s.ProbabilityWeight);
        }

        /// <summary>
        /// Gets the probability percentage for a specific slot.
        /// </summary>
        public float GetSlotProbability(int slotNumber)
        {
            var slot = Slots.FirstOrDefault(s => s.SlotNumber == slotNumber);
            if (slot == null) return 0f;

            float totalWeight = GetTotalWeight();
            if (totalWeight <= 0) return 0f;

            return (slot.ProbabilityWeight / totalWeight) * 100f;
        }

        /// <summary>
        /// Validates that all 8 slots are properly configured.
        /// </summary>
        public bool ValidateConfiguration(out string errorMessage)
        {
            if (Slots == null || Slots.Count != 8)
            {
                errorMessage = $"Spin wheel must have exactly 8 slots, but has {Slots?.Count ?? 0}.";
                return false;
            }

            var slotNumbers = Slots.Select(s => s.SlotNumber).OrderBy(n => n).ToList();
            for (int i = 0; i < 8; i++)
            {
                if (slotNumbers[i] != i + 1)
                {
                    errorMessage = $"Missing or duplicate slot number. Expected slots 1-8.";
                    return false;
                }
            }

            float totalWeight = GetTotalWeight();
            if (totalWeight <= 0)
            {
                errorMessage = "Total probability weight must be greater than 0.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private SpinWheelSlot SelectWeightedSlot()
        {
            float totalWeight = GetTotalWeight();
            if (totalWeight <= 0) return null;

            float randomValue = UnityEngine.Random.Range(0, totalWeight);
            float currentWeight = 0;

            foreach (var slot in Slots.OrderBy(s => s.SlotNumber))
            {
                currentWeight += slot.ProbabilityWeight;
                if (randomValue <= currentWeight)
                {
                    return slot;
                }
            }

            // Fallback to last slot
            return Slots.LastOrDefault();
        }

#if UNITY_EDITOR
        [Button("Validate Configuration")]
        private void EditorValidate()
        {
            if (ValidateConfiguration(out string error))
            {
                Debug.Log("Spin wheel configuration is valid!");
                float totalWeight = GetTotalWeight();
                foreach (var slot in Slots.OrderBy(s => s.SlotNumber))
                {
                    float probability = (slot.ProbabilityWeight / totalWeight) * 100f;
                    Debug.Log($"Slot {slot.SlotNumber}: {probability:F1}% - {slot.DisplayName}");
                }
            }
            else
            {
                Debug.LogError($"Validation failed: {error}");
            }
        }
#endif
    }
}
