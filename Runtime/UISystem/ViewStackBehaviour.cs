namespace AK.Systems
{
	/// <summary>
	/// Unified stack behaviour for all UIViews.
	/// Determines what happens to the view below when a new view is pushed on top.
	/// </summary>
	public enum ViewStackBehaviour
	{
		/// <summary>
		/// The view below is not affected at all. Both remain visible and interactive.
		/// Good for overlays, toasts, and transient notifications.
		/// </summary>
		DoNothing,

		/// <summary>
		/// The view below is hidden (animation plays). When this view is closed, the view below is shown again.
		/// Good for full-screen takeover flows.
		/// </summary>
		HideBelow,

		/// <summary>
		/// The view below remains visible but input is blocked (interactable = false).
		/// Good for popups and dialogs that dim the background.
		/// </summary>
		PauseOnlyBelow,

		/// <summary>
		/// The view below is fully paused and hidden (animation plays, input blocked).
		/// Good for full-screen menus that completely replace the previous screen.
		/// </summary>
		PauseAndHideBelow,

		/// <summary>
		/// The view below is closed/destroyed when this view is shown.
		/// Good for navigation flows where you don't want to return.
		/// </summary>
		CloseBelow,
	}
}

