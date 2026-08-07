using Reflex.Attributes;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// Registers this RectTransform in the UITargetRegistry under a UITargetId asset,
	/// so tutorial steps can reference it from data. Injection arrives before Start
	/// for views shown through UISystem.
	/// </summary>
	public class UITarget : MonoBehaviour
	{
		[SerializeField] private UITargetId _id;

		[Inject] private IUITargetRegistry _registry;

		private void Start()
		{
			if (_registry != null && _id != null)
			{
				_registry.Register(_id, transform as RectTransform);
			}
		}

		private void OnDestroy()
		{
			if (_registry != null && _id != null)
			{
				_registry.Unregister(_id, transform as RectTransform);
			}
		}
	}
}
