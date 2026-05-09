using AK.Core;
using UnityEngine;

namespace GameplayCore.MetaData.DailyChallenges
{
	/// <summary>
	/// Registry for managing daily challenge definitions using UID-based lookup
	/// </summary>
	[CreateAssetMenu(fileName = "DailyChallengesRegistry", menuName = "Gameplay/MetaData/DailyChallenges/DailyChallengesRegistry")]
	public class DailyChallengesRegistry : TypedUIDRegistryAsset<DailyChallengeDefinition>
	{
		// Inherits from TypedUIDRegistryAsset which provides:
		// - UID-based lookup
		// - Validation
		// - Registry management
	}
}