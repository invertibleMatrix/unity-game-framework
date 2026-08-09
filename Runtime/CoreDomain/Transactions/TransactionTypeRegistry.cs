using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Transactions
{
	/// <summary>
	/// Project-wide catalog of TransactionType assets with GUID-keyed runtime lookups.
	/// Use Refresh All Objects in the editor to repopulate.
	/// </summary>
	[CreateAssetMenu(fileName = "TransactionTypeRegistry", menuName = "AK/Transactions/Transaction Type Registry")]
	public class TransactionTypeRegistry : TypedUIDRegistryAsset<TransactionType> { }
}
