using AK.Core;
using GameplayCore.MetaData.Rewards;
using UnityEngine;

namespace GameplayCore.MetaData
{
	[CreateAssetMenu(fileName = "RewardsRegistry", menuName = "Gameplay/MetaData/RewardsRegistry")]
	public class RewardsRegistry : TypedUIDRegistryAsset<RewardDefinition> { }
}