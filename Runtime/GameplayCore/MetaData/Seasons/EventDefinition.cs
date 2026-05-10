using System;
using System.Collections.Generic;
using AK.Core;
using GameplayCore.MetaData.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.MetaData.Seasons
{
	/// <summary>
	/// Definition for a seasonal or special event with time limits, rewards, and progression
	/// </summary>
	[CreateAssetMenu(fileName = "EventDefinition", menuName = "Gameplay/MetaData/Seasons/EventDefinition")]
	public class EventDefinition : MetaDataAsset
	{
		[Header("Identification")]
		[Tooltip("Unique string ID for this event")]
		public string EventID;
		
		[Header("Display Information")]
		[Tooltip("Display name shown to players")]
		public string DisplayName;
		
		[Tooltip("Description shown to players")]
		[TextArea]
		public string Description;
		
		[Tooltip("Icon displayed in UI")]
		public Sprite Icon;
		
		[Tooltip("Banner image displayed in UI")]
		public Sprite BannerImage;
		
		[Header("Event Type")]
		[Tooltip("Type of event")]
		public EventType Type;
		
		[Header("Time Schedule")]
		[Tooltip("Start time (Unix timestamp)")]
		public long StartTime;
		
		[Tooltip("End time (Unix timestamp)")]
		public long EndTime;
		
		[Tooltip("Is this event currently active?")]
		public bool IsActive;
		
		[Header("Requirements")]
		[Tooltip("Minimum level required to participate")]
		public int MinimumLevel;
		
		[Tooltip("Maximum level for this event (0 = no limit)")]
		public int MaximumLevel;
		
		[Tooltip("Required achievements to participate")]
		public List<UID> RequiredAchievements;
		
		[Header("Progression")]
		[Tooltip("Event progression levels")]
		public List<EventLevel> Levels;
		
		[Tooltip("Current event level")]
		[ReadOnly]
		public int CurrentLevel;
		
		[Tooltip("Current event XP")]
		[ReadOnly]
		public int CurrentXP;
		
		[Header("Rewards")]
		[Tooltip("Rewards granted on event completion")]
		public List<RewardDefinition> CompletionRewards;
		
		[Tooltip("Rewards granted for reaching each level")]
		public List<EventLevelReward> LevelRewards;
		
		[Tooltip("Bonus rewards for early completion")]
		public List<RewardDefinition> EarlyCompletionBonus;
		
		[Header("Special Features")]
		[Tooltip("Special shop items available during event")]
		public List<UID> SpecialShopItems;
		
		[Tooltip("Special challenges available during event")]
		public List<UID> SpecialChallenges;
		
		[Tooltip("Special achievements available during event")]
		public List<UID> SpecialAchievements;
		
		[Header("Leaderboard")]
		[Tooltip("Does this event have a leaderboard?")]
		public bool HasLeaderboard;
		
		[ShowIf("HasLeaderboard")]
		[Tooltip("Leaderboard scoring type")]
		public LeaderboardType LeaderboardType;
		
		[ShowIf("HasLeaderboard")]
		[Tooltip("Leaderboard reset interval")]
		public LeaderboardResetInterval LeaderboardResetInterval;
		
		[Header("Analytics")]
		[Tooltip("Analytics event to track when event starts")]
		public string StartEventID;
		
		[Tooltip("Analytics event to track when event ends")]
		public string EndEventID;
		
		[Tooltip("Analytics event to track when event is completed")]
		public string CompletionEventID;
		
		[Header("Additional Data")]
		[Tooltip("Additional data for custom event types")]
		public Dictionary<string, string> CustomData;
		
		public virtual UID UniqueID => this;
		
		/// <summary>
		/// Check if the event is currently active
		/// </summary>
		public bool IsCurrentlyActive()
		{
			if (!IsActive) return false;
			
			long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			return currentTime >= StartTime && currentTime <= EndTime;
		}
		
		/// <summary>
		/// Check if the event is available to the player
		/// </summary>
		public bool IsAvailableToPlayer(int playerLevel, List<UID> completedAchievements)
		{
			if (!IsCurrentlyActive()) return false;
			if (playerLevel < MinimumLevel) return false;
			if (MaximumLevel > 0 && playerLevel > MaximumLevel) return false;
			
			foreach (var achievementUID in RequiredAchievements)
			{
				if (!completedAchievements.Contains(achievementUID))
				{
					return false;
				}
			}
			
			return true;
		}
		
		/// <summary>
		/// Get the current event level
		/// </summary>
		public EventLevel GetCurrentEventLevel()
		{
			foreach (var level in Levels)
			{
				if (CurrentXP >= level.RequiredXP)
				{
					return level;
				}
			}
			return Levels[0];
		}
		
		/// <summary>
		/// Get the next event level
		/// </summary>
		public EventLevel GetNextEventLevel()
		{
			for (int i = 0; i < Levels.Count; i++)
			{
				if (CurrentXP < Levels[i].RequiredXP)
				{
					return Levels[i];
				}
			}
			return null;
		}
		
		/// <summary>
		/// Add XP to the event
		/// </summary>
		public void AddXP(int amount)
		{
			CurrentXP += amount;
			
			// Check for level up
			var nextLevel = GetNextEventLevel();
			if (nextLevel != null && CurrentXP >= nextLevel.RequiredXP)
			{
				CurrentLevel++;
			}
		}
		
		/// <summary>
		/// Check if the event is completed
		/// </summary>
		public bool IsCompleted()
		{
			if (Levels.Count == 0) return false;
			return CurrentXP >= Levels[Levels.Count - 1].RequiredXP;
		}
		
		/// <summary>
		/// Get completion percentage (0-100)
		/// </summary>
		public float GetCompletionPercentage()
		{
			if (Levels.Count == 0) return 0f;
			
			int maxXP = Levels[Levels.Count - 1].RequiredXP;
			if (maxXP == 0) return 0f;
			
			return (float)CurrentXP / maxXP * 100f;
		}
		
		/// <summary>
		/// Get time remaining in seconds
		/// </summary>
		public long GetTimeRemaining()
		{
			long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			return EndTime - currentTime;
		}
		
		/// <summary>
		/// Get time elapsed in seconds
		/// </summary>
		public long GetTimeElapsed()
		{
			long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			return currentTime - StartTime;
		}
		
		/// <summary>
		/// Get total duration in seconds
		/// </summary>
		public long GetTotalDuration()
		{
			return EndTime - StartTime;
		}
	}
	
	/// <summary>
	/// Event level with XP requirements and rewards
	/// </summary>
	[Serializable]
	public class EventLevel
	{
		[Tooltip("Level number")]
		public int LevelNumber;
		
		[Tooltip("XP required to reach this level")]
		public int RequiredXP;
		
		[Tooltip("Rewards granted at this level")]
		public List<RewardDefinition> Rewards;
		
		[Tooltip("Unlocks at this level")]
		public List<UID> Unlocks;
	}
	
	/// <summary>
	/// Reward granted for reaching a specific event level
	/// </summary>
	[Serializable]
	public class EventLevelReward
	{
		[Tooltip("Event level")]
		public int LevelNumber;
		
		[Tooltip("Rewards granted")]
		public List<RewardDefinition> Rewards;
	}
	
	/// <summary>
	/// Types of leaderboards
	/// </summary>
	public enum LeaderboardType
	{
		Score,
		XP,
		Level,
		Custom
	}
	
	/// <summary>
	/// Leaderboard reset intervals
	/// </summary>
	public enum LeaderboardResetInterval
	{
		Never,
		Daily,
		Weekly,
		Monthly,
		EventEnd
	}
}