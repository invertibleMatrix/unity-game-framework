using AK.Core;
using AK.Examples.Currency;
using UnityEngine;

namespace AK.CoreDomain
{
	/// <summary>
	/// Registry for all currency definitions.
	/// Similar to IAPRegistry but for currencies.
	/// </summary>
	[CreateAssetMenu(fileName = "CurrencyRegistry", menuName = "AK/MetaData/Currency/CurrencyRegistry")]
	public class CurrencyRegistry : TypedUIDRegistryAsset<CurrencyDefinition> { }
}