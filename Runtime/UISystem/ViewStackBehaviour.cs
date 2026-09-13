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
		/// The view below is paused (<see cref="UIView.OnPause"/> fires, input blocked) and then
		/// hidden (animation plays); on close it is shown again and <see cref="UIView.OnResume"/> fires.
		/// Good for full-screen takeover flows.
		/// </summary>
		HideBelow,

		/// <summary>
		/// The view below remains visible but input is blocked (interactable = false).
		/// Good for popups and dialogs that dim the background.
		/// </summary>
		PauseOnlyBelow,

		/// <summary>
		/// The view below is closed/destroyed when this view is shown.
		/// Good for navigation flows where you don't want to return.
		/// </summary>
		CloseBelow,
	}
}

