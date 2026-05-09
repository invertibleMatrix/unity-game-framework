using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.MetaData.DailyChallenges
{
	/// <summary>
	/// Container for daily challenge definitions with query methods
	/// </summary>
	[CreateAssetMenu(fileName = "DailyChallengesMeta", menuName = "Gameplay/MetaData/DailyChallenges/DailyChallengesMeta")]
	public class DailyChallengesMeta : MetaDataAsset
	{
		[Header("Daily Challenges")]
		[Tooltip("All daily challenge definitions")]
		public List<DailyChallengeDefinition> Challenges;
		
		/// <summary>
		/// Get challenge by ID
		/// </summary>
		public DailyChallengeDefinition GetChallengeByID(string challengeID)
		{
			return Challenges.FirstOrDefault(c => c.ChallengeID == challengeID);
		}
		
		/// <summary>
		/// Get challenge by UID
		/// </summary>
		public DailyChallengeDefinition GetChallengeByUID(UID uid)
		{
			return Challenges.FirstOrDefault(c => c.UID == uid);
		}
		
		/// <summary>
		/// Get all challenges of a specific type
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesByType(ChallengeType type)
		{
			return Challenges.Where(c => c.Type == type).ToList();
		}
		
		/// <summary>
		/// Get all challenges of a specific difficulty
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesByDifficulty(ChallengeDifficulty difficulty)
		{
			return Challenges.Where(c => c.Difficulty == difficulty).ToList();
		}
		
		/// <summary>
		/// Get all active challenges
		/// </summary>
		public List<DailyChallengeDefinition> GetActiveChallenges()
		{
			return Challenges.Where(c => c.IsActive).ToList();
		}
		
		/// <summary>
		/// Get challenges available for a specific day
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesForDay(int dayOfWeek)
		{
			return Challenges.Where(c => c.AvailableDays == null || c.AvailableDays.Count == 0 || c.AvailableDays.Contains(dayOfWeek)).ToList();
		}
		
		/// <summary>
		/// Get challenges available to a player
		/// </summary>
		public List<DailyChallengeDefinition> GetAvailableChallenges(int playerLevel, int currentDayOfWeek)
		{
			return Challenges.Where(c => c.IsAvailable(playerLevel, currentDayOfWeek)).ToList();
		}
		
		/// <summary>
		/// Get challenges with time limits
		/// </summary>
		public List<DailyChallengeDefinition> GetTimeLimitedChallenges()
		{
			return Challenges.Where(c => c.HasTimeLimit).ToList();
		}
		
		/// <summary>
		/// Get recurring challenges
		/// </summary>
		public List<DailyChallengeDefinition> GetRecurringChallenges()
		{
			return Challenges.Where(c => c.IsRecurring).ToList();
		}
		
		/// <summary>
		/// Get challenges for a specific level range
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesForLevelRange(int minLevel, int maxLevel)
		{
			return Challenges.Where(c => c.MinimumLevel >= minLevel && (c.MaximumLevel == 0 || c.MaximumLevel <= maxLevel)).ToList();
		}
		
		/// <summary>
		/// Get challenges that require a specific powerup
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesForPowerup(UID powerupUID)
		{
			return Challenges.Where(c => c.RequiredPowerupUID == powerupUID).ToList();
		}
		
		/// <summary>
		/// Get challenges that require a specific theme
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesForTheme(UID themeUID)
		{
			return Challenges.Where(c => c.RequiredThemeUID == themeUID).ToList();
		}
		
		/// <summary>
		/// Get challenges that require a specific booster
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesForBooster(UID boosterUID)
		{
			return Challenges.Where(c => c.RequiredBoosterUID == boosterUID).ToList();
		}
		
		/// <summary>
		/// Get challenges sorted by difficulty (easy to hard)
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesSortedByDifficulty()
		{
			return Challenges.OrderBy(c => c.Difficulty).ToList();
		}
		
		/// <summary>
		/// Get challenges sorted by target value (easiest to hardest)
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesSortedByTargetValue()
		{
			return Challenges.OrderBy(c => c.TargetValue).ToList();
		}
		
		/// <summary>
		/// Get challenges with milestones
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesWithMilestones()
		{
			return Challenges.Where(c => c.Milestones != null && c.Milestones.Count > 0).ToList();
		}
		
		/// <summary>
		/// Get challenges with early completion bonuses
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesWithEarlyCompletionBonus()
		{
			return Challenges.Where(c => c.EarlyCompletionBonus != null && c.EarlyCompletionBonus.Count > 0).ToList();
		}
		
		/// <summary>
		/// Get challenges with analytics tracking
		/// </summary>
		public List<DailyChallengeDefinition> GetChallengesWithAnalytics()
		{
			return Challenges.Where(c => !string.IsNullOrEmpty(c.CompletionEventID)).ToList();
		}
		
		/// <summary>
		/// Get total number of challenges
		/// </summary>
		public int GetTotalChallengeCount()
		{
			return Challenges.Count;
		}
		
		/// <summary>
		/// Get number of challenges by type
		/// </summary>
		public int GetChallengeCountByType(ChallengeType type)
		{
			return Challenges.Count(c => c.Type == type);
		}
		
		/// <summary>
		/// Get number of challenges by difficulty
		/// </summary>
		public int GetChallengeCountByDifficulty(ChallengeDifficulty difficulty)
		{
			return Challenges.Count(c => c.Difficulty == difficulty);
		}
		
		/// <summary>
		/// Get challenge completion percentage for a player
		/// </summary>
		public float GetCompletionPercentage(List<UID> completedChallenges)
		{
			if (Challenges.Count == 0) return 0f;
			
			int completedCount = Challenges.Count(c => completedChallenges.Contains(c.UID));
			return (float)completedCount / Challenges.Count * 100f;
		}
		
		/// <summary>
		/// Get challenge completion percentage by type
		/// </summary>
		public float GetCompletionPercentageByType(ChallengeType type, List<UID> completedChallenges)
		{
			var typeChallenges = GetChallengesByType(type);
			if (typeChallenges.Count == 0) return 0f;
			
			int completedCount = typeChallenges.Count(c => completedChallenges.Contains(c.UID));
			return (float)completedCount / typeChallenges.Count * 100f;
		}
		
		/// <summary>
		/// Get random challenges for the day
		/// </summary>
		public List<DailyChallengeDefinition> GetRandomDailyChallenges(int count, int playerLevel, int currentDayOfWeek, System.Random random = null)
		{
			var availableChallenges = GetAvailableChallenges(playerLevel, currentDayOfWeek);
			if (availableChallenges.Count == 0) return new List<DailyChallengeDefinition>();
			
			random = random ?? new System.Random();
			int actualCount = Mathf.Min(count, availableChallenges.Count);
			
			return availableChallenges.OrderBy(x => random.Next()).Take(actualCount).ToList();
		}
		
		/// <summary>
		/// Get balanced daily challenges (mix of difficulties)
		/// </summary>
		public List<DailyChallengeDefinition> GetBalancedDailyChallenges(int count, int playerLevel, int currentDayOfWeek, System.Random random = null)
		{
			var availableChallenges = GetAvailableChallenges(playerLevel, currentDayOfWeek);
			if (availableChallenges.Count == 0) return new List<DailyChallengeDefinition>();
			
			random = random ?? new System.Random();
			List<DailyChallengeDefinition> selectedChallenges = new List<DailyChallengeDefinition>();
			
			// Try to get a balanced mix of difficulties
			var difficulties = new[] { ChallengeDifficulty.Easy, ChallengeDifficulty.Medium, ChallengeDifficulty.Hard };
			int perDifficulty = Mathf.CeilToInt((float)count / difficulties.Length);
			
			foreach (var difficulty in difficulties)
			{
				var difficultyChallenges = availableChallenges.Where(c => c.Difficulty == difficulty).ToList();
				if (difficultyChallenges.Count > 0)
				{
					int takeCount = Mathf.Min(perDifficulty, difficultyChallenges.Count);
					selectedChallenges.AddRange(difficultyChallenges.OrderBy(x => random.Next()).Take(takeCount));
				}
			}
			
			// Fill remaining slots with any available challenges
			while (selectedChallenges.Count < count && selectedChallenges.Count < availableChallenges.Count)
			{
				var remaining = availableChallenges.Except(selectedChallenges).ToList();
				if (remaining.Count == 0) break;
				selectedChallenges.Add(remaining[random.Next(remaining.Count)]);
			}
			
			return selectedChallenges.Take(count).ToList();
		}
	}
}