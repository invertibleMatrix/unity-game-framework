using System.Collections.Generic;
using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.MetaData.Store
{
	/// <summary>
	/// Definition for a shop category to organize items
	/// </summary>
	[CreateAssetMenu(fileName = "ShopCategoryDefinition", menuName = "Gameplay/MetaData/Store/ShopCategoryDefinition")]
	public class ShopCategoryDefinition : MetaDataAsset
	{
		[Header("Identification")]
		[Tooltip("Unique identifier for this category")]
		public UID CategoryID => this;

		[Tooltip("Optional for filtering: Which Cost Type is used to make purchases in this store category")]
		public CostType CostType;

		[Tooltip("Optional if CostType is used: UID for the Coast Type e.g for Currency use Currency Def id etc")]
		public UID CostTypeUID;

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
		
		[InlineEditor]
		public List<UID> ProductIDs;
	}
}