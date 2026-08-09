using AK.Core;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// UID identity for a UI element that data-driven presentation can point at.
	/// Dragged into both UITarget (scene/prefab side) and tutorial steps (data side),
	/// replacing free-text keys with rename-safe, typo-proof references.
	/// </summary>
	[CreateAssetMenu(fileName = "UITargetId", menuName = "AK/Tutorials/UI Target Id")]
	public class UITargetId : UID
	{
	}
}
