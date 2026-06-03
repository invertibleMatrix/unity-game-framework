using System;
using AK.Core;
using AK.CoreDomain;
using UnityEngine;

namespace AK.Examples.Store
{
	/// <summary>
	/// Definition for a shop item with pricing, availability, and rewards
	/// </summary>
	[CreateAssetMenu(fileName = "ShopItemDefinition", menuName = "AK/MetaData/Store/ShopItemDefinition")]
	public class ShopItemDefinition : PurchasableItemDefinition
	{
		/// <summary>
		/// Check if the item is on discount
		/// </summary>
		public bool IsOnDiscount => DiscountPercentage > 0;

		/// <summary>
		/// Get the discounted price
		/// </summary>
		public int GetDiscountedPrice()
		{
			if (!IsOnDiscount) return Price;
			return Mathf.RoundToInt(Price * (1f - DiscountPercentage / 100f));
		}

		/// <summary>
		/// Check if the item is within its time limit
		/// </summary>
		public bool IsWithinTimeLimit()
		{
			if (!HasTimeLimit) return true;

			long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			return currentTime >= StartTime && currentTime <= EndTime;
		}

		/// <summary>
		/// Check if this is the player's first purchase
		/// </summary>
		public bool IsFirstPurchase(int playerPurchases)
		{
			return playerPurchases == 0;
		}
	}
}