namespace AK.Systems
{
	/// <summary>
	/// How a view's children close relative to the view itself on a graceful close.
	/// Each level of a hierarchy consults its own policy for its own children, so deep
	/// cascades compose: rows close together → the list closes → the root closes.
	/// Only governs graceful closes — teardown cascades (ParentDestroyed) always
	/// settle immediately without animation.
	/// </summary>
	public enum ChildCloseOrder
	{
		/// <summary>Current behavior: the parent animates out first; children settle immediately after.</summary>
		ParentFirst,

		/// <summary>All children animate out together (one WhenAll), then the parent hides.</summary>
		ChildrenFirstParallel,

		/// <summary>Children animate out one at a time, then the parent hides.</summary>
		ChildrenFirstSequential
	}
}
