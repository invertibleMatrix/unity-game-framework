using System;
using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace AK.Examples.Difficulty
{
    /// <summary>
    /// Defines difficulty settings for the game.
    /// Contains universal parameters; game-specific settings go in CustomData.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyDefinition", menuName = "Examples/MetaData/Difficulty/DifficultyDefinition")]
    public class DifficultyDefinition : MetaDataAsset
    {
        [Header("Identification")]
        [Tooltip("Display name for the difficulty")]
        public string DisplayName;

        [Tooltip("Internal name for reference")]
        public string InternalName;

        [Header("Classification")]
        [Tooltip("Type of difficulty")]
        public DifficultyType Type;

        [Tooltip("Difficulty level (1-10, higher = harder)")]
        [Range(1, 10)]
        public int DifficultyLevel = 5;

        [Header("Level Range")]
        [Tooltip("Minimum level this difficulty applies to")]
        [Min(1)]
        public int MinLevel = 1;

        [Tooltip("Maximum level this difficulty applies to")]
        [Min(1)]
        public int MaxLevel = 999;

        [Header("Time Settings")]
        [Tooltip("Time limit for level (0 = no limit)")]
        [Min(0)]
        public int TimeLimit = 0;

        [Header("Lives")]
        [Tooltip("Number of lives")]
        [Range(1, 10)]
        public int Lives = 3;

        [Header("Scoring")]
        [Tooltip("Score multiplier")]
        [Range(0.5f, 3f)]
        public float ScoreMultiplier = 1f;

        [Tooltip("Combo multiplier")]
        [Range(1f, 5f)]
        public float ComboMultiplier = 1.5f;

        [Header("Progression")]
        [Tooltip("Level progression speed (0.5-2, higher = faster)")]
        [Range(0.5f, 2f)]
        public float ProgressionSpeed = 1f;

        [Tooltip("Difficulty increase per level")]
        [Range(0f, 0.5f)]
        public float DifficultyIncreasePerLevel = 0.05f;

        [Header("Rewards")]
        [Tooltip("Reward multiplier")]
        [Range(0.5f, 3f)]
        public float RewardMultiplier = 1f;

        [Tooltip("Bonus rewards for completion")]
        public List<UID> BonusRewards = new();

        [Header("Visual Settings")]
        [Tooltip("Show hints")]
        public bool ShowHints = true;

        [Tooltip("Hint delay (seconds)")]
        [Range(0, 30)]
        public float HintDelay = 10f;

        [Header("Additional Data")]
        [Tooltip("Game-specific data (e.g., bubble settings, enemy params, aim assist)")]
        public Dictionary<string, string> CustomData;

        /// <summary>
        /// Checks if this difficulty applies to a specific level.
        /// </summary>
        public bool AppliesToLevel(int level)
        {
            return level >= MinLevel && level <= MaxLevel;
        }

        /// <summary>
        /// Gets the adjusted difficulty for a specific level.
        /// </summary>
        public float GetAdjustedDifficulty(int level)
        {
            float levelProgress = (float)(level - MinLevel) / (MaxLevel - MinLevel);
            return DifficultyLevel + (levelProgress * DifficultyIncreasePerLevel * 10f);
        }

        public UID UniqueID => this;
    }
}
