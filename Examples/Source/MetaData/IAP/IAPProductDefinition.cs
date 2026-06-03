using System;
using System.Collections.Generic;
using AK.Core;
using AK.Examples.Rewards;
using AK.Examples.Store;
using UnityEngine;

namespace AK.Examples.IAP
{
	/// <summary>
	/// Defines a single In-App Purchase product that can be purchased by players.
	/// Similar to RewardDefinition but for purchasable items.
	/// </summary>
	[CreateAssetMenu(fileName = "IAPProductDefinition", menuName = "AK/MetaData/IAP/IAPProductDefinition")]
	public class IAPProductDefinition : ShopItemDefinition
	{
		[Header("Product Type")] [Tooltip("The type of IAP product.")]
		public IAPProductType ProductType;

		[Tooltip("Required product IDs that must be owned before this can be purchased.")]
		public List<string> RequiredProductIDs;

		public bool VisibleInStore = true;
		
		/// <summary>
		/// Gets all rewards from this product (handles both single reward and bundle).
		/// </summary>
		public List<RewardDefinition> GetAllRewards()
		{
			List<RewardDefinition> rewards = new();

			if (RewardBundle != null && RewardBundle.Rewards.Count > 0)
			{
				RewardBundle.GetAllRewardsRecursive(rewards);
			}
			else if (Reward != null)
			{
				rewards.Add(Reward);
			}

			return rewards;
		}

		/// <summary>
		/// Checks if this product is currently available for purchase.
		/// </summary>
		public bool IsAvailable()
		{
			// Check special offer time window
			if (IsSpecialOffer)
			{
				DateTime now = DateTime.UtcNow;
				if (now < SpecialOfferStartTime || now > SpecialOfferEndTime)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Checks if this is a subscription product.
		/// </summary>
		private bool IsSubscription => ProductType == IAPProductType.Subscription ||
		                               ProductType == IAPProductType.NonRenewingSubscription;
	}
}