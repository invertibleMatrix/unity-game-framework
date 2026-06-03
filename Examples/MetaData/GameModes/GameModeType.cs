using AK.Core;
using UnityEngine;

namespace AK.Examples.GameModes
{
    /// <summary>
    /// ScriptableObject asset for categorizing game modes.
    /// Create instances per game (e.g., "Campaign", "Endless", "TimeAttack", "Custom").
    /// </summary>
    [CreateAssetMenu(fileName = "GameModeType", menuName = "Examples/MetaData/GameModes/GameModeType")]
    public class GameModeType : MetaDataAsset { }
}
