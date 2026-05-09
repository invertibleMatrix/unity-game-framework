using AK.Core;
using UnityEngine;

namespace GameplayCore.MetaData.Costs
{
	[System.Serializable]
	public class CostOption
	{
		[Tooltip("The type of this cost.")]
		public CostType Type;

		[Tooltip("The amount for Coin, Gem, or Resource costs.")]
		public int Amount;

		[Tooltip("UID of the Cost Type e.g CurrencyUID, Ad UID, InAPP item UID etc")]
		public UID CostTypeUID;

		[Tooltip("The ID of the resource to be consumed if the type is Resource.")]
		public UID ResourceID;
	}
}