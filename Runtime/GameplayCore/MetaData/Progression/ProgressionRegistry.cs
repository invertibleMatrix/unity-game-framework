using AK.Core;
using GameplayCore.MetaData.Progression;
using UnityEngine;

namespace GameplayCore.MetaData
{
	/// <summary>
	/// Registry for all progression level definitions.
	/// Similar to IAPRegistry but for progression levels.
	/// </summary>
	[CreateAssetMenu(fileName = "ProgressionRegistry", menuName = "Gameplay/MetaData/Progression/ProgressionRegistry")]
	public class ProgressionRegistry : TypedUIDRegistryAsset<ProgressionLevel> { }
}