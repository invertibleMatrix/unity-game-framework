using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Currency
{
	/// <summary>
	/// ScriptableObject asset representing a type of currency.
	/// Create asset instances for each currency type your game supports
	/// (e.g., "Soft", "Hard", "Event", "Social", "Special", "Energy").
	/// </summary>
	[CreateAssetMenu(fileName = "CurrencyType", menuName = "AK/MetaData/Currency/CurrencyType")]
	public class CurrencyType : MetaDataAsset
	{
	}
}
