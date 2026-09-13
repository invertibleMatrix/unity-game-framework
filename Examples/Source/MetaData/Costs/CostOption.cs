using AK.Core;
using AK.CoreDomain;
using UnityEngine;

namespace AK.Examples.Costs
{
	[System.Serializable]
	public class CostOption : ICostInfo
	{
		[Tooltip("The type of this cost. References a CostType asset.")]
		public CostType Type;

		[Tooltip("The amount for this cost (e.g., 100 coins, 5 gems, 10 stamina).")]
		public int Amount;

		[Tooltip("Identity of the specific resource consumed (e.g. a CurrencyDefinition, AdPlacement, or IAP product).")]
		[UidOf(typeof(MetaDataAsset))]
		public Uid Resource;

		Uid ICostInfo.CostType => Type != null ? Type.Id : Uid.None;
		int ICostInfo.Amount => Amount;
	}
}
