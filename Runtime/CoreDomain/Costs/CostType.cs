using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Costs
{
	/// <summary>
	/// ScriptableObject asset representing a type of cost.
	/// Create asset instances for each cost type your game supports
	/// (e.g., "Free", "SoftCurrency", "HardCurrency", "Ad", "IAP", "Stamina").
	/// </summary>
	[CreateAssetMenu(fileName = "CostType", menuName = "Gameplay/MetaData/Costs/CostType")]
	public class CostType : MetaDataAsset
	{
		// Empty — identity is the UID. "Free", "Coin", "Gem", "Ad", "IAP"
		// are all just asset instances, not enum values.
	}
}
