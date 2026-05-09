using System.Collections.Generic;
using System.Linq;
using AK.Core;
using GameplayCore.MetaData.Progression;
using UnityEngine;

namespace GameplayCore.MetaData
{
	/// <summary>
	/// Container for all progression level and milestone definitions with powerful query methods.
	/// Similar to IAPMeta but for progression.
	/// </summary>
	[CreateAssetMenu(fileName = "ProgressionMeta", menuName = "Gameplay/MetaData/Progression/ProgressionMeta")]
	public class ProgressionMeta : MetaDataAsset
	{
		[Header("Progression Levels")]
		[Tooltip("All player level definitions.")]
		public List<ProgressionLevel> Levels;

		[Header("Milestones")]
		[Tooltip("All milestone definitions.")]
		public List<MilestoneDefinition> Milestones;

		[Header("Prestige")]
		[Tooltip("Maximum player level (0 = unlimited).")]
		public int MaxLevel = 0;

		[Tooltip("Prestige level cap (0 = no prestige).")]
		public int MaxPrestigeLevel = 0;

		/// <summary>
		/// Gets a level by its level number.
		/// </summary>
		public ProgressionLevel GetLevel(int levelNumber)
		{
			return Levels.FirstOrDefault(l => l.Level == levelNumber);
		}

		/// <summary>
		/// Gets the level for a given amount of XP.
		/// </summary>
		public ProgressionLevel GetLevelForXP(long playerXP)
		{
			// Find the highest level that can be reached with the given XP
			return Levels.Where(l => l.RequiredXP <= playerXP)
				.OrderByDescending(l => l.Level)
				.FirstOrDefault();
		}

		/// <summary>
		/// Gets all levels sorted by level number.
		/// </summary>
		public List<ProgressionLevel> GetLevelsSorted()
		{
			return Levels.OrderBy(l => l.Level).ToList();
		}

		/// <summary>
		/// Gets all levels that can be prestiged.
		/// </summary>
		public List<ProgressionLevel> GetPrestigeableLevels()
		{
			return Levels.Where(l => l.CanPrestige).ToList();
		}

			/// <summary>
		/// Gets a milestone by its MilestoneID.
		/// </summary>
		public MilestoneDefinition GetMilestoneByID(string milestoneID)
		{
			if (string.IsNullOrEmpty(milestoneID))
			{
				return null;
			}

			return Milestones.FirstOrDefault(m => m.MilestoneID == milestoneID);
		}

		/// <summary>
		/// Gets all milestones for a specific level.
		/// </summary>
		public List<MilestoneDefinition> GetMilestonesForLevel(int levelNumber)
		{
			return Milestones.Where(m => m.RequiredLevel == levelNumber).ToList();
			}

		/// <summary>
		/// Gets all milestones that have been reached.
		/// </summary>
		public List<MilestoneDefinition> GetReachedMilestones(int playerLevel, long playerXP)
		{
			return Milestones.Where(m => m.IsReached(playerLevel, playerXP)).ToList();
		}

		/// <summary>
		/// Gets all milestones sorted by required level.
		/// </summary>
		public List<MilestoneDefinition> GetMilestonesSorted()
		{
			return Milestones.OrderBy(m => m.RequiredLevel).ToList();
		}

		/// <summary>
		/// Gets all milestones sorted by display priority.
		/// </summary>
		public List<MilestoneDefinition> GetMilestonesByPriority()
		{
			return Milestones.OrderBy(m => m.DisplayPriority).ToList();
		}

		/// <summary>
		/// Gets all milestones that are not hidden.
		/// </summary>
		public List<MilestoneDefinition> GetVisibleMilestones()
		{
			return Milestones.Where(m => !m.IsHidden).ToList();
		}

		/// <summary>
		/// Checks if a level exists.
		/// </summary>
		public bool HasLevel(int levelNumber)
		{
			return Levels.Any(l => l.Level == levelNumber);
		}

		/// <summary>
		/// Checks if a milestone exists by MilestoneID.
		/// </summary>
		public bool HasMilestone(string milestoneID)
		{
			return Milestones.Any(m => m.MilestoneID == milestoneID);
		}

		/// <summary>
		/// Gets the maximum level that can be reached.
		/// </summary>
		public int GetMaxLevel()
		{
			if (MaxLevel > 0) return MaxLevel;
			return Levels.Count > 0 ? Levels.Max(l => l.Level) : 1;
		}

		/// <summary>
		/// Gets the total XP required to reach the maximum level.
		/// </summary>
		public long GetMaxLevelXP()
		{
			var maxLevel = GetMaxLevel();
			var level = GetLevel(maxLevel);
			return level != null ? level.RequiredXP : 0;
		}

		/// <summary>
		/// Calculates the player level based on XP.
		/// </summary>
		public int CalculatePlayerLevel(long playerXP)
		{
			var level = GetLevelForXP(playerXP);
			return level != null ? level.Level : 1;
		}

		/// <summary>
		/// Calculates the XP progress towards the next level.
		/// </summary>
		public float GetXPProgress(long playerXP)
		{
			var currentLevel = CalculatePlayerLevel(playerXP);
			var levelData = GetLevel(currentLevel);
			
			if (levelData == null) return 0f;

			var levelStartXP = levelData.RequiredXP;
			var levelEndXP = levelData.RequiredXP + levelData.XPToNextLevel;
			
			if (levelEndXP <= levelStartXP) return 1f;
			
			return (float)(playerXP - levelStartXP) / (levelEndXP - levelStartXP);
		}

		/// <summary>
		/// Gets the XP required to reach the next level.
		/// </summary>
		public long GetXPToNextLevel(long playerXP)
		{
			var currentLevel = CalculatePlayerLevel(playerXP);
			var levelData = GetLevel(currentLevel);
			
			if (levelData == null) return 0;
			
			return levelData.RequiredXP + levelData.XPToNextLevel - playerXP;
		}
	}
}