using System;
using System.Collections.Generic;
using System.Threading;
using AK.Jobs;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Jobs
{
	/// <summary>
	/// Drives the barrier by hand: <c>Tick</c> hands pending work to the workers and kicks them,
	/// <c>WaitForIdle</c> blocks until they are done, the next <c>Tick</c> delivers completions.
	/// </summary>
	[TestFixture]
	public sealed class JobSchedulerCoreTests
	{
		private readonly List<JobScheduler> _created = new();
		private int _mainThreadId;

		[SetUp]
		public void SetUp()
		{
			_mainThreadId = Thread.CurrentThread.ManagedThreadId;
		}

		[TearDown]
		public void TearDown()
		{
			foreach (JobScheduler scheduler in _created) scheduler.Dispose();
			_created.Clear();
		}

		private JobScheduler Create(int workers, int phases = 1)
		{
			var scheduler = new JobScheduler(new JobSchedulerOptions { WorkerCount = workers, PhaseCount = phases });
			_created.Add(scheduler);
			return scheduler;
		}

		private static FrameContext Frame(int n) => new(n, n / 60f, n / 60f, 1f / 60f);

		private static void RunFrame(JobScheduler scheduler, int frame)
		{
			scheduler.Tick(Frame(frame));
			scheduler.WaitForIdle();
		}

		// ------------------------------------------------------------------ execution

		[Test]
		public void Schedule_RunsOnAWorkerThread_AfterTheNextTick()
		{
			JobScheduler scheduler = Create(workers: 2);
			var          job       = new RecordingJob();

			scheduler.Schedule(job);
			Assert.AreEqual(0, job.ExecuteCount, "nothing runs before the barrier");

			RunFrame(scheduler, 1);

			Assert.AreEqual(1, job.ExecuteCount);
			Assert.AreNotEqual(_mainThreadId, job.ExecuteThreadId, "Execute must run off the main thread");
			Assert.AreEqual(1, job.ExecuteFrame, "the job sees the frame context it was kicked with");
		}

		[Test]
		public void Schedule_OnComplete_RunsOnMainThread_ExactlyOnce_AtTheFollowingTick()
		{
			JobScheduler scheduler = Create(workers: 2);
			var          job       = new RecordingJob();
			JobHandle    handle    = scheduler.Schedule(job);

			RunFrame(scheduler, 1);
			Assert.AreEqual(0, job.CompleteCount, "completion waits for the barrier, it is never delivered from a worker");
			Assert.IsTrue(scheduler.IsPending(handle), "still pending until its completion is delivered");

			RunFrame(scheduler, 2);
			Assert.AreEqual(1, job.CompleteCount);
			Assert.IsFalse(job.CompletedCancelled);
			Assert.AreEqual(_mainThreadId, job.CompleteThreadId);
			Assert.IsFalse(scheduler.IsPending(handle));

			RunFrame(scheduler, 3);
			RunFrame(scheduler, 4);
			Assert.AreEqual(1, job.CompleteCount, "exactly once");
			Assert.AreEqual(1, job.ExecuteCount, "a one-shot job runs once");
		}

		[Test]
		public void Schedule_DuringAFrame_IsNotVisibleToWorkersUntilTheNextTick()
		{
			JobScheduler scheduler = Create(workers: 2);
			var          blocker   = new RecordingJob { Gate = new ManualResetEventSlim(false) };
			var          late      = new RecordingJob();

			scheduler.Schedule(blocker);
			scheduler.Tick(Frame(1));

			scheduler.Schedule(late);
			Thread.Sleep(50);
			Assert.AreEqual(0, late.ExecuteCount, "the second worker is free, yet a job scheduled mid-frame must not be picked up");

			blocker.Gate.Set();
			scheduler.WaitForIdle();
			Assert.AreEqual(0, late.ExecuteCount, "still not visible after the frame ends");

			RunFrame(scheduler, 2);
			Assert.AreEqual(1, late.ExecuteCount);
		}

		// ------------------------------------------------------------------ cancellation

		[Test]
		public void Cancel_BeforeHandOff_NeverExecutes_CallbackGetsCancelledTrue()
		{
			JobScheduler scheduler = Create(workers: 1);
			var          job       = new RecordingJob();
			JobHandle    handle    = scheduler.Schedule(job);

			Assert.IsTrue(scheduler.Cancel(handle));
			Assert.IsFalse(scheduler.IsPending(handle));
			Assert.IsFalse(scheduler.Cancel(handle), "second cancel is a no-op");

			RunFrame(scheduler, 1);
			RunFrame(scheduler, 2);

			Assert.AreEqual(0, job.ExecuteCount);
			Assert.AreEqual(1, job.CompleteCount);
			Assert.IsTrue(job.CompletedCancelled);
			Assert.AreEqual(_mainThreadId, job.CompleteThreadId);
		}

		[Test]
		public void Cancel_AfterHandOff_MayExecuteOnce_CallbackGetsCancelledTrue()
		{
			JobScheduler scheduler = Create(workers: 1);
			var          job       = new RecordingJob { Gate = new ManualResetEventSlim(false) };
			JobHandle    handle    = scheduler.Schedule(job);

			scheduler.Tick(Frame(1));
			Assert.IsTrue(scheduler.Cancel(handle), "cancel after hand-off is accepted");
			Assert.IsFalse(scheduler.IsPending(handle));

			job.Gate.Set();
			scheduler.WaitForIdle();
			Assert.AreEqual(1, job.ExecuteCount, "already handed to a worker: it runs once");
			Assert.AreEqual(0, job.CompleteCount);

			RunFrame(scheduler, 2);
			Assert.AreEqual(1, job.CompleteCount);
			Assert.IsTrue(job.CompletedCancelled, "the caller still learns the job was cancelled");
		}

		[Test]
		public void Cancel_WithStaleHandle_ReturnsFalse()
		{
			JobScheduler scheduler = Create(workers: 1);
			var          first     = new RecordingJob();
			JobHandle    stale     = scheduler.Schedule(first);

			RunFrame(scheduler, 1);
			RunFrame(scheduler, 2);
			Assert.AreEqual(1, first.CompleteCount);

			var       second = new RecordingJob();
			JobHandle reused = scheduler.Schedule(second);
			Assert.AreEqual(stale.Slot.Index, reused.Slot.Index, "the freed slot is reused, so only the generation tells the handles apart");
			Assert.AreNotEqual(stale, reused);

			Assert.IsFalse(scheduler.Cancel(stale));
			Assert.IsFalse(scheduler.IsPending(stale));
			Assert.IsFalse(scheduler.Cancel(JobHandle.Invalid));
			Assert.IsTrue(scheduler.IsPending(reused), "the stale cancel did not touch the live job in the same slot");

			RunFrame(scheduler, 3);
			RunFrame(scheduler, 4);
			Assert.AreEqual(1, second.ExecuteCount);
			Assert.IsFalse(second.CompletedCancelled);
		}

		// ------------------------------------------------------------------ barrier behaviour

		[Test]
		public void Tick_WhileWorkersBusy_SkipsTheFrame_WithoutSwapping()
		{
			JobScheduler scheduler = Create(workers: 1);
			var          blocker   = new RecordingJob { Gate = new ManualResetEventSlim(false) };
			var          second    = new RecordingJob();

			scheduler.Schedule(blocker);
			scheduler.Tick(Frame(1));
			Assert.IsFalse(scheduler.IsIdle);

			scheduler.Schedule(second);
			scheduler.Tick(Frame(2));
			Assert.AreEqual(1, scheduler.Stats.SkippedFrames);
			Assert.AreEqual(0, second.ExecuteCount);

			blocker.Gate.Set();
			scheduler.WaitForIdle();
			Assert.IsTrue(scheduler.IsIdle);
			Assert.AreEqual(0, second.ExecuteCount, "a skipped tick must not have swapped the pending buffer");
			Assert.AreEqual(0, blocker.CompleteCount);

			RunFrame(scheduler, 3);
			Assert.AreEqual(1, blocker.CompleteCount, "the late frame is collected at the next successful barrier");
			Assert.IsFalse(blocker.CompletedCancelled);
			Assert.AreEqual(1, second.ExecuteCount);
			Assert.AreEqual(1, scheduler.Stats.SkippedFrames);
		}

		[Test]
		public void ThrowingJob_IsLogged_DoesNotKillWorkers_OthersStillRun()
		{
			JobScheduler scheduler = Create(workers: 1);
			var          thrower   = new RecordingJob { OnExecuteAction = () => throw new InvalidOperationException("boom") };
			var          neighbour = new RecordingJob();

			using (ExpectedLog.Exception("boom"))
			{
				scheduler.Schedule(thrower);
				scheduler.Schedule(neighbour);
				RunFrame(scheduler, 1);
			}

			Assert.AreEqual(1, thrower.ExecuteCount);
			Assert.AreEqual(1, neighbour.ExecuteCount, "a throwing job does not skip the rest of its chunk");

			RunFrame(scheduler, 2);
			Assert.AreEqual(1, scheduler.Stats.LastFrameFaults);
			Assert.AreEqual(1, thrower.CompleteCount, "a faulted job still completes exactly once");
			Assert.IsFalse(thrower.CompletedCancelled);

			var afterwards = new RecordingJob();
			scheduler.Schedule(afterwards);
			RunFrame(scheduler, 3);
			Assert.AreEqual(1, afterwards.ExecuteCount, "the single worker is still alive and serving frames");
			Assert.IsTrue(scheduler.AnyWorkerAlive);
		}

		[Test]
		public void Dispose_WhileAFrameIsInFlight_CollectsIt_JoinsWorkers_CancelsPending()
		{
			var scheduler = new JobScheduler(new JobSchedulerOptions { WorkerCount = 2 });
			var slow      = new RecordingJob { OnExecuteAction = () => Thread.Sleep(100) };
			var pending   = new RecordingJob();

			scheduler.Schedule(slow);
			scheduler.Tick(Frame(1));
			scheduler.Schedule(pending);

			scheduler.Dispose();

			Assert.AreEqual(1, slow.ExecuteCount);
			Assert.AreEqual(1, slow.CompleteCount, "the frame in flight is collected before shutdown");
			Assert.IsFalse(slow.CompletedCancelled);

			Assert.AreEqual(0, pending.ExecuteCount);
			Assert.AreEqual(1, pending.CompleteCount);
			Assert.IsTrue(pending.CompletedCancelled, "work that never ran is reported as cancelled");

			Assert.IsFalse(scheduler.AnyWorkerAlive, "worker threads have exited");
			Assert.Throws<ObjectDisposedException>(() => scheduler.Schedule(new RecordingJob()));
			scheduler.Dispose();
		}

		// ------------------------------------------------------------------ determinism & allocation

		[Test]
		public void WorkerCount_1_2_4_ProduceIdenticalCompletionOrder()
		{
			const int jobCount = 200;

			foreach (int workers in new[] { 1, 2, 4 })
			{
				JobScheduler scheduler = Create(workers);
				var          order     = new List<int>(jobCount);

				for (int i = 0; i < jobCount; i++)
				{
					int index = i;
					int spins = (i * 7919) % 400;
					scheduler.Schedule(new RecordingJob
					{
						OnExecuteAction  = () => Thread.SpinWait(spins),
						OnCompleteAction = (_, _) => order.Add(index),
					});
				}

				RunFrame(scheduler, 1);
				RunFrame(scheduler, 2);

				Assert.AreEqual(jobCount, order.Count, $"workers={workers}");
				for (int i = 0; i < jobCount; i++)
				{
					Assert.AreEqual(i, order[i], $"workers={workers}: completion order must be schedule order, independent of which worker ran what");
				}

				Assert.AreEqual(jobCount, scheduler.Stats.LastFrameJobs);
				Assert.GreaterOrEqual(scheduler.Stats.LastFrameChunks, Math.Min(workers, jobCount));
			}
		}

		[Test]
		public void Schedule_ReusedJobs_SteadyState_AllocatesNothingOnMainThread()
		{
			const int jobCount = 256;

			JobScheduler scheduler = Create(workers: 2);
			var          jobs      = new RecordingJob[jobCount];
			for (int i = 0; i < jobCount; i++) jobs[i] = new RecordingJob();

			for (int frame = 1; frame <= 5; frame++)
			{
				for (int i = 0; i < jobCount; i++) scheduler.Schedule(jobs[i]);
				RunFrame(scheduler, frame);
			}

			int allocations = GcAllocations.Count(() =>
			{
				for (int frame = 6; frame <= 55; frame++)
				{
					for (int i = 0; i < jobCount; i++) scheduler.Schedule(jobs[i]);
					RunFrame(scheduler, frame);
				}
			});

			Assert.AreEqual(0, allocations, "schedule + barrier + completion delivery must not allocate once warm");
			Assert.AreEqual(55, jobs[0].ExecuteCount);
		}

		private struct IntegrateJob : IJob
		{
			public float X, V;

			public void Execute(in FrameContext ctx)
			{
				V -= 9.81f * ctx.DeltaTime;
				X += V * ctx.DeltaTime;
			}
		}

		[Test]
		public void MixedWorkload_SteadyState_AllocatesNothing_OnMainThreadOrOnAnyWorker()
		{
			const int workers = 4, jobCount = 64, elements = 2000, warmup = 5, measured = 200;

			JobScheduler scheduler = Create(workers);
			var          oneShots  = new CountingJob[jobCount];
			var          batch     = new JobBatch<IntegrateJob>(elements);
			var          repeating = new CountingJob();

			for (int i = 0; i < jobCount; i++) oneShots[i] = new CountingJob();
			scheduler.Register(batch);
			scheduler.ScheduleRepeating(repeating);

			void Produce(int frame)
			{
				foreach (ref readonly IntegrateJob r in batch.Results) _ = r.X;
				for (int i = 0; i < elements; i++)
				{
					ref IntegrateJob e = ref batch.Add();
					e.X = i;
					e.V = 0f;
				}

				for (int i = 0; i < jobCount; i++) scheduler.Schedule(oneShots[i]);
				scheduler.Cancel(scheduler.Schedule(oneShots[0]));
				_ = scheduler.Stats;
				RunFrame(scheduler, frame);
			}

			for (int frame = 1; frame <= warmup; frame++) Produce(frame);

			int mainThread = GcAllocations.Count(() =>
			{
				for (int frame = warmup + 1; frame <= warmup + measured; frame++) Produce(frame);
			});

			int anyThread = GcAllocations.Count(() =>
			{
				for (int frame = warmup + measured + 1; frame <= warmup + 2 * measured; frame++) Produce(frame);
			}, allThreads: true);

			Assert.AreEqual(0, mainThread, "main thread: batch fill + results read + schedule + cancel + barrier must not allocate once warm");
			Assert.AreEqual(0, anyThread, "workers: chunk claims, phase waits and job execution must not allocate once warm");
			Assert.AreEqual(workers, scheduler.Stats.WorkerCount);
			Assert.AreEqual(warmup + 2 * measured, repeating.Count);
			Assert.AreEqual(elements, batch.Results.Length);
		}
	}
}
