namespace AK.Kernel.Timing
{
	/// <summary>
	/// The time a timer counts: one of the two <see cref="TimeDomain"/>s, or the wall clock.
	///
	/// A frame-driven feature, such as a tween or a wait, runs on a <see cref="TimeDomain"/>. A
	/// timer can also count the wall clock, which goes on while the app is in the background, so
	/// a countdown to a deadline completes at the deadline.
	/// </summary>
	public enum TimeBase : byte
	{
		/// <summary>Game time, as on <see cref="TimeDomain.Scaled"/>: stops at a time scale of zero and follows slow or fast motion.</summary>
		Scaled = 0,

		/// <summary>
		/// Real time while the app runs in the foreground, as on <see cref="TimeDomain.Unscaled"/>:
		/// ignores the time scale, and counts each frame for at most <see cref="ForegroundTime.MaxFrameSeconds"/>.
		/// </summary>
		Unscaled = 1,

		/// <summary>The wall clock: ignores the time scale and counts the time the app spends in the background.</summary>
		Wall = 2,
	}
}
