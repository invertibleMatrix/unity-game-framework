namespace AK.Kernel.Timing
{
	/// <summary>
	/// Which clock a time-based feature follows.
	///
	/// Scaled time is game time: it stops when the game pauses with a time scale of zero and
	/// stretches with slow motion. Unscaled time is real time and keeps running. Menus,
	/// tutorials, previews and UI audio belong on <see cref="Unscaled"/>, so a paused game never
	/// freezes the UI that is supposed to un-pause it; gameplay belongs on <see cref="Scaled"/>.
	///
	/// Both count only while the app runs in the foreground, and each frame counts for a bounded
	/// time: Unity caps a scaled frame at its maximum delta time, and an unscaled wait counts a
	/// frame for at most <see cref="ForegroundTime.MaxFrameSeconds"/>. A countdown to a deadline
	/// on the wall clock, which goes on while the app is in the background, belongs on neither:
	/// a timer counts it as <see cref="TimeBase.Wall"/>.
	/// </summary>
	public enum TimeDomain : byte
	{
		/// <summary>Game time: pauses at a time scale of zero and follows slow or fast motion.</summary>
		Scaled = 0,

		/// <summary>Real time, independent of the time scale.</summary>
		Unscaled = 1,
	}
}
