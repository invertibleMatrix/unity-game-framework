using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain
{
	[CreateAssetMenu(fileName = "RewardsMeta", menuName = "AK/MetaData/Rewards/RewardsMeta")]
	public class RewardsMeta : MetaDataAsset, IMeta
	{
		[SerializeField] private RewardsRegistry _registry;

		[InlineEditor(), SerializeField]
		private List<RewardDefinition> _starRewards;
		
		[SerializeField] private List<CheckpointReward> _checkpointRewards;

		[SerializeField] private GachaBundle _rbBoosterGachaBundle;
		
		public RewardsRegistry                 Registry          => _registry;
		public IReadOnlyList<RewardDefinition> StarRewards       => _starRewards;
		public IReadOnlyList<CheckpointReward> CheckpointRewards => _checkpointRewards;

		public override void InitializeMeta()
		{
			_registry.Initialize();
		}
	}
}