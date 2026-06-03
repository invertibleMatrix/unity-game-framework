using AK.Core;
using UnityEngine;

namespace AK.Examples.Achievements
{
    /// <summary>
    /// Registry for managing achievement definitions using UID-based lookup
    /// </summary>
    [CreateAssetMenu(fileName = "AchievementsRegistry", menuName = "Examples/MetaData/Achievements/AchievementsRegistry")]
    public class AchievementsRegistry : TypedUIDRegistryAsset<AchievementDefinition> { }
}
