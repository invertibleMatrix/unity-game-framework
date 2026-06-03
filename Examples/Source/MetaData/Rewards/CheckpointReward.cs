using System;
using UnityEngine;

namespace AK.Examples.Rewards
{
	[Serializable]
	public class CheckpointReward
	{
		public int              LevelNumber;
		public RewardDefinition RewardDefinition;
		public Sprite           Icon;
	}
}