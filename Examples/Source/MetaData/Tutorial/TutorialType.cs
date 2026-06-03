using AK.Core;
using UnityEngine;

namespace AK.Examples.Tutorial
{
    /// <summary>
    /// ScriptableObject asset for categorizing tutorials.
    /// Create instances per game (e.g., "Onboarding", "GameplayBasics", "Custom").
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialType", menuName = "AK/Examples/MetaData/Tutorial/TutorialType")]
    public class TutorialType : MetaDataAsset { }
}
