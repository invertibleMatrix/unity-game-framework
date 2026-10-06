using Reflex.Attributes;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// Registers this RectTransform in the <see cref="IUITargetRegistry"/> under a
	/// <see cref="UITargetId"/> asset while it is enabled, so tutorial steps can reference it
	/// from data. A target in a hidden or pooled view is therefore not registered. Works
	/// without a registry in the container: it then registers nothing.
	/// </summary>
	public class UITarget : MonoBehaviour
	{
		[SerializeField] private UITargetId _id;

		private IUITargetRegistry _registry;

		/// <summary>
		/// Injection can arrive after the first OnEnable (a view is enabled when it is
		/// instantiated, then injected), so it registers here too.
		/// </summary>
		[Inject]
		private void Construct(IUITargetRegistry registry = null)
		{
			_registry = registry;
			if (isActiveAndEnabled) Register();
		}

		private void OnEnable() => Register();

		private void OnDisable()
		{
			if (_registry != null && _id != null)
			{
				_registry.Unregister(_id, transform as RectTransform);
			}
		}

		private void Register()
		{
			if (_registry != null && _id != null)
			{
				_registry.Register(_id, transform as RectTransform);
			}
		}
	}
}
