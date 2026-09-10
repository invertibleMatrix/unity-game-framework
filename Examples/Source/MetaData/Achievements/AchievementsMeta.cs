using System.Collections.Generic;
using System.Linq;
using AK.Core;
using UnityEngine;

namespace AK.Examples.Achievements
{
    /// <summary>
    /// Container for achievement definitions with query methods. Definitions live in the
    /// registry so identity lookups (prerequisites, persisted completions) resolve in O(1).
    /// </summary>
    [CreateAssetMenu(fileName = "AchievementsMeta", menuName = "AK/Examples/MetaData/Achievements/AchievementsMeta")]
    public class AchievementsMeta : MetaDataAsset, IMetaWithRegistry
    {
        [SerializeField] private AchievementsRegistry _registry;

        public AchievementsRegistry Registry => _registry;

        public UidRegistryAssetBase RegistryAsset => _registry;

        public IReadOnlyList<AchievementDefinition> Achievements =>
            _registry != null ? _registry.Objects : System.Array.Empty<AchievementDefinition>();

        public override void InitializeMeta() { }

        public AchievementDefinition GetAchievementByID(string achievementID)
        {
            return Achievements.FirstOrDefault(a => a.AchievementID == achievementID);
        }

        public bool TryGetAchievement(Uid<AchievementDefinition> id, out AchievementDefinition achievement)
        {
            achievement = null;
            return _registry != null && _registry.TryResolve(id, out achievement);
        }

        public AchievementDefinition GetAchievement(Uid<AchievementDefinition> id)
        {
            return TryGetAchievement(id, out var achievement) ? achievement : null;
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

        public List<AchievementDefinition> GetAvailableAchievements(int playerLevel, IReadOnlyCollection<Uid<AchievementDefinition>> completedAchievements)
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
            var prerequisites = new List<AchievementDefinition>();
            if (achievement == null || achievement.PrerequisiteAchievements == null) return prerequisites;

            foreach (var id in achievement.PrerequisiteAchievements)
            {
                if (TryGetAchievement(id, out var prereq)) prerequisites.Add(prereq);
            }

            return prerequisites;
        }

        public List<AchievementDefinition> GetDependentAchievements(AchievementDefinition achievement)
        {
            if (achievement == null) return new List<AchievementDefinition>();

            var id = achievement.AchievementId;
            return Achievements.Where(a => a.PrerequisiteAchievements != null && a.PrerequisiteAchievements.Contains(id)).ToList();
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
            return Achievements.Where(a => a.CompletionEvent != null).ToList();
        }

        public int GetTotalAchievementCount() => Achievements.Count;

        public int GetAchievementCountByType(AchievementType type) => Achievements.Count(a => a.Type == type);

        public int GetAchievementCountByRarity(AchievementRarity rarity) => Achievements.Count(a => a.Rarity == rarity);

        public float GetCompletionPercentage(IReadOnlyCollection<Uid<AchievementDefinition>> completedAchievements)
        {
            if (Achievements.Count == 0 || completedAchievements == null) return 0f;
            int completedCount = Achievements.Count(a => completedAchievements.Contains(a.AchievementId));
            return (float)completedCount / Achievements.Count * 100f;
        }

        public float GetCompletionPercentageByType(AchievementType type, IReadOnlyCollection<Uid<AchievementDefinition>> completedAchievements)
        {
            var typeAchievements = GetAchievementsByType(type);
            if (typeAchievements.Count == 0 || completedAchievements == null) return 0f;
            int completedCount = typeAchievements.Count(a => completedAchievements.Contains(a.AchievementId));
            return (float)completedCount / typeAchievements.Count * 100f;
        }
    }
}
