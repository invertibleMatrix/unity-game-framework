using AK.Core;
using AK.CoreDomain.Ads;
using UnityEngine;

namespace AK.CoreDomain
{
	/// <summary>
	/// Registry for all ad placement definitions.
	/// Similar to IAPRegistry but for ad placements.
	/// </summary>
	[CreateAssetMenu(fileName = "AdsRegistry", menuName = "AK/MetaData/Ads/AdsRegistry")]
	public class AdsRegistry : TypedUIDRegistryAsset<AdPlacementDefinition> { }
}