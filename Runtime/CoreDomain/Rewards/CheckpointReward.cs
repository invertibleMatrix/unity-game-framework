using System;
using UnityEngine;

namespace AK.CoreDomain.Rewards
{
	[Serializable]
	public class CheckpointReward
	{
		public int              LevelNumber;
		public RewardDefinition RewardDefinition;
		public Sprite           Icon;
	}
}