using AK.Core;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// Project-wide catalog of TutorialProvider assets with identity-keyed runtime lookups.
	/// Views and metas may hold a Uid&lt;TutorialProvider&gt; and resolve it here, keeping
	/// bundles free of hard asset references. Tracked automatically in the editor.
	/// </summary>
	[CreateAssetMenu(fileName = "TutorialProviderRegistry", menuName = "AK/Tutorials/Tutorial Provider Registry")]
	public class TutorialProviderRegistry : UidRegistryAsset<TutorialProvider> { }
}
