using System;
using System.Collections.Generic;
using System.Threading;
using AK.Jobs;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Jobs
{
	/// <summary>
	/// A scheduler without workers, as on WebGL: the barrier runs each frame on the main thread, and
	/// completions and batch results still arrive at the barrier after, as they do with workers.
	/// </summary>
	[TestFixture]
	public sealed class JobSchedulerInlineTests
	{
		private sealed class LogJob : IFrameJob
		{
			private readonly List<char> _log;
			private readonly char       _id;

			public LogJob(List<char> log, char id)
			{
				_log = log;
				_id  = id;
			}

			public void Execute(in FrameContext ctx) => _log.Add(_id);
		}

		private struct DoubleJob : IFrameJob
		{
			public int Input;
			public int Output;
			public int ThreadId;

			public void Execute(in FrameContext ctx)
			{
				Output   = Input * 2;
				ThreadId = Thread.CurrentThread.ManagedThreadId;
			}
		}

		private readonly List<JobScheduler> _created = new();
		private int _mainThreadId;

		[SetUp]
		public void SetUp() => _mainThreadId = Thread.CurrentThread.ManagedThreadId;

		[TearDown]
		public void TearDown()
		{
			foreach (JobScheduler scheduler in _created) scheduler.Dispose();
			_created.Clear();
		}

		private JobScheduler Create(int phases = 1)
		{
			var scheduler = new JobScheduler(new JobSchedulerOptions { WorkerCount = 0, PhaseCount = phases });
			_created.Add(scheduler);
			return scheduler;
		}

		private static FrameContext Frame(int n) => new(n, n / 60f, n / 60f, 1f / 60f);

		[Test]
		public void WorkerCount0_StartsNoThreads_NegativeIsRejected()
		{
			JobScheduler scheduler = Create();

			Assert.AreEqual(0, scheduler.Stats.WorkerCount);
			Assert.IsFalse(scheduler.AnyWorkerAlive);
			Assert.Throws<ArgumentOutOfRangeException>(() => new JobScheduler(new JobSchedulerOptions { WorkerCount = -1 }));
		}

		[Test]
		public void Schedule_RunsOnTheMainThread_InsideTheNextTick_CompletesAtTheOneAfter()
		{
			JobScheduler   scheduler = Create();
			var            job       = new RecordingJob();
			FrameJobHandle handle    = scheduler.Schedule(job);

			scheduler.Tick(Frame(1));
			Assert.AreEqual(1, job.ExecuteCount, "the barrier ran it before returning");
			Assert.AreEqual(_mainThreadId, job.ExecuteThreadId);
			Assert.AreEqual(1, job.ExecuteFrame);
			Assert.IsTrue(scheduler.IsIdle, "nothing is left running between barriers");
			Assert.AreEqual(0, job.CompleteCount, "completion waits for the next barrier, as with workers");
			Assert.IsTrue(scheduler.IsPending(handle));

			scheduler.WaitForIdle();
			scheduler.Tick(Frame(2));
			Assert.AreEqual(1, job.CompleteCount);
			Assert.IsFalse(job.CompletedCancelled);
			Assert.AreEqual(_mainThreadId, job.CompleteThreadId);
			Assert.IsFalse(scheduler.IsPending(handle));
		}

		[Test]
		public void Batch_RunsOnTheMainThread_ResultsAppearAtTheBarrierAfter()
		{
			JobScheduler scheduler = Create();
			var          batch     = new JobBatch<DoubleJob>(capacity: 8);
			scheduler.Register(batch);

			for (int i = 0; i < 100; i++) batch.Add().Input = i;

			scheduler.Tick(Frame(1));
			Assert.AreEqual(0, batch.Results.Length, "results wait for the barrier that collects the frame");

			scheduler.Tick(Frame(2));
			Span<DoubleJob> results = batch.Results;
			Assert.AreEqual(100, results.Length);
			for (int i = 0; i < results.Length; i++)
			{
				Assert.AreEqual(2 * i, results[i].Output);
				Assert.AreEqual(_mainThreadId, results[i].ThreadId);
			}

			Assert.AreEqual(100, scheduler.Stats.LastFrameJobs);
		}

		[Test]
		public void Phases_RunInOrder_JobsWithinAPhaseInScheduleOrder()
		{
			JobScheduler scheduler = Create(phases: 3);
			var          log       = new List<char>();

			scheduler.Schedule(new LogJob(log, 'C'), phase: 2);
			scheduler.Schedule(new LogJob(log, 'A'), phase: 0);
			scheduler.Schedule(new LogJob(log, 'B'), phase: 1);
			scheduler.Schedule(new LogJob(log, 'a'), phase: 0);

			scheduler.Tick(Frame(1));

			Assert.AreEqual("AaBC", new string(log.ToArray()));
		}

		[Test]
		public void ThrowingJob_IsCounted_TheOthersStillRun()
		{
			JobScheduler scheduler = Create();
			var          thrower   = new RecordingJob { OnExecuteAction = () => throw new InvalidOperationException("inline boom") };
			var          neighbour = new RecordingJob();

			using (ExpectedLog.Exception("inline boom"))
			{
				scheduler.Schedule(thrower);
				scheduler.Schedule(neighbour);
				scheduler.Tick(Frame(1));
			}

			Assert.AreEqual(1, neighbour.ExecuteCount);

			scheduler.Tick(Frame(2));
			Assert.AreEqual(1, scheduler.Stats.LastFrameFaults);
			Assert.AreEqual(1, thrower.CompleteCount, "a faulted job still completes exactly once");
			Assert.IsFalse(thrower.CompletedCancelled);
		}

		[Test]
		public void Repeating_RunsAtEveryTick_NoFrameIsSkipped()
		{
			JobScheduler scheduler = Create();
			var          job       = new RecordingJob();
			scheduler.ScheduleRepeating(job);

			for (int frame = 1; frame <= 10; frame++) scheduler.Tick(Frame(frame));

			Assert.AreEqual(10, job.ExecuteCount);
			Assert.AreEqual(10, job.ExecuteFrame);
			Assert.AreEqual(0, scheduler.Stats.SkippedFrames);
		}

		[Test]
		public void Dispose_CollectsTheFrameItRan_AndCancelsPending()
		{
			var scheduler = new JobScheduler(new JobSchedulerOptions { WorkerCount = 0 });
			var ran       = new RecordingJob();
			var pending   = new RecordingJob();

			scheduler.Schedule(ran);
			scheduler.Tick(Frame(1));
			scheduler.Schedule(pending);
			scheduler.Dispose();

			Assert.AreEqual(1, ran.CompleteCount);
			Assert.IsFalse(ran.CompletedCancelled);
			Assert.AreEqual(0, pending.ExecuteCount);
			Assert.AreEqual(1, pending.CompleteCount);
			Assert.IsTrue(pending.CompletedCancelled);
		}

		[Test]
		public void SteadyState_AllocatesNothing()
		{
			const int jobCount = 64, elements = 256;

			JobScheduler scheduler = Create();
			var          jobs      = new CountingJob[jobCount];
			var          batch     = new JobBatch<DoubleJob>(elements);
			int          frame     = 0;

			for (int i = 0; i < jobCount; i++) jobs[i] = new CountingJob();
			scheduler.Register(batch);

			void RunFrames()
			{
				for (int f = 0; f < 20; f++)
				{
					for (int i = 0; i < jobCount; i++) scheduler.Schedule(jobs[i]);
					for (int i = 0; i < elements; i++) batch.Add().Input = i;
					scheduler.Tick(Frame(++frame));
				}
			}

			RunFrames();
			int allocations = GcAllocations.Count(RunFrames);

			Assert.AreEqual(0, allocations, "schedule + inline run + completion delivery must not allocate once warm");
			Assert.AreEqual(40, jobs[0].Count, "each barrier ran the jobs scheduled before it");
		}
	}
}
