using System.Collections.Generic;
using AK.Core.Collections;

namespace AK.Jobs
{
	/// <summary>
	/// Everything scheduled for one phase. Reference jobs and their slot handles live in parallel
	/// arrays: workers read only the job array (8 bytes per entry, nothing written back), and the
	/// main thread resolves completion by index into the handle array.
	///
	/// <c>Pending*</c> is appended to by the main thread during the frame; <c>Executing*</c> is read
	/// by workers. The two swap at the barrier. <c>Repeating*</c> is main-owned, mutated only at the
	/// barrier, and read by workers during the frame. Batches rotate their own buffers at the barrier.
	/// </summary>
	internal sealed class PhaseLane
	{
		private const int InitialCapacity = 64;

		public JobArray<IJob>            PendingJobs      = new(InitialCapacity);
		public JobArray<Handle<JobSlot>> PendingHandles   = new(InitialCapacity);
		public JobArray<IJob>            ExecutingJobs    = new(InitialCapacity);
		public JobArray<Handle<JobSlot>> ExecutingHandles = new(InitialCapacity);

		public readonly JobArray<IJob>            RepeatingJobs    = new(16);
		public readonly JobArray<Handle<JobSlot>> RepeatingHandles = new(16);

		/// <summary>Repeating jobs scheduled during the frame; merged into <c>Repeating*</c> at the barrier so workers never see a growing array.</summary>
		public readonly JobArray<IJob>            StagedRepeatingJobs    = new(16);
		public readonly JobArray<Handle<JobSlot>> StagedRepeatingHandles = new(16);

		public readonly List<IJobBatch> Batches = new();

		public readonly ReferenceJobRunner OneShotRunner   = new();
		public readonly ReferenceJobRunner RepeatingRunner = new();

		/// <summary>One-shot jobs cancelled while still pending; when zero the hand-off skips the compaction pass.</summary>
		public int CancelledBeforeHandOff;

		/// <summary>Repeating jobs cancelled since the last barrier; when zero the barrier skips the purge pass.</summary>
		public int RepeatingCancelled;

		public PhaseLane()
		{
			OneShotRunner.Jobs   = ExecutingJobs;
			RepeatingRunner.Jobs = RepeatingJobs;
		}

		public void Swap()
		{
			(PendingJobs, ExecutingJobs)       = (ExecutingJobs, PendingJobs);
			(PendingHandles, ExecutingHandles) = (ExecutingHandles, PendingHandles);
			OneShotRunner.Jobs                 = ExecutingJobs;
		}
	}
}
