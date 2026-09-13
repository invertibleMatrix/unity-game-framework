using System;

namespace AK.Jobs
{
	/// <summary>
	/// Frame-synchronous job system. Jobs scheduled during frame N are handed to the worker threads
	/// at the top of frame N + 1 and run in parallel with that frame's main-thread work; their
	/// completions are delivered on the main thread at the top of frame N + 2.
	///
	/// Every member must be called from the thread that created the scheduler. Nothing here blocks
	/// the main thread except <see cref="WaitForIdle"/>.
	/// </summary>
	public interface IJobScheduler : IDisposable
	{
		/// <summary>Runs <paramref name="job"/> once, on a worker, in the next frame.</summary>
		JobHandle Schedule(IJob job, int phase = 0);

		/// <summary>
		/// Runs <paramref name="job"/> on a worker every frame from the next one until cancelled; its
		/// callback fires once, with <c>cancelled == true</c>, when it is withdrawn. The job's state is
		/// being written by a worker during every frame, so the main thread must not read it directly —
		/// publish results through a later-phase job or use a <see cref="JobBatch{TJob}"/> instead.
		/// </summary>
		JobHandle ScheduleRepeating(IJob job, int phase = 0);

		/// <summary>
		/// Withdraws a job. Before hand-off the job never runs; after hand-off it may run once more.
		/// Either way its <see cref="IJobCallback.OnComplete"/> receives <c>cancelled == true</c>
		/// at the next barrier. False if the handle is stale or already cancelled.
		/// </summary>
		bool Cancel(JobHandle handle);

		/// <summary>True while the job is scheduled and not cancelled — until its completion is delivered.</summary>
		bool IsPending(JobHandle handle);

		/// <summary>Starts rotating the batch at every barrier. Idempotent for the same scheduler.</summary>
		void Register<TJob>(JobBatch<TJob> batch) where TJob : struct, IJob;

		/// <summary>Stops rotating the batch. A frame in flight still executes it; wait for idle before discarding it.</summary>
		void Unregister<TJob>(JobBatch<TJob> batch) where TJob : struct, IJob;

		/// <summary>True when no frame is in flight on the workers.</summary>
		bool IsIdle { get; }

		/// <summary>Blocks until the workers finish the frame in flight. For teardown and scene changes; not for per-frame use.</summary>
		void WaitForIdle();

		JobSchedulerStats Stats { get; }
	}
}
