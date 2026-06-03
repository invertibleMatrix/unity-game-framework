using AK.Core;
using AK.CoreDomain.Analytics;
using UnityEngine;

namespace AK.CoreDomain
{
	/// <summary>
	/// Registry for all analytics event definitions.
	/// Similar to IAPRegistry but for analytics events.
	/// </summary>
	[CreateAssetMenu(fileName = "AnalyticsRegistry", menuName = "Gameplay/MetaData/Analytics/AnalyticsRegistry")]
	public class AnalyticsRegistry : TypedUIDRegistryAsset<AnalyticsEventDefinition> { }
}