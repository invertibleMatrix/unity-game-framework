using System;
using System.Collections.Generic;
using AK.Core;
using AK.Examples.Costs;
using AK.Examples.Rewards;
using AK.Examples.Store;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain
{
	public class PurchasableItemDefinition : MetaDataAsset, IPurchasable
	{
		[Tooltip("Rarity/Importance of this item")]
		public ShopItemRarity Rarity;

		[Header("Item Type")] [Tooltip("Type of shop item")]
		public ShopItemType Type;

		[Header("Pricing")] [Tooltip("The cost required to purchase this item. Type references a CostType SO asset.")]
		public CostOption Cost;

		[Header("Basic Information")] [Tooltip("Unique identifier for this product (must match store ID for IAP items).")]
		public string ProductID;

		[Tooltip("Price in the specified currency (used for display; actual deduction is via CostProvider)")]
		public int Price;

		[Tooltip("Discount percentage (0-100)")] [Range(0, 100)]
		public int DiscountPercentage;

		[Tooltip("Minimum level required to purchase")]
		public int MinimumLevel;

		[Tooltip("Maximum level for this item (0 = no limit)")]
		public int MaximumLevel;

		[Tooltip("Is this item limited quantity?")]
		public bool IsLimitedQuantity;

		[ShowIf("IsLimitedQuantity")] [Tooltip("Maximum quantity available")]
		public int MaxQuantity;

		[Header("Time Limits")] [Tooltip("Does this item have a time limit?")]
		public bool HasTimeLimit;

		[ShowIf("HasTimeLimit")] [Tooltip("Start time (Unix timestamp)")]
		public long StartTime;

		[ShowIf("HasTimeLimit")] [Tooltip("End time (Unix timestamp)")]
		public long EndTime;

		[Header("Special Offer")] [Tooltip("Is this a limited-time special offer?")]
		public bool IsSpecialOffer;

		[Tooltip("Is this item featured?")]
		public bool IsFeatured;

		[Tooltip("Start time of special offer (UTC).")] [ShowIf("IsSpecialOffer")]
		public DateTime SpecialOfferStartTime;

		[Tooltip("End time of special offer (UTC).")] [ShowIf("IsSpecialOffer")]
		public DateTime SpecialOfferEndTime;

		[Header("Requirements")] [Tooltip("Minimum level required to purchase.")]
		public int MinLevelRequired = 1;

		[Header("Tags")] [Tooltip("Tags for categorization and filtering (e.g., 'Starter', 'Premium', 'Limited').")]
		public List<string> Tags;

		[Header("Display Priority")] [Tooltip("Sort order in store UI (lower = higher priority).")]
		public int DisplayPriority = 0;

		[Header("Analytics")] [Tooltip("Custom analytics event name for tracking purchases.")]
		public string AnalyticsEventName;

		[Header("Rewards")] [Tooltip("The rewards granted upon purchase.")] 
		[InlineEditor]
		public RewardDefinition Reward;

		[Tooltip("List of rewards in this bundle.")]
		public RewardBundle RewardBundle;

		[Tooltip("Optional bonus rewards that are guaranteed (e.g., 'Buy this and get 100 extra coins').")]
		public RewardBundle BonusRewards;

		[Tooltip("Optional gacha rewards that are probabilistic (e.g., 'Chance to get rare item').")] 
		[InlineEditor]
		public GachaBundle GachaRewards;

		public UID AnalyticsEventDefId;

		// IPurchasable explicit implementation
		string IPurchasable.DisplayName => DisplayName;
		string IPurchasable.ProductID => ProductID;
		ICostInfo IPurchasable.Cost => Cost;

		/// <summary>
		/// Collect all rewards from this item as IReward (interface-based, used by services).
		/// </summary>
		public void CollectRewards(List<IReward> rewards)
		{
			if (Reward != null)
				Reward.CollectRewards(rewards);

			if (HasAnyBundle())
			{
				RewardBundle?.CollectRewards(rewards);
				BonusRewards?.CollectRewards(rewards);
			}
		}
		
		/// <summary>
		/// Convenience accessor: the CostType SO from the Cost option.
		/// Null if Cost is not set.
		/// </summary>
		public CostType CostType => Cost?.Type;

		public bool HasAnyBundle()
		{
			return RewardBundle?.Rewards?.Count > 0 || BonusRewards?.Rewards?.Count > 0 || GachaRewards?.PossibleRewards?.Count > 0;
		}

		/// <summary>
		/// Get the discounted price for display purposes.
		/// </summary>
		public int GetDiscountedPrice()
		{
			if (DiscountPercentage <= 0) return Price;
			return Mathf.RoundToInt(Price * (1f - DiscountPercentage / 100f));
		}
	}
}
