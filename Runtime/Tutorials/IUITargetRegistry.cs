using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// Maps UITargetId assets to live UI RectTransforms so data-driven presentation
	/// (tutorial steps, highlights) can name targets without scene references.
	///
	/// An id can have several instances at once, such as a view's target and the same
	/// target in a second copy of that view. <see cref="TryGet"/> returns the topmost one.
	/// </summary>
	public interface IUITargetRegistry
	{
		/// <summary>Adds <paramref name="target"/> under <paramref name="id"/>. Registering an instance twice keeps one entry.</summary>
		void Register(UITargetId id, RectTransform target);

		/// <summary>Removes that one instance; the id's other instances stay.</summary>
		void Unregister(UITargetId id, RectTransform target);

		/// <summary>
		/// The topmost instance of <paramref name="id"/>: an active one before an inactive one,
		/// then the one drawn on top (overlay canvases over camera canvases, then sorting layer,
		/// then the sorting order of its nearest sorting canvas), then the latest registered.
		/// </summary>
		bool TryGet(UITargetId id, out RectTransform target);
	}
}
