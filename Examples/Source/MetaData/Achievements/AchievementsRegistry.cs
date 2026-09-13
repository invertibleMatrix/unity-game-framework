using AK.Core;
using UnityEngine;

namespace AK.Examples.Achievements
{
    /// <summary>
    /// Registry of achievement definitions with identity-keyed lookup.
    /// </summary>
    [CreateAssetMenu(fileName = "AchievementsRegistry", menuName = "AK/Examples/MetaData/Achievements/AchievementsRegistry")]
    public class AchievementsRegistry : UidRegistryAsset<AchievementDefinition> { }
}
