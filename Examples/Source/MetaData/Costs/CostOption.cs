using AK.Core;
using AK.CoreDomain;
using UnityEngine;

namespace AK.Examples.Costs
{
	[System.Serializable]
	public class CostOption : ICostInfo
	{
		[Tooltip("The type of this cost. References a CostType ScriptableObject asset.")]
		public CostType Type;

		[Tooltip("The amount for this cost (e.g., 100 coins, 5 gems, 10 stamina).")]
		public int Amount;

		[Tooltip("UID of the specific resource definition (e.g., CurrencyDefinition UID, AdPlacement UID, IAP product UID).")]
		public UID CostTypeUID;

		[Tooltip("The ID of the resource to be consumed if the type is Resource.")]
		public UID ResourceID;

		// ICostInfo explicit implementation
		UID ICostInfo.CostTypeUID => Type;
		int ICostInfo.Amount => Amount;
	}
}
