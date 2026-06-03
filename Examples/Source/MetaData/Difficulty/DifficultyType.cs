using AK.Core;
using UnityEngine;

namespace AK.Examples.Difficulty
{
    /// <summary>
    /// ScriptableObject asset for categorizing difficulty levels.
    /// Create instances per game (e.g., "Easy", "Normal", "Hard", "Custom").
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyType", menuName = "AK/Examples/MetaData/Difficulty/DifficultyType")]
    public class DifficultyType : MetaDataAsset { }
}
