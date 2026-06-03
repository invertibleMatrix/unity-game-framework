using AK.Core;
using UnityEngine;

namespace AK.Examples.Seasons
{
    /// <summary>
    /// ScriptableObject asset for categorizing seasonal events.
    /// Create instances per game (e.g., "Seasonal", "LimitedTime", "Community", "Custom").
    /// </summary>
    [CreateAssetMenu(fileName = "EventType", menuName = "AK/Examples/MetaData/Seasons/EventType")]
    public class EventType : MetaDataAsset { }
}
