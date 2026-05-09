using AK.Core;
using GameplayCore.MetaData.Ads;
using UnityEngine;

namespace GameplayCore.MetaData
{
	/// <summary>
	/// Registry for all ad placement definitions.
	/// Similar to IAPRegistry but for ad placements.
	/// </summary>
	[CreateAssetMenu(fileName = "AdsRegistry", menuName = "Gameplay/MetaData/Ads/AdsRegistry")]
	public class AdsRegistry : TypedUIDRegistryAsset<AdPlacementDefinition> { }
}