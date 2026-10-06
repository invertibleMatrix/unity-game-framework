using System;
using System.Collections.Generic;
using System.Threading;
using AK.Jobs;
using NUnit.Framework;

namespace AK.Tests.Jobs
{
	[TestFixture]
	public sealed class JobSchedulerPhaseTests
	{
		private sealed class OrderJob : IFrameJob
		{
			private readonly List<char> _log;
			private readonly char       _id;

			public OrderJob(List<char> log, char id)
			{
				_log = log;
				_id  = id;
			}

			public void Execute(in FrameContext ctx) => _log.Add(_id);
		}

		private struct ProducerJob : IFrameJob
		{
			public int   Index;
			public int   Frame;
			public int[] Shared;

			public void Execute(in FrameContext ctx)
			{
				Thread.SpinWait(200);
				Shared[Index] = Frame * 1000 + Index;
			}
		}

		private struct ConsumerJob : IFrameJob
		{
			public int   Index;
			public int   Frame;
			public int[] Shared;
			public int   Observed;

			public void Execute(in FrameContext ctx)
			{
				Observed = Shared[Index];
			}
		}

		private readonly List<JobScheduler> _created = new();

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

		// ------------------------------------------------------------------ repeating

		[Test]
		public void Repeating_RunsOncePerTick_UntilCancelled_ThenCallbackCancelledTrue()
		{
			JobScheduler   scheduler = Create(workers: 2);
			var            job       = new RecordingJob();
			FrameJobHandle handle    = scheduler.ScheduleRepeating(job);

			for (int frame = 1; frame <= 5; frame++) RunFrame(scheduler, frame);
			Assert.AreEqual(5, job.ExecuteCount);
			Assert.AreEqual(5, job.ExecuteFrame);
			Assert.AreEqual(0, job.CompleteCount, "a repeating job completes only when withdrawn");
			Assert.IsTrue(scheduler.IsPending(handle));

			Assert.IsTrue(scheduler.Cancel(handle));
			Assert.IsFalse(scheduler.IsPending(handle));

			RunFrame(scheduler, 6);
			Assert.AreEqual(5, job.ExecuteCount, "purged at the barrier before the kick");
			Assert.AreEqual(1, job.CompleteCount);
			Assert.IsTrue(job.CompletedCancelled);

			RunFrame(scheduler, 7);
			Assert.AreEqual(5, job.ExecuteCount);
			Assert.AreEqual(1, job.CompleteCount);
			Assert.IsFalse(scheduler.Cancel(handle), "handle is stale once the slot is freed");
		}

		[Test]
		public void Repeating_ScheduledMidFrame_StartsNextFrame_NotTheRunningOne()
		{
			JobScheduler scheduler = Create(workers: 2);
			var          blocker   = new RecordingJob { Gate = new ManualResetEventSlim(false) };
			var          repeating = new RecordingJob();

			scheduler.Schedule(blocker);
			scheduler.Tick(Frame(1));

			scheduler.ScheduleRepeating(repeating);
			Thread.Sleep(30);
			Assert.AreEqual(0, repeating.ExecuteCount, "staged, not merged into the array the workers are reading");

			blocker.Gate.Set();
			scheduler.WaitForIdle();
			Assert.AreEqual(0, repeating.ExecuteCount);

			RunFrame(scheduler, 2);
			Assert.AreEqual(1, repeating.ExecuteCount);
			Assert.AreEqual(2, repeating.ExecuteFrame);
		}

		[Test]
		public void Repeating_RemovalKeepsOrderOfRemainingJobs()
		{
			JobScheduler scheduler = Create(workers: 1);
			var          log       = new List<char>();
			var          handles   = new Dictionary<char, FrameJobHandle>();

			foreach (char id in "ABCDE") handles[id] = scheduler.ScheduleRepeating(new OrderJob(log, id));

			RunFrame(scheduler, 1);
			Assert.AreEqual("ABCDE", new string(log.ToArray()));

			log.Clear();
			scheduler.Cancel(handles['C']);
			RunFrame(scheduler, 2);
			Assert.AreEqual("ABDE", new string(log.ToArray()), "ordered removal: the survivors keep their relative order");

			log.Clear();
			scheduler.Cancel(handles['A']);
			scheduler.Cancel(handles['E']);
			handles['F'] = scheduler.ScheduleRepeating(new OrderJob(log, 'F'));
			RunFrame(scheduler, 3);
			Assert.AreEqual("BDF", new string(log.ToArray()), "new repeating jobs append after the survivors");
		}

		// ------------------------------------------------------------------ phases

		[Test]
		public void Phase1Job_AlwaysObservesPhase0Result_1000Frames_4Workers()
		{
			const int elements = 64;
			const int frames   = 1000;

			JobScheduler scheduler = Create(workers: 4, phases: 2);
			var          shared    = new int[elements];
			var          producers = new JobBatch<ProducerJob>(elements, phase: 0, itemsPerChunk: 4);
			var          consumers = new JobBatch<ConsumerJob>(elements, phase: 1, itemsPerChunk: 4);
			scheduler.Register(producers);
			scheduler.Register(consumers);

			int mismatches = 0;

			for (int frame = 1; frame <= frames; frame++)
			{
				for (int i = 0; i < elements; i++)
				{
					ref ProducerJob p = ref producers.Add();
					p.Index  = i;
					p.Frame  = frame;
					p.Shared = shared;

					ref ConsumerJob c = ref consumers.Add();
					c.Index  = i;
					c.Frame  = frame;
					c.Shared = shared;
				}

				RunFrame(scheduler, frame);

				Span<ConsumerJob> results = consumers.Results;
				for (int i = 0; i < results.Length; i++)
				{
					if (results[i].Observed != results[i].Frame * 1000 + results[i].Index) mismatches++;
				}
			}

			Assert.AreEqual(0, mismatches, "no phase-1 element may run before every phase-0 chunk has finished");
			Assert.AreEqual(elements, consumers.Results.Length);
		}

		[Test]
		public void SinglePhaseScheduler_RejectsHigherPhases()
		{
			JobScheduler scheduler = Create(workers: 1, phases: 1);
			var          job       = new RecordingJob();

			Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(job, phase: 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleRepeating(job, phase: 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Register(new JobBatch<ConsumerJob>(8, phase: 1)));
			Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(job, phase: -1));

			JobScheduler two = Create(workers: 1, phases: 2);
			Assert.DoesNotThrow(() => two.Schedule(job, phase: 1));
		}
	}
}
