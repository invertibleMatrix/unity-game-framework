using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.CoreDomain.Costs;
using AK.CoreDomain.Currency;
using AK.CoreDomain.IAP;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain.Store
{
	/// <summary>
	/// Container for shop item definitions with query methods
	/// </summary>
	[CreateAssetMenu(fileName = "ShopMeta", menuName = "Gameplay/MetaData/Store/ShopMeta")]
	public class ShopMeta : MetaDataAsset
	{
		[SerializeField] private ShopRegistry         _productsRegistry;
		
		public IAPProductDefinition NoAdsProductDefinition;
		public IAPProductDefinition VIPSubscriptionProductDefinition;

		[InlineEditor, Header("Categories")] [Tooltip("Product categories for UI organization.")]
		public List<ShopCategoryDefinition> Categories;

		public ShopRegistry Registry => _productsRegistry;
		
		public override void InitializeMeta()
		{
			_productsRegistry.Initialize();
		}

		/// <summary>
		/// Get item by UID
		/// </summary>
		public ShopItemDefinition GetItemByUID(UID uid)
		{
			return _productsRegistry.GetObjectByUID(uid);
		}
		
		/// <summary>
		/// Get category by UID
		/// </summary>
		public ShopCategoryDefinition GetCategoryByUID(UID uid)
		{
			return Categories.FirstOrDefault(c => c.CategoryID == uid);
		}

		/// <summary>
		/// Get all items of a specific type
		/// </summary>
		public List<ShopItemDefinition> GetItemsByType(ShopItemType type)
		{
			return _productsRegistry.Registry.Objects.Where(i => i.Type == type).ToList();
		}

		/// <summary>
		/// Get all items of a specific rarity
		/// </summary>
		public List<ShopItemDefinition> GetItemsByRarity(ShopItemRarity rarity)
		{
			return _productsRegistry.Registry.Objects.Where(i => i.Rarity == rarity).ToList();
		}

		/// <summary>
		/// Get all items that use a specific currency type.
		/// </summary>
		public List<ShopItemDefinition> GetItemsByCurrency(CurrencyType currencyType)
		{
			if (currencyType == null) return new List<ShopItemDefinition>();
			return _productsRegistry.Registry.Objects.Where(i => i.Cost?.CostTypeUID != null).ToList();
		}

		/// <summary>
		/// Get all items that use a specific CostType
		/// </summary>
		public List<ShopItemDefinition> GetItemsByCostType(CostType costType)
		{
			if (costType == null) return new List<ShopItemDefinition>();
			return _productsRegistry.Registry.Objects.Where(i => i.CostType == costType).ToList();
		}

		/// <summary>
		/// Gets all products in a specific category.
		/// </summary>
		public List<ShopItemDefinition> GetProductsByCategory(UID categoryID)
		{
			if (categoryID.IsEmpty())
			{
				return new List<ShopItemDefinition>();
			}

			ShopCategoryDefinition category = Categories.FirstOrDefault(c => c.CategoryID == categoryID);
			if (category == null || category.ProductIDs == null)
			{
				return new List<ShopItemDefinition>();
			}

			return _productsRegistry.Registry.Objects.Where(p => category.ProductIDs.Contains(p.UniqueID)).ToList();
		}

		/// <summary>
		/// Get items with time limits
		/// </summary>
		public List<ShopItemDefinition> GetTimeLimitedItems()
		{
			return _productsRegistry.Registry.Objects.Where(i => i.HasTimeLimit).ToList();
		}

		/// <summary>
		/// Get items with limited quantity
		/// </summary>
		public List<ShopItemDefinition> GetLimitedQuantityItems()
		{
			return _productsRegistry.Registry.Objects.Where(i => i.IsLimitedQuantity).ToList();
		}

		/// <summary>
		/// Get items for a specific level range
		/// </summary>
		public List<ShopItemDefinition> GetItemsForLevelRange(int minLevel, int maxLevel)
		{
			return _productsRegistry.Registry.Objects.Where(i => i.MinimumLevel >= minLevel && (i.MaximumLevel == 0 || i.MaximumLevel <= maxLevel))
			                        .ToList();
		}

		/// <summary>
		/// Get items sorted by price (low to high)
		/// </summary>
		public List<ShopItemDefinition> GetItemsSortedByPrice()
		{
			return _productsRegistry.Registry.Objects.OrderBy(i => i.GetDiscountedPrice()).ToList();
		}

		/// <summary>
		/// Get items sorted by rarity (common to legendary)
		/// </summary>
		public List<ShopItemDefinition> GetItemsSortedByRarity()
		{
			return _productsRegistry.Registry.Objects.OrderBy(i => i.Rarity).ToList();
		}

		/// <summary>
		/// Get all visible categories
		/// </summary>
		public List<ShopCategoryDefinition> GetVisibleCategories()
		{
			return Categories.Where(c => c.IsVisible).ToList();
		}

		/// <summary>
		/// Get categories available to a player
		/// </summary>
		public List<ShopCategoryDefinition> GetAvailableCategoriesForPlayer(int playerLevel)
		{
			return Categories.Where(c => c.IsVisible && playerLevel >= c.MinimumLevel && (c.MaximumLevel == 0 || playerLevel <= c.MaximumLevel))
			                 .ToList();
		}

		/// <summary>
		/// Get featured categories
		/// </summary>
		public List<ShopCategoryDefinition> GetFeaturedCategories()
		{
			return Categories.Where(c => c.IsFeatured && c.IsVisible).ToList();
		}

		/// <summary>
		/// Get categories sorted by sort order
		/// </summary>
		public List<ShopCategoryDefinition> GetCategoriesSortedBySortOrder()
		{
			return Categories.OrderBy(c => c.SortOrder).ToList();
		}

		/// <summary>
		/// Get total number of items
		/// </summary>
		public int GetTotalItemCount()
		{
			return _productsRegistry.Registry.Objects.Count;
		}

		/// <summary>
		/// Get number of items by type
		/// </summary>
		public int GetItemCountByType(ShopItemType type)
		{
			return _productsRegistry.Registry.Objects.Count(i => i.Type == type);
		}

		/// <summary>
		/// Get number of items by rarity
		/// </summary>
		public int GetItemCountByRarity(ShopItemRarity rarity)
		{
			return _productsRegistry.Registry.Objects.Count(i => i.Rarity == rarity);
		}

		/// <summary>
		/// Get total number of categories
		/// </summary>
		public int GetTotalCategoryCount()
		{
			return Categories.Count;
		}

		/// <summary>
		/// Get items that are expiring soon (within hours)
		/// </summary>
		public List<ShopItemDefinition> GetExpiringItems(int hours)
		{
			long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			long expireTime = currentTime + (hours * 3600);

			return _productsRegistry.Registry.Objects.Where(i =>
				i.HasTimeLimit &&
				i.EndTime > currentTime &&
				i.EndTime <= expireTime
			).ToList();
		}

		/// <summary>
		/// Get items that are newly available (within hours)
		/// </summary>
		public List<ShopItemDefinition> GetNewlyAvailableItems(int hours)
		{
			long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			long startTime = currentTime - (hours * 3600);

			return _productsRegistry.Registry.Objects.Where(i =>
				i.HasTimeLimit &&
				i.StartTime >= startTime &&
				i.StartTime <= currentTime
			).ToList();
		}
	}
}
