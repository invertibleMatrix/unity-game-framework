using System;
using UnityEngine;

namespace GameplayCore.MetaData.Rewards
{
	[Serializable]
	public class CheckpointReward
	{
		public int              LevelNumber;
		public RewardDefinition RewardDefinition;
		public Sprite           Icon;
	}
}