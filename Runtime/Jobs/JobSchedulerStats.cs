using System.Diagnostics;

namespace AK.Jobs
{
	/// <summary>Snapshot of the last collected frame. Ticks are <see cref="Stopwatch"/> ticks; see <see cref="TicksToMilliseconds"/>.</summary>
	public readonly struct JobSchedulerStats
	{
		public readonly int WorkerCount;

		/// <summary>Jobs and batch elements executed in the last frame.</summary>
		public readonly int LastFrameJobs;

		public readonly int LastFrameChunks;

		/// <summary>Jobs whose <see cref="IFrameJob.Execute"/> threw in the last frame.</summary>
		public readonly int LastFrameFaults;

		/// <summary>Cumulative frames where the workers were still busy at the barrier, so nothing was handed off.</summary>
		public readonly int SkippedFrames;

		/// <summary>Time spent inside worker loops last frame, summed over workers.</summary>
		public readonly long LastFrameWorkerTicks;

		/// <summary>Time spent by the slowest worker last frame — the frame's parallel critical path.</summary>
		public readonly long LastFrameCriticalTicks;

		internal JobSchedulerStats(int workerCount, int lastFrameJobs, int lastFrameChunks, int lastFrameFaults, int skippedFrames,
		                           long lastFrameWorkerTicks, long lastFrameCriticalTicks)
		{
			WorkerCount            = workerCount;
			LastFrameJobs          = lastFrameJobs;
			LastFrameChunks        = lastFrameChunks;
			LastFrameFaults        = lastFrameFaults;
			SkippedFrames          = skippedFrames;
			LastFrameWorkerTicks   = lastFrameWorkerTicks;
			LastFrameCriticalTicks = lastFrameCriticalTicks;
		}

		public static double TicksToMilliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

		public override string ToString() =>
			$"workers {WorkerCount}, jobs {LastFrameJobs} in {LastFrameChunks} chunks, faults {LastFrameFaults}, " +
			$"worker {TicksToMilliseconds(LastFrameWorkerTicks):0.###} ms (critical {TicksToMilliseconds(LastFrameCriticalTicks):0.###} ms), skipped {SkippedFrames}";
	}
}
