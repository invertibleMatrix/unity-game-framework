namespace AK.Kernel.Timing
{
	/// <summary>
	/// How unscaled time counts while an app runs in the foreground: frame by frame, each frame
	/// for its real duration but at most <see cref="MaxFrameSeconds"/>. A stall then counts as one
	/// short frame: the app suspended in the background, a fullscreen ad holding the player loop,
	/// a long load, a breakpoint. Scaled time has the same guard, Unity's maximum delta time.
	///
	/// Waits and timeouts on unscaled time count this way, so a stall can't run a watchdog out
	/// before its owner has had a frame to answer, and can't end every pending wait at once.
	/// A deadline on the wall clock, which must count the time spent in the background, reads
	/// the system clock instead.
	/// </summary>
	public static class ForegroundTime
	{
		/// <summary>The most real time one frame counts for, in seconds.</summary>
		public const double MaxFrameSeconds = 0.5d;

		/// <summary>
		/// The time a frame of <paramref name="deltaSeconds"/> counts for: the delta, at most
		/// <see cref="MaxFrameSeconds"/>. A delta that is zero, negative or NaN counts for
		/// nothing, so a clock that steps back never rewinds a wait.
		/// </summary>
		public static double FrameStep(double deltaSeconds)
		{
			if (!(deltaSeconds > 0d)) return 0d;

			return deltaSeconds < MaxFrameSeconds ? deltaSeconds : MaxFrameSeconds;
		}
	}
}
