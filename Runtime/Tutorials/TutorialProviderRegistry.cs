using AK.Core;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// Project-wide catalog of TutorialProvider assets with GUID-keyed runtime lookups.
	/// Resolution hub for UIDRef links — views and metas reference providers by GUID
	/// and resolve them here, keeping bundles free of hard asset references.
	/// Use Refresh All Objects in the editor to repopulate.
	/// </summary>
	[CreateAssetMenu(fileName = "TutorialProviderRegistry", menuName = "AK/Tutorials/Tutorial Provider Registry")]
	public class TutorialProviderRegistry : TypedUIDRegistryAsset<TutorialProvider> { }
}
