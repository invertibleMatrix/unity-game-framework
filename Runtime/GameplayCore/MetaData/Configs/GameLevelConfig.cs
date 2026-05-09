using GameplayCore.MetaData.Rewards;
using UnityEngine;

namespace GameplayCore.MetaData
{
	[CreateAssetMenu(fileName = "GameLevelConfig", menuName = "Gameplay/Configs/GameLevelConfig")]
	public class GameLevelConfig : ScriptableObject
	{
		public float RotationTorque        = 2;
		public float Sign                  = 1f;
		public float MinBulletCooldownTime = 1f;

		public float WallPunishmentDistanceOffset = 0f;
		
		// Must Pop percentage of tiles in order to earn stars
		public float TwoStarsRatio   = 0.7f;
		public float ThreeStarsRatio = 0.999999999f;
	}
}