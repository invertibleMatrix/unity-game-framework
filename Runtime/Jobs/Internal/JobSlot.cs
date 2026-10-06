namespace AK.Jobs
{
	internal enum JobKind : byte
	{
		OneShot,
		Repeating,
	}

	/// <summary>
	/// Main-thread bookkeeping for one scheduled job. Lives in the scheduler's <c>SlotMap</c>;
	/// workers never see it, which is why cancellation can be a plain field.
	/// </summary>
	internal struct JobSlot
	{
		public IFrameJob         Job;
		public IFrameJobCallback Callback;
		public JobKind           Kind;
		public byte              Phase;
		public bool              Cancelled;

		/// <summary>Hand-off generation the job was scheduled in; equal to the scheduler's current one while the job is still pending.</summary>
		public int ScheduledGen;
	}
}
