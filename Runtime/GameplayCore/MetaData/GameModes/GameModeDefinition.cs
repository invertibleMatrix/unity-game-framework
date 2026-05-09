using System;
using System.Collections.Generic;
using AK.Core;
using GameplayCore.MetaData.Rewards;
using UnityEngine;

namespace GameplayCore.MetaData.GameModes
{
	/// <summary>
	/// Defines a game mode with specific rules, objectives, and settings.
	/// Game modes provide different ways to play the game with unique challenges.
	/// </summary>
	[CreateAssetMenu(fileName = "GameModeDefinition", menuName = "Gameplay/MetaData/GameModes/GameModeDefinition")]
	public class GameModeDefinition : MetaDataAsset
	{
		[Header("Identification")]
		[Tooltip("Unique identifier for this game mode")]
		public UID UID;
		
		[Tooltip("Display name for the game mode")]
		public string DisplayName;
		
		[Tooltip("Internal name for reference")]
		public string InternalName;
		
		[Header("Classification")]
		[Tooltip("Type of game mode")]
		public GameModeType Type;
		
		[Tooltip("Priority for displaying game modes (lower = higher priority)")]
		[Range(0, 100)]
		public int Priority = 50;
		
		[Header("Availability")]
		[Tooltip("Minimum player level required to unlock this mode")]
		[Min(1)]
		public int MinPlayerLevel = 1;
		
		[Tooltip("Is this mode always available")]
		public bool AlwaysAvailable = true;
		
		[Tooltip("Is this mode featured/highlighted")]
		public bool IsFeatured = false;
		
		[Tooltip("Is this mode currently active")]
		public bool IsActive = true;
		
		[Header("Description")]
		[Tooltip("Short description of the game mode")]
		[TextArea(2, 4)]
		public string ShortDescription;
		
		[Tooltip("Full description of the game mode")]
		[TextArea(3, 6)]
		public string FullDescription;
		
		[Header("Objectives")]
		[Tooltip("Primary objective description")]
		[TextArea(1, 3)]
		public string PrimaryObjective;
		
		[Tooltip("Secondary objectives")]
		public List<string> SecondaryObjectives = new();
		
		[Header("Rules")]
		[Tooltip("Game rules description")]
		[TextArea(2, 5)]
		public string Rules;
		
		[Tooltip("Win conditions")]
		public List<string> WinConditions = new();
		
		[Tooltip("Lose conditions")]
		public List<string> LoseConditions = new();
		
		[Header("Time Settings")]
		[Tooltip("Has time limit")]
		public bool HasTimeLimit = false;
		
		[Tooltip("Time limit in seconds (0 = unlimited)")]
		[Min(0)]
		public int TimeLimit = 0;
		
		[Tooltip("Time bonus per objective completed")]
		[Range(0, 60)]
		public int TimeBonusPerObjective = 0;
		
		[Tooltip("Time penalty per mistake")]
		[Range(0, 30)]
		public int TimePenaltyPerMistake = 0;
		
		[Header("Shot Settings")]
		[Tooltip("Has shot limit")]
		public bool HasShotLimit = false;
		
		[Tooltip("Maximum shots allowed (0 = unlimited)")]
		[Min(0)]
		public int MaxShots = 0;
		
		[Tooltip("Shots bonus per objective completed")]
		[Range(0, 10)]
		public int ShotsBonusPerObjective = 0;
		
		[Header("Lives Settings")]
		[Tooltip("Has lives")]
		public bool HasLives = false;
		
		[Tooltip("Number of lives")]
		[Range(1, 10)]
		public int Lives = 3;
		
		[Tooltip("Lives lost per mistake")]
		[Range(0, 3)]
		public int LivesLostPerMistake = 1;
		
		[Header("Scoring")]
		[Tooltip("Score multiplier")]
		[Range(0.5f, 3f)]
		public float ScoreMultiplier = 1f;
		
		[Tooltip("Combo multiplier")]
		[Range(1f, 5f)]
		public float ComboMultiplier = 1.5f;
		
		[Tooltip("Bonus score for completion")]
		[Min(0)]
		public int CompletionBonus = 0;
		
		[Header("Difficulty")]
		[Tooltip("Difficulty level (1-10)")]
		[Range(1, 10)]
		public int DifficultyLevel = 5;
		
		[Tooltip("Difficulty increases over time")]
		public bool DifficultyIncreases = false;
		
		[Tooltip("Difficulty increase rate")]
		[Range(0f, 1f)]
		public float DifficultyIncreaseRate = 0.1f;
		
		[Header("Progression")]
		[Tooltip("Has level progression")]
		public bool HasLevelProgression = false;
		
		[Tooltip("Starting level")]
		[Min(1)]
		public int StartingLevel = 1;
		
		[Tooltip("Maximum level (0 = unlimited)")]
		[Min(0)]
		public int MaxLevel = 0;
		
