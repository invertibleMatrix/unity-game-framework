using AK.Core;
using GameplayCore.MetaData.Currency;
using UnityEngine;

namespace GameplayCore.MetaData
{
	/// <summary>
	/// Registry for all currency definitions.
	/// Similar to IAPRegistry but for currencies.
	/// </summary>
	[CreateAssetMenu(fileName = "CurrencyRegistry", menuName = "Gameplay/MetaData/Currency/CurrencyRegistry")]
	public class CurrencyRegistry : TypedUIDRegistryAsset<CurrencyDefinition> { }
}