namespace AK.Jobs
{
	/// <summary>
	/// Executes a contiguous slice of one job source on the calling worker. Implementations catch
	/// per element and return how many threw; nothing may escape into the worker loop.
	/// </summary>
	internal interface IChunkRunner
	{
		int ExecuteRange(int start, int count, in FrameContext ctx);
	}

	/// <summary>
	/// One unit of work claimed by a worker: a contiguous slice of a single source, so each worker
	/// still streams sequentially through memory. Built by the main thread at the barrier, sorted
	/// by phase, immutable for the frame.
	/// </summary>
	internal readonly struct Chunk
	{
		public readonly IChunkRunner Runner;
		public readonly int          Start;
		public readonly int          Count;
		public readonly int          Phase;

		public Chunk(IChunkRunner runner, int start, int count, int phase)
		{
			Runner = runner;
			Start  = start;
			Count  = count;
			Phase  = phase;
		}
	}
}
