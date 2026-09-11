namespace AK.Jobs
{
	/// <summary>
	/// Frame timing captured on the main thread at the barrier and handed to every job that runs in
	/// that frame. Jobs read this instead of <c>UnityEngine.Time</c>, which is main-thread-only.
	/// </summary>
	public readonly struct FrameContext
	{
		public readonly int   Frame;
		public readonly float Time;
		public readonly float UnscaledTime;
		public readonly float DeltaTime;

		public FrameContext(int frame, float time, float unscaledTime, float deltaTime)
		{
			Frame        = frame;
			Time         = time;
			UnscaledTime = unscaledTime;
			DeltaTime    = deltaTime;
		}

		public override string ToString() => $"frame {Frame} t={Time:0.###} dt={DeltaTime:0.####}";
	}
}
