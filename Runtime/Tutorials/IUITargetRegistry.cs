using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// Maps UITargetId assets to live UI RectTransforms so data-driven presentation
	/// (tutorial steps, highlights) can name targets without scene references.
	/// </summary>
	public interface IUITargetRegistry
	{
		void Register(UITargetId id, RectTransform target);
		void Unregister(UITargetId id, RectTransform target);
		bool TryGet(UITargetId id, out RectTransform target);
	}
}
