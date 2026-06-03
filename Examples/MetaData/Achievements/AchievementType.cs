using AK.Core;
using UnityEngine;

namespace AK.Examples.Achievements
{
    /// <summary>
    /// ScriptableObject asset for categorizing achievements.
    /// Create instances per game (e.g., "LevelBased", "Accumulation", "Streak", "Custom").
    /// </summary>
    [CreateAssetMenu(fileName = "AchievementType", menuName = "Examples/MetaData/Achievements/AchievementType")]
    public class AchievementType : MetaDataAsset { }
}
