using System.Collections.Generic;
using AK.Core;
using AK.Examples.Costs;
using UnityEngine;

namespace AK.Examples.Store
{
	/// <summary>
	/// Definition for a shop category to organize items. The category's own identity
	/// (<see cref="UID.Id"/>) is the key UI and persistence use to refer to it.
	/// </summary>
	[CreateAssetMenu(fileName = "ShopCategoryDefinition", menuName = "AK/MetaData/Store/ShopCategoryDefinition")]
	public class ShopCategoryDefinition : MetaDataAsset
	{
		[Tooltip("Optional for filtering: which Cost Type is used to make purchases in this store category.")]
		public CostType CostType;

		[Tooltip("Optional if CostType is used: identity of the resource behind the cost type, e.g. a CurrencyDefinition.")]
		[UidOf(typeof(MetaDataAsset))]
		public Uid CostResource;

		[Header("Category Settings")] [Tooltip("Is this category currently visible?")]
		public bool IsVisible;

		[Tooltip("Sort order in shop (lower = first)")]
		public int SortOrder;

		[Tooltip("Is this category featured?")]
		public bool IsFeatured;

		[Header("Availability")] [Tooltip("Minimum level required to view this category")]
		public int MinimumLevel;

		[Tooltip("Maximum level for this category (0 = no limit)")]
		public int MaximumLevel;

		[Tooltip("Products shown in this category, by identity. Resolved through the shop registry.")]
		public List<Uid<ShopItemDefinition>> Products = new();

		public Uid<ShopCategoryDefinition> CategoryId => IdAs<ShopCategoryDefinition>();

		public bool ContainsProduct(Uid<ShopItemDefinition> productId)
		{
			if (Products == null) return false;

			for (int i = 0; i < Products.Count; i++)
			{
				if (Products[i] == productId) return true;
			}

			return false;
		}
	}
}
