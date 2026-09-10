using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.Examples.Costs;
using AK.Examples.Currency;
using AK.Examples.IAP;
using UnityEngine;

namespace AK.Examples.Store
{
	/// <summary>
	/// Container for shop item definitions with query methods
	/// </summary>
	[CreateAssetMenu(fileName = "ShopMeta", menuName = "AK/MetaData/Store/ShopMeta")]
	public class ShopMeta : MetaDataAsset, IMetaWithRegistry
	{
		[SerializeField] private ShopRegistry _productsRegistry;

		public IAPProductDefinition NoAdsProductDefinition;
		public IAPProductDefinition VIPSubscriptionProductDefinition;

		[Header("Categories")] [Tooltip("Product categories for UI organization.")]
		public List<ShopCategoryDefinition> Categories = new();

		public ShopRegistry Registry => _productsRegistry;

		public UidRegistryAssetBase RegistryAsset => _productsRegistry;

		private IReadOnlyList<ShopItemDefinition> Items =>
			_productsRegistry != null ? _productsRegistry.Objects : Array.Empty<ShopItemDefinition>();

		public override void InitializeMeta() { }

		public bool TryGetItem(Uid<ShopItemDefinition> id, out ShopItemDefinition item)
		{
			item = null;
			return _productsRegistry != null && _productsRegistry.TryResolve(id, out item);
		}

		public ShopItemDefinition GetItem(Uid<ShopItemDefinition> id)
		{
			return TryGetItem(id, out var item) ? item : null;
		}

		public ShopCategoryDefinition GetCategory(Uid<ShopCategoryDefinition> id)
		{
			if (id.IsNone) return null;

			for (int i = 0; i < Categories.Count; i++)
			{
				var category = Categories[i];
				if (category != null && category.CategoryId == id) return category;
			}

			return null;
		}

		public List<ShopItemDefinition> GetItemsByType(ShopItemType type)
		{
			return Items.Where(i => i.Type == type).ToList();
		}

		public List<ShopItemDefinition> GetItemsByRarity(ShopItemRarity rarity)
		{
			return Items.Where(i => i.Rarity == rarity).ToList();
		}

		/// <summary>
		/// Items whose cost is paid in the given currency (matched on the cost's resource identity).
		/// </summary>
		public List<ShopItemDefinition> GetItemsByCurrency(CurrencyDefinition currency)
		{
			if (currency == null) return new List<ShopItemDefinition>();

			var currencyId = currency.Id;
			return Items.Where(i => i.Cost != null && i.Cost.Resource == currencyId).ToList();
		}

		public List<ShopItemDefinition> GetItemsByCostType(CostType costType)
		{
			if (costType == null) return new List<ShopItemDefinition>();
			return Items.Where(i => i.CostType == costType).ToList();
		}

		public List<ShopItemDefinition> GetProductsByCategory(Uid<ShopCategoryDefinition> categoryId)
		{
			return GetProductsByCategory(GetCategory(categoryId));
		}

		public List<ShopItemDefinition> GetProductsByCategory(ShopCategoryDefinition category)
		{
			var result = new List<ShopItemDefinition>();
			if (category == null || category.Products == null || _productsRegistry == null) return result;

			foreach (var productId in category.Products)
			{
				if (_productsRegistry.TryResolve(productId, out var item)) result.Add(item);
			}

			return result;
		}

		public List<ShopItemDefinition> GetTimeLimitedItems()
		{
			return Items.Where(i => i.HasTimeLimit).ToList();
		}

		public List<ShopItemDefinition> GetLimitedQuantityItems()
		{
			return Items.Where(i => i.IsLimitedQuantity).ToList();
		}

		public List<ShopItemDefinition> GetItemsForLevelRange(int minLevel, int maxLevel)
		{
			return Items.Where(i => i.MinimumLevel >= minLevel && (i.MaximumLevel == 0 || i.MaximumLevel <= maxLevel)).ToList();
		}

		public List<ShopItemDefinition> GetItemsSortedByPrice()
		{
			return Items.OrderBy(i => i.GetDiscountedPrice()).ToList();
		}

		public List<ShopItemDefinition> GetItemsSortedByRarity()
		{
			return Items.OrderBy(i => i.Rarity).ToList();
		}

		public List<ShopCategoryDefinition> GetVisibleCategories()
		{
			return Categories.Where(c => c != null && c.IsVisible).ToList();
		}

		public List<ShopCategoryDefinition> GetAvailableCategoriesForPlayer(int playerLevel)
		{
			return Categories.Where(c => c != null && c.IsVisible && playerLevel >= c.MinimumLevel && (c.MaximumLevel == 0 || playerLevel <= c.MaximumLevel))
			                 .ToList();
		}

		public List<ShopCategoryDefinition> GetFeaturedCategories()
		{
			return Categories.Where(c => c != null && c.IsFeatured && c.IsVisible).ToList();
		}

		public List<ShopCategoryDefinition> GetCategoriesSortedBySortOrder()
		{
			return Categories.Where(c => c != null).OrderBy(c => c.SortOrder).ToList();
		}

		public int GetTotalItemCount() => Items.Count;

		public int GetItemCountByType(ShopItemType type) => Items.Count(i => i.Type == type);

		public int GetItemCountByRarity(ShopItemRarity rarity) => Items.Count(i => i.Rarity == rarity);

		public int GetTotalCategoryCount() => Categories.Count;

		public List<ShopItemDefinition> GetExpiringItems(int hours)
		{
			long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			long expireTime = currentTime + (hours * 3600);

			return Items.Where(i =>
				i.HasTimeLimit &&
				i.EndTime > currentTime &&
				i.EndTime <= expireTime
			).ToList();
		}

		public List<ShopItemDefinition> GetNewlyAvailableItems(int hours)
		{
			long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			long startTime = currentTime - (hours * 3600);

			return Items.Where(i =>
				i.HasTimeLimit &&
				i.StartTime >= startTime &&
				i.StartTime <= currentTime
			).ToList();
		}
	}
}
