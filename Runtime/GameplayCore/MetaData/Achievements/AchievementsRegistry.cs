using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.MetaData.Achievements
{
	/// <summary>
	/// Registry for managing achievement definitions using UID-based lookup
	/// </summary>
	[CreateAssetMenu(fileName = "AchievementsRegistry", menuName = "Gameplay/MetaData/Achievements/AchievementsRegistry")]
	public class AchievementsRegistry : TypedUIDRegistryAsset<AchievementDefinition>
	{
		// Inherits from TypedUIDRegistryAsset which provides:
		// - UID-based lookup
		// - Validation
		// - Registry management
	}
}