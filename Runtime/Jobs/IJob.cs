namespace AK.Jobs
{
	/// <summary>
	/// Work that runs on a scheduler worker thread. Three rules for the body of <see cref="Execute"/>:
	/// no Unity API (engine objects are main-thread-only); no <c>ListPool&lt;T&gt;</c> or any other
	/// main-thread pool; own your scratch memory — allocate it once on the job and reuse it.
	///
	/// An exception thrown by <see cref="Execute"/> is logged and counted in
	/// <see cref="JobSchedulerStats.LastFrameFaults"/>; it never stops the other jobs or the worker.
	/// </summary>
	public interface IJob
	{
		void Execute(in FrameContext ctx);
	}

	/// <summary>
	/// Optional on jobs scheduled by reference. Called on the main thread exactly once per schedule:
	/// at the barrier after the job ran (<c>cancelled == false</c>), or when the job was cancelled
	/// or the scheduler disposed before it was delivered (<c>cancelled == true</c>).
	/// A job cancelled after hand-off may still have executed once.
	/// </summary>
	public interface IJobCallback
	{
		void OnComplete(bool cancelled);
	}
}
