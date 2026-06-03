using AK.Core;
using AK.CoreDomain.Rewards;
using UnityEngine;

namespace AK.CoreDomain
{
	[CreateAssetMenu(fileName = "RewardsRegistry", menuName = "Gameplay/MetaData/RewardsRegistry")]
	public class RewardsRegistry : TypedUIDRegistryAsset<RewardDefinition> { }
}