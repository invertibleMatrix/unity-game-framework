using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Seasons
{
	/// <summary>
	/// Registry for managing event definitions using UID-based lookup
	/// </summary>
	[CreateAssetMenu(fileName = "SeasonsRegistry", menuName = "Gameplay/MetaData/Seasons/SeasonsRegistry")]
	public class SeasonsRegistry : TypedUIDRegistryAsset<EventDefinition>
	{
		// Inherits from TypedUIDRegistryAsset which provides:
		// - UID-based lookup
		// - Validation
		// - Registry management
	}
}