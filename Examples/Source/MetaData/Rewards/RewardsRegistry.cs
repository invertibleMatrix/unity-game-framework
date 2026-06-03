using AK.Core;
using AK.Examples.Rewards;
using UnityEngine;

namespace AK.CoreDomain
{
	[CreateAssetMenu(fileName = "RewardsRegistry", menuName = "AK/MetaData/RewardsRegistry")]
	public class RewardsRegistry : TypedUIDRegistryAsset<RewardDefinition> { }
}