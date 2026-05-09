using System.Collections.Generic;
using AK.Core;
using GameplayCore.MetaData.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.MetaData
{
	[CreateAssetMenu(fileName = "RewardsMeta", menuName = "Gameplay/MetaData/Rewards/RewardsMeta")]
	public class RewardsMeta : MetaDataAsset
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