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
		public RewardsRegistry Registry => _registry;

		public override void InitializeMeta()
		{
			_registry.Initialize();
		}
	}
}