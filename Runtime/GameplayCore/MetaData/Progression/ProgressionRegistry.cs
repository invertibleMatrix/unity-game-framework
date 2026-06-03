using AK.Core;
using AK.CoreDomain.Progression;
using UnityEngine;

namespace AK.CoreDomain
{
	/// <summary>
	/// Registry for all progression level definitions.
	/// Similar to IAPRegistry but for progression levels.
	/// </summary>
	[CreateAssetMenu(fileName = "ProgressionRegistry", menuName = "Gameplay/MetaData/Progression/ProgressionRegistry")]
	public class ProgressionRegistry : TypedUIDRegistryAsset<ProgressionLevel> { }
}