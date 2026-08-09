using System.Collections.Generic;
using System.Linq;
using AK.Core;
using UnityEngine;

namespace AK.Examples.Achievements
{
    /// <summary>
    /// Container for achievement definitions with query methods
    /// </summary>
    [CreateAssetMenu(fileName = "AchievementsMeta", menuName = "AK/Examples/MetaData/Achievements/AchievementsMeta")]
    public class AchievementsMeta : MetaDataAsset
    {
        [Header("Achievements")]
        [Tooltip("All achievement definitions")]
        public List<AchievementDefinition> Achievements;

        public AchievementDefinition GetAchievementByID(string achievementID)
        {
            return Achievements.FirstOrDefault(a => a.AchievementID == achievementID);
        }

        public AchievementDefinition GetAchievementByUID(UID uid)
        {
            return Achievements.FirstOrDefault(a => a.UniqueID == uid);
        }

        public List<AchievementDefinition> GetAchievementsByType(AchievementType type)
        {
            return Achievements.Where(a => a.Type == type).ToList();
        }

        public List<AchievementDefinition> GetAchievementsByRarity(AchievementRarity rarity)
        {
            return Achievements.Where(a => a.Rarity == rarity).ToList();
        }

        public List<AchievementDefinition> GetActiveAchievements()
        {
            return Achievements.Where(a => a.IsActive).ToList();
        }

        public List<AchievementDefinition> GetHiddenAchievements()
        {
            return Achievements.Where(a => a.IsHidden).ToList();
        }

        public List<AchievementDefinition> GetVisibleAchievements()
        {
            return Achievements.Where(a => !a.IsHidden).ToList();
        }

        public List<AchievementDefinition> GetRepeatableAchievements()
        {
            return Achievements.Where(a => a.IsRepeatable).ToList();
        }

        public List<AchievementDefinition> GetAvailableAchievements(int playerLevel, List<UID> completedAchievements)
        {
            return Achievements.Where(a => a.IsAvailable(playerLevel, completedAchievements)).ToList();
        }

        public List<AchievementDefinition> GetTimeLimitedAchievements()
        {
            return Achievements.Where(a => a.HasTimeLimit).ToList();
        }

        public List<AchievementDefinition> GetAchievementsForLevel(int level)
        {
            return Achievements.Where(a => a.MinimumLevel <= level).ToList();
        }

        public List<AchievementDefinition> GetAchievementsUnlockedAtLevel(int level)
        {
            return Achievements.Where(a => a.MinimumLevel == level).ToList();
        }

        public List<AchievementDefinition> GetAchievementsWithPrerequisites()
        {
            return Achievements.Where(a => a.PrerequisiteAchievements != null && a.PrerequisiteAchievements.Count > 0).ToList();
        }

        public List<AchievementDefinition> GetPrerequisiteAchievements(AchievementDefinition achievement)
        {
            List<AchievementDefinition> prerequisites = new List<AchievementDefinition>();
            if (achievement.PrerequisiteAchievements == null) return prerequisites;
            foreach (var uid in achievement.PrerequisiteAchievements)
            {
                var prereq = GetAchievementByUID(uid);
                if (prereq != null) prerequisites.Add(prereq);
            }
            return prerequisites;
        }

        public List<AchievementDefinition> GetDependentAchievements(AchievementDefinition achievement)
        {
            return Achievements.Where(a => a.PrerequisiteAchievements != null && a.PrerequisiteAchievements.Contains(achievement.UniqueID)).ToList();
        }

        public List<AchievementDefinition> GetAchievementsSortedByRarity()
        {
            return Achievements.OrderBy(a => a.Rarity).ToList();
        }

        public List<AchievementDefinition> GetAchievementsSortedByDifficulty()
        {
            return Achievements.OrderBy(a => a.TargetValue).ToList();
        }

        public List<AchievementDefinition> GetAchievementsWithMilestones()
        {
            return Achievements.Where(a => a.Milestones != null && a.Milestones.Count > 0).ToList();
        }

        public List<AchievementDefinition> GetAchievementsWithAnalytics()
        {
            return Achievements.Where(a => !string.IsNullOrEmpty(a.CompletionEventID)).ToList();
        }

        public int GetTotalAchievementCount()
        {
            return Achievements.Count;
        }

        public int GetAchievementCountByType(AchievementType type)
        {
            return Achievements.Count(a => a.Type == type);
        }

        public int GetAchievementCountByRarity(AchievementRarity rarity)
        {
            return Achievements.Count(a => a.Rarity == rarity);
        }

        public float GetCompletionPercentage(List<UID> completedAchievements)
        {
            if (Achievements.Count == 0) return 0f;
            int completedCount = Achievements.Count(a => completedAchievements.Contains(a.UniqueID));
            return (float)completedCount / Achievements.Count * 100f;
        }

        public float GetCompletionPercentageByType(AchievementType type, List<UID> completedAchievements)
        {
            var typeAchievements = GetAchievementsByType(type);
            if (typeAchievements.Count == 0) return 0f;
            int completedCount = typeAchievements.Count(a => completedAchievements.Contains(a.UniqueID));
            return (float)completedCount / typeAchievements.Count * 100f;
        }
    }
}
