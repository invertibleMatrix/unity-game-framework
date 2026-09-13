using AK.Core;
using AK.Examples.Rewards;
using UnityEngine;

namespace AK.CoreDomain
{
	[CreateAssetMenu(fileName = "RewardsMeta", menuName = "AK/MetaData/Rewards/RewardsMeta")]
	public class RewardsMeta : MetaDataAsset, IMetaWithRegistry
	{
		[SerializeField] private RewardsRegistry _registry;
		public RewardsRegistry Registry => _registry;

		public UidRegistryAssetBase RegistryAsset => _registry;

		public override void InitializeMeta() { }

		public bool TryGetReward(Uid<RewardDefinition> id, out RewardDefinition reward)
		{
			reward = null;
			return _registry != null && _registry.TryResolve(id, out reward);
		}
	}
}