		[Tooltip("Auto-advance to next level on completion")]
		public bool AutoAdvanceLevel = true;
		
		[Header("Special Features")]
		[Tooltip("Enable special tiles")]
		public bool EnableSpecialTiles = true;
		
		[Tooltip("Enable powerups")]
		public bool EnablePowerups = true;
		
		[Tooltip("Enable boosters")]
		public bool EnableBoosters = true;
		
		[Tooltip("Enable enemies")]
		public bool EnableEnemies = false;
		
		[Tooltip("Enable obstacles")]
		public bool EnableObstacles = false;
		
		[Header("Multiplayer")]
		[Tooltip("Is multiplayer mode")]
		public bool IsMultiplayer = false;
		
		[Tooltip("Maximum players")]
		[Range(2, 10)]
		public int MaxPlayers = 2;
		
		[Tooltip("Is cooperative")]
		public bool IsCooperative = false;
		
		[Tooltip("Is competitive")]
		public bool IsCompetitive = false;
		
		[Header("Leaderboard")]
		[Tooltip("Has leaderboard")]
		public bool HasLeaderboard = false;
		
		[Tooltip("Leaderboard type")]
		public LeaderboardType LeaderboardType = LeaderboardType.Global;
		
		[Tooltip("Leaderboard reset frequency")]
		public LeaderboardResetFrequency LeaderboardResetFrequency = LeaderboardResetFrequency.Weekly;
		
		[Header("Rewards")]
		[Tooltip("Rewards for completing the mode")]
		public List<RewardDefinition> CompletionRewards = new();
		
		[Tooltip("Reward multiplier")]
		[Range(0.5f, 3f)]
		public float RewardMultiplier = 1f;
		
		[Tooltip("Bonus rewards for high scores")]
		public List<RewardDefinition> HighScoreRewards = new();
		
		[Header("Visual Settings")]
		[Tooltip("Icon for the game mode")]
		public Sprite Icon;
		
		[Tooltip("Background image")]
		public Sprite BackgroundImage;
		
		[Tooltip("Theme color")]
		public Color ThemeColor = Color.white;
		
		[Header("Analytics")]
		[Tooltip("Analytics event for mode start")]
		public UID StartEvent;
		
		[Tooltip("Analytics event for mode complete")]
		public UID CompleteEvent;
		
		[Tooltip("Analytics event for mode fail")]
		public UID FailEvent;
		
		public UID UniqueID => UID;
		
		/// <summary>
		/// Gets all rewards from this game mode definition.
		/// </summary>
		public List<RewardDefinition> GetAllRewards()
		{
			List<RewardDefinition> allRewards = new List<RewardDefinition>();
			
			foreach (var reward in CompletionRewards)
			{
				if (reward != null)
				{
					allRewards.Add(reward);
				}
			}
			
			foreach (var reward in HighScoreRewards)
			{
				if (reward != null)
				{
					allRewards.Add(reward);
				}
			}
			
			return allRewards;
		}
		
		/// <summary>
		/// Checks if this mode is available for a specific player level.
		/// </summary>
		public bool IsAvailableForPlayerLevel(int playerLevel)
		{
			if (!AlwaysAvailable) return false;
			if (!IsActive) return false;
			return playerLevel >= MinPlayerLevel;
		}
		
		/// <summary>
		/// Gets the adjusted difficulty for a specific level.
		/// </summary>
		public float GetAdjustedDifficulty(int level)
		{
			if (!DifficultyIncreases) return DifficultyLevel;
			
			float levelProgress = (float)(level - StartingLevel) / (MaxLevel - StartingLevel);
			return DifficultyLevel + (levelProgress * DifficultyIncreaseRate * 10f);
		}
	}
	
	/// <summary>
	/// Defines the type of leaderboard for a game mode.
	/// </summary>
	public enum LeaderboardType
	{
		/// <summary>
		/// Global leaderboard across all players.
		/// </summary>
		Global,
		
		/// <summary>
		/// Friends-only leaderboard.
		/// </summary>
		Friends,
		
		/// <summary>
		/// Regional leaderboard.
		/// </summary>
		Regional,
		
		/// <summary>
		/// Country-specific leaderboard.
		/// </summary>
		Country,
		
		/// <summary>
		/// Local device leaderboard.
		/// </summary>
		Local
	}
	
	/// <summary>
	/// Defines how often the leaderboard resets.
	/// </summary>
	public enum LeaderboardResetFrequency
	{
		/// <summary>
		/// Never resets.
		/// </summary>
		Never,
		
		/// <summary>
		/// Resets daily.
		/// </summary>
		Daily,
		
		/// <summary>
		/// Resets weekly.
		/// </summary>
		Weekly,
		
		/// <summary>
		/// Resets monthly.
		/// </summary>
		Monthly,
		
		/// <summary>
		/// Resets with each season.
		/// </summary>
		Seasonal
	}
}