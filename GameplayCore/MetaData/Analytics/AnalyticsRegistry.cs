using AK.Core;
using GameplayCore.MetaData.Analytics;
using UnityEngine;

namespace GameplayCore.MetaData
{
	/// <summary>
	/// Registry for all analytics event definitions.
	/// Similar to IAPRegistry but for analytics events.
	/// </summary>
	[CreateAssetMenu(fileName = "AnalyticsRegistry", menuName = "Gameplay/MetaData/Analytics/AnalyticsRegistry")]
	public class AnalyticsRegistry : TypedUIDRegistryAsset<AnalyticsEventDefinition> { }
}