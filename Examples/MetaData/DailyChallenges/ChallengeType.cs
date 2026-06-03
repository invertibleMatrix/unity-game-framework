using AK.Core;
using UnityEngine;

namespace AK.Examples.DailyChallenges
{
    /// <summary>
    /// ScriptableObject asset for categorizing daily challenges.
    /// Create instances per game (e.g., "LevelComplete", "ScoreAchieve", "Custom").
    /// </summary>
    [CreateAssetMenu(fileName = "ChallengeType", menuName = "Examples/MetaData/DailyChallenges/ChallengeType")]
    public class ChallengeType : MetaDataAsset { }
}
