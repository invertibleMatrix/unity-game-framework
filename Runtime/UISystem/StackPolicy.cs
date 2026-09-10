namespace AK.Systems
{
	/// <summary>
	/// What a <see cref="ViewStackBehaviour"/> does to the view beneath it. The show and
	/// close pipelines are the only readers; a new behaviour is added here, not in a switch.
	/// </summary>
	internal static class StackPolicy
	{
		/// <summary>The view below loses input and gets OnPause; it gets them back when the one above closes.</summary>
		public static bool Pauses(ViewStackBehaviour behaviour) =>
			behaviour is ViewStackBehaviour.HideBelow
				or ViewStackBehaviour.PauseAndHideBelow
				or ViewStackBehaviour.PauseOnlyBelow;

		/// <summary>The view below is also taken off screen until the one above closes.</summary>
		public static bool Hides(ViewStackBehaviour behaviour) =>
			behaviour is ViewStackBehaviour.HideBelow
				or ViewStackBehaviour.PauseAndHideBelow;

		/// <summary>The view below is closed for good.</summary>
		public static bool Closes(ViewStackBehaviour behaviour) => behaviour == ViewStackBehaviour.CloseBelow;

		/// <summary>A view above with this behaviour keeps the one below from being resumed while it is still there.</summary>
		public static bool Covers(ViewStackBehaviour behaviour) => Pauses(behaviour);
	}
}
