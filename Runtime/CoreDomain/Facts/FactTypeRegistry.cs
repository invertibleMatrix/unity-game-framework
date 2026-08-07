using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Facts
{
	/// <summary>
	/// Project-wide catalog of FactType assets with GUID-keyed runtime lookups.
	/// Use Refresh All Objects in the editor to repopulate.
	/// </summary>
	[CreateAssetMenu(fileName = "FactTypeRegistry", menuName = "AK/Facts/Fact Type Registry")]
	public class FactTypeRegistry : TypedUIDRegistryAsset<FactType> { }
}
