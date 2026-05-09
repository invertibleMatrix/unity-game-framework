using System;
using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace GameplayCore.MetaData.Difficulty
{
	/// <summary>
	/// Defines difficulty settings for the game.
	/// Controls various gameplay parameters to adjust challenge level.
	/// </summary>
	[CreateAssetMenu(fileName = "DifficultyDefinition", menuName = "Gameplay/MetaData/Difficulty/DifficultyDefinition")]
	public class DifficultyDefinition : MetaDataAsset
	{
		[Header("Identification")]
		[Tooltip("Unique identifier for this difficulty")]
		public UID UID;
		
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
		
		[Header("Bubble Settings")]
		[Tooltip("Number of bubble colors available")]
		[Range(2, 10)]
		public int BubbleColorCount = 5;
		
		[Tooltip("Minimum bubble cluster size to pop")]
		[Range(2, 5)]
		public int MinClusterSize = 3;
		
		[Tooltip("Maximum bubble cluster size to pop")]
		[Range(3, 10)]
		public int MaxClusterSize = 5;
		
		[Tooltip("Bubble spawn rate multiplier")]
		[Range(0.5f, 2f)]
		public float BubbleSpawnRate = 1f;
		
		[Header("Time Settings")]
		[Tooltip("Time limit for level (0 = no limit)")]
		[Min(0)]
		public int TimeLimit = 0;
		
		[Tooltip("Time bonus per bubble popped")]
		[Range(0, 10)]
		public float TimeBonusPerBubble = 0f;
		
		[Tooltip("Time penalty per shot")]
		[Range(0, 5)]
		public float TimePenaltyPerShot = 0f;
		
		[Header("Shot Settings")]
		[Tooltip("Maximum number of shots allowed (0 = unlimited)")]
		[Min(0)]
		public int MaxShots = 0;
		
		[Tooltip("Shot accuracy required (0-1, higher = more accurate)")]
		[Range(0f, 1f)]
		public float RequiredAccuracy = 0f;
		
		[Tooltip("Aim assist strength (0 = none, 1 = full)")]
		[Range(0f, 1f)]
		public float AimAssistStrength = 0.5f;
		
		[Header("Special Tiles")]
		[Tooltip("Enable special tiles")]
		public bool EnableSpecialTiles = true;
		
		[Tooltip("Special tile spawn chance (0-1)")]
		[Range(0f, 1f)]
		public float SpecialTileSpawnChance = 0.1f;
		
		[Tooltip("Maximum special tiles on screen")]
		[Range(0, 20)]
		public int MaxSpecialTiles = 5;
		
		[Header("Powerups")]
		[Tooltip("Enable powerups")]
		public bool EnablePowerups = true;
		
		[Tooltip("Powerup spawn chance (0-1)")]
		[Range(0f, 1f)]
		public float PowerupSpawnChance = 0.05f;
		
		[Tooltip("Maximum powerups on screen")]
		[Range(0, 10)]
		public int MaxPowerups = 3;
		
		[Header("AI/Enemy Settings")]
		[Tooltip("Enable AI enemies")]
		public bool EnableEnemies = false;
		
		[Tooltip("Enemy spawn rate multiplier")]
		[Range(0.5f, 2f)]
		public float EnemySpawnRate = 1f;
		
		[Tooltip("Enemy speed multiplier")]
		[Range(0.5f, 2f)]
		public float EnemySpeedMultiplier = 1f;
		
		[Tooltip("Enemy health multiplier")]
		[Range(0.5f, 2f)]
		public float EnemyHealthMultiplier = 1f;
		
		[Header("Scoring")]
		[Tooltip("Score multiplier")]
		[Range(0.5f, 3f)]
		public float ScoreMultiplier = 1f;
		
		[Tooltip("Combo multiplier")]
		[Range(1f, 5f)]
		public float ComboMultiplier = 1.5f;
		
		[Tooltip("Star score thresholds (1, 2, 3 stars)")]
		public Vector3 StarScoreThresholds = new Vector3(1000, 2000, 3000);
		
		[Header("Lives")]
		[Tooltip("Number of lives")]
		[Range(1, 10)]
		public int Lives = 3;
		
		[Tooltip("Lives lost per failed shot")]
		[Range(0, 3)]
		public int LivesLostPerFailedShot = 0;
		
		[Tooltip("Lives lost per time limit")]
		[Range(0, 3)]
		public int LivesLostPerTimeLimit = 1;
		
		[Header("Visual Settings")]
		[Tooltip("Show hints")]
		public bool ShowHints = true;
		
		[Tooltip("Hint delay (seconds)")]
		[Range(0, 30)]
		public float HintDelay = 10f;
		
		[Tooltip("Show trajectory preview")]
		public bool ShowTrajectory = true;
		
		[Tooltip("Trajectory length")]
		[Range(0, 10)]
		public int TrajectoryLength = 5;
		
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
		
		/// <summary>
		/// Gets the star score threshold for a specific star count.
		/// </summary>
		public int GetStarScoreThreshold(int stars)
		{
			stars = Mathf.Clamp(stars, 1, 3);
			return (int)StarScoreThresholds[stars - 1];
		}
		
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

		public UID UniqueID => UID;
	}
}