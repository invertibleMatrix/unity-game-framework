using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using AK.Core;

namespace GameplayCore.MetaData.Achievements
{
	/// <summary>
	/// Container for achievement definitions with query methods
	/// </summary>
	[CreateAssetMenu(fileName = "AchievementsMeta", menuName = "Gameplay/MetaData/Achievements/AchievementsMeta")]
	public class AchievementsMeta : MetaDataAsset
	{
		[Header("Achievements")]
		[Tooltip("All achievement definitions")]
		public List<AchievementDefinition> Achievements;
		
		/// <summary>
		/// Get achievement by ID
		/// </summary>
		public AchievementDefinition GetAchievementByID(string achievementID)
		{
			return Achievements.FirstOrDefault(a => a.AchievementID == achievementID);
		}
		
		/// <summary>
		/// Get achievement by UID
		/// </summary>
		public AchievementDefinition GetAchievementByUID(UID uid)
		{
			return Achievements.FirstOrDefault(a => a.UID == uid);
		}
		
		/// <summary>
		/// Get all achievements of a specific type
		/// </summary>
		public List<AchievementDefinition> GetAchievementsByType(AchievementType type)
		{
			return Achievements.Where(a => a.Type == type).ToList();
		}
		
		/// <summary>
		/// Get all achievements of a specific rarity
		/// </summary>
		public List<AchievementDefinition> GetAchievementsByRarity(AchievementRarity rarity)
		{
			return Achievements.Where(a => a.Rarity == rarity).ToList();
		}
		
		/// <summary>
		/// Get all active achievements
		/// </summary>
		public List<AchievementDefinition> GetActiveAchievements()
		{
			return Achievements.Where(a => a.IsActive).ToList();
		}
		
		/// <summary>
		/// Get all hidden achievements
		/// </summary>
		public List<AchievementDefinition> GetHiddenAchievements()
		{
			return Achievements.Where(a => a.IsHidden).ToList();
		}
		
		/// <summary>
		/// Get all visible achievements
		/// </summary>
		public List<AchievementDefinition> GetVisibleAchievements()
		{
			return Achievements.Where(a => !a.IsHidden).ToList();
		}
		
		/// <summary>
		/// Get all repeatable achievements
		/// </summary>
		public List<AchievementDefinition> GetRepeatableAchievements()
		{
			return Achievements.Where(a => a.IsRepeatable).ToList();
		}
		
		/// <summary>
		/// Get achievements available to a player
		/// </summary>
		public List<AchievementDefinition> GetAvailableAchievements(int playerLevel, List<UID> completedAchievements)
		{
			return Achievements.Where(a => a.IsAvailable(playerLevel, completedAchievements)).ToList();
		}
		
		/// <summary>
		/// Get achievements with time limits
		/// </summary>
		public List<AchievementDefinition> GetTimeLimitedAchievements()
		{
			return Achievements.Where(a => a.HasTimeLimit).ToList();
		}
		
		/// <summary>
		/// Get achievements that require a minimum level
		/// </summary>
		public List<AchievementDefinition> GetAchievementsForLevel(int level)
		{
			return Achievements.Where(a => a.MinimumLevel <= level).ToList();
		}
		
		/// <summary>
		/// Get achievements that unlock at a specific level
		/// </summary>
		public List<AchievementDefinition> GetAchievementsUnlockedAtLevel(int level)
		{
			return Achievements.Where(a => a.MinimumLevel == level).ToList();
		}
		
		/// <summary>
		/// Get achievements that have prerequisites
		/// </summary>
		public List<AchievementDefinition> GetAchievementsWithPrerequisites()
		{
			return Achievements.Where(a => a.PrerequisiteAchievements != null && a.PrerequisiteAchievements.Count > 0).ToList();
		}
		
		/// <summary>
		/// Get achievements that are prerequisites for a given achievement
		/// </summary>
		public List<AchievementDefinition> GetPrerequisiteAchievements(AchievementDefinition achievement)
		{
			List<AchievementDefinition> prerequisites = new List<AchievementDefinition>();
			
			if (achievement.PrerequisiteAchievements == null) return prerequisites;
			
			foreach (var uid in achievement.PrerequisiteAchievements)
			{
				var prereq = GetAchievementByUID(uid);
				if (prereq != null)
				{
					prerequisites.Add(prereq);
				}
			}
			
			return prerequisites;
		}
		
		/// <summary>
		/// Get achievements that depend on a given achievement
		/// </summary>
		public List<AchievementDefinition> GetDependentAchievements(AchievementDefinition achievement)
		{
			return Achievements.Where(a => a.PrerequisiteAchievements != null && a.PrerequisiteAchievements.Contains(achievement.UID)).ToList();
		}
		
		/// <summary>
		/// Get achievements sorted by rarity (common to legendary)
		/// </summary>
		public List<AchievementDefinition> GetAchievementsSortedByRarity()
		{
			return Achievements.OrderBy(a => a.Rarity).ToList();
		}
		
		/// <summary>
		/// Get achievements sorted by target value (easiest to hardest)
		/// </summary>
		public List<AchievementDefinition> GetAchievementsSortedByDifficulty()
		{
			return Achievements.OrderBy(a => a.TargetValue).ToList();
		}
		
		/// <summary>
		/// Get achievements with milestones
		/// </summary>
		public List<AchievementDefinition> GetAchievementsWithMilestones()
		{
			return Achievements.Where(a => a.Milestones != null && a.Milestones.Count > 0).ToList();
		}
		
		/// <summary>
		/// Get achievements with analytics tracking
		/// </summary>
		public List<AchievementDefinition> GetAchievementsWithAnalytics()
		{
			return Achievements.Where(a => !string.IsNullOrEmpty(a.CompletionEventID)).ToList();
		}
		
		/// <summary>
		/// Get total number of achievements
		/// </summary>
		public int GetTotalAchievementCount()
		{
			return Achievements.Count;
		}
		
		/// <summary>
		/// Get number of achievements by type
		/// </summary>
		public int GetAchievementCountByType(AchievementType type)
		{
			return Achievements.Count(a => a.Type == type);
		}
		
		/// <summary>
		/// Get number of achievements by rarity
		/// </summary>
		public int GetAchievementCountByRarity(AchievementRarity rarity)
		{
			return Achievements.Count(a => a.Rarity == rarity);
		}
		
		/// <summary>
		/// Get achievement completion percentage for a player
		/// </summary>
		public float GetCompletionPercentage(List<UID> completedAchievements)
		{
			if (Achievements.Count == 0) return 0f;
			
			int completedCount = Achievements.Count(a => completedAchievements.Contains(a.UID));
			return (float)completedCount / Achievements.Count * 100f;
		}
		
		/// <summary>
		/// Get achievement completion percentage by type
		/// </summary>
		public float GetCompletionPercentageByType(AchievementType type, List<UID> completedAchievements)
		{
			var typeAchievements = GetAchievementsByType(type);
			if (typeAchievements.Count == 0) return 0f;
			
			int completedCount = typeAchievements.Count(a => completedAchievements.Contains(a.UID));
			return (float)completedCount / typeAchievements.Count * 100f;
		}
	}
}