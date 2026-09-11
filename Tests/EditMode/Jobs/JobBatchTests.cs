using System;
using System.Collections.Generic;
using System.Threading;
using AK.Jobs;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Jobs
{
	[TestFixture]
	public sealed class JobBatchTests
	{
		private struct SquareJob : IJob
		{
			public int  Input;
			public int  Output;
			public int  ThreadId;
			public bool Throw;

			public void Execute(in FrameContext ctx)
			{
				ThreadId = Thread.CurrentThread.ManagedThreadId;
				if (Throw) throw new InvalidOperationException("batch boom");
				Output = Input * Input;
			}
		}

		private struct TallyJob : IJob
		{
			public int   Index;
			public int[] Counts;

			public void Execute(in FrameContext ctx)
			{
				Interlocked.Increment(ref Counts[Index]);
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

		private JobScheduler Create(int workers)
		{
			var scheduler = new JobScheduler(new JobSchedulerOptions { WorkerCount = workers });
			_created.Add(scheduler);
			return scheduler;
		}

		private static FrameContext Frame(int n) => new(n, n / 60f, n / 60f, 1f / 60f);

		private static void RunFrame(JobScheduler scheduler, int frame)
		{
			scheduler.Tick(Frame(frame));
			scheduler.WaitForIdle();
		}

		[Test]
		public void Batch_ElementsExecuteInPlace_ResultsReadableAfterTheCollectingTick()
		{
			JobScheduler scheduler = Create(workers: 2);
			var          batch     = new JobBatch<SquareJob>(capacity: 16);
			scheduler.Register(batch);

			for (int i = 0; i < 100; i++) batch.Add().Input = i;
			Assert.AreEqual(100, batch.PendingCount, "Add grows past the initial capacity");

			RunFrame(scheduler, 1);
			Assert.AreEqual(0, batch.PendingCount, "handed off");
			Assert.AreEqual(0, batch.Results.Length, "results appear at the barrier that collects the frame, not while it runs");

			RunFrame(scheduler, 2);
			Span<SquareJob> results = batch.Results;
			Assert.AreEqual(100, results.Length);
			for (int i = 0; i < results.Length; i++)
			{
				Assert.AreEqual(i, results[i].Input, "order is preserved");
				Assert.AreEqual(i * i, results[i].Output, "the element's own fields hold the result");
				Assert.AreNotEqual(_mainThreadId, results[i].ThreadId);
			}

			Assert.AreEqual(0, batch.ResultFaults);
			Assert.AreEqual(100, scheduler.Stats.LastFrameJobs);
		}

		[Test]
		public void Batch_ChunksAreContiguousSlicesOfOneBatch_AndCoverEveryElementOnce()
		{
			JobScheduler scheduler = Create(workers: 4);
			var          first     = new JobBatch<SquareJob>(capacity: 1000);
			var          second    = new JobBatch<SquareJob>(capacity: 100, itemsPerChunk: 7);
			scheduler.Register(first);
			scheduler.Register(second);

			for (int i = 0; i < 1000; i++) first.Add().Input = i;
			for (int i = 0; i < 100; i++) second.Add().Input = i;

			scheduler.Tick(Frame(1));
			ReadOnlySpan<Chunk> chunks = scheduler.Chunks;
			scheduler.WaitForIdle();

			int[] firstCover  = new int[1000];
			int[] secondCover = new int[100];
			int   secondChunks = 0;

			foreach (Chunk chunk in chunks)
			{
				Assert.Greater(chunk.Count, 0);
				int[] cover;
				if (ReferenceEquals(chunk.Runner, first)) cover = firstCover;
				else if (ReferenceEquals(chunk.Runner, second)) { cover = secondCover; secondChunks++; Assert.LessOrEqual(chunk.Count, 7); }
				else { Assert.Fail("a chunk must belong to exactly one registered source"); return; }

				for (int i = chunk.Start; i < chunk.Start + chunk.Count; i++) cover[i]++;
			}

			foreach (int c in firstCover) Assert.AreEqual(1, c, "every element of the first batch is covered exactly once");
			foreach (int c in secondCover) Assert.AreEqual(1, c, "every element of the second batch is covered exactly once");
			Assert.AreEqual(15, secondChunks, "100 elements at a pinned 7 per chunk");
			Assert.GreaterOrEqual(chunks.Length - secondChunks, 4, "automatic chunking hands each of four workers something");
		}

		[Test]
		public void Batch_WithWorkerCount4_EveryElementExecutesExactlyOnce_10kElements_200Frames()
		{
			const int elements = 10_000;
			const int frames   = 200;

			JobScheduler scheduler = Create(workers: 4);
			var          batch     = new JobBatch<TallyJob>(capacity: elements);
			var          counts    = new int[elements];
			scheduler.Register(batch);

			for (int frame = 1; frame <= frames; frame++)
			{
				for (int i = 0; i < elements; i++)
				{
					ref TallyJob job = ref batch.Add();
					job.Index  = i;
					job.Counts = counts;
				}

				RunFrame(scheduler, frame);
			}

			for (int i = 0; i < elements; i++)
			{
				Assert.AreEqual(frames, counts[i], $"element {i} must run exactly once per frame");
			}
		}

		[Test]
		public void Batch_ThrowingElement_IsCountedInResultFaults_OthersComplete()
		{
			JobScheduler scheduler = Create(workers: 2);
			var          batch     = new JobBatch<SquareJob>(capacity: 128);
			scheduler.Register(batch);

			using (ExpectedLog.Exception("batch boom", expected: 3))
			{
				for (int i = 0; i < 100; i++)
				{
					ref SquareJob job = ref batch.Add();
					job.Input = i;
					job.Throw = i % 33 == 0 && i > 0;
				}

				RunFrame(scheduler, 1);
			}

			RunFrame(scheduler, 2);

			Assert.AreEqual(3, batch.ResultFaults);
			Assert.AreEqual(3, scheduler.Stats.LastFrameFaults);

			Span<SquareJob> results = batch.Results;
			Assert.AreEqual(100, results.Length);
			for (int i = 0; i < results.Length; i++)
			{
				if (results[i].Throw) continue;
				Assert.AreEqual(i * i, results[i].Output, "elements after a throwing one still run");
			}
		}

		[Test]
		public void Batch_Results_StayUntilANewerFrameWithElementsCompletes()
		{
			JobScheduler scheduler = Create(workers: 1);
			var          batch     = new JobBatch<SquareJob>(capacity: 16);
			scheduler.Register(batch);

			for (int i = 0; i < 10; i++) batch.Add().Input = i;
			RunFrame(scheduler, 1);
			RunFrame(scheduler, 2);
			Assert.AreEqual(10, batch.Results.Length);

			RunFrame(scheduler, 3);
			Assert.AreEqual(10, batch.Results.Length, "an empty frame does not wipe the last results");

			for (int i = 0; i < 5; i++) batch.Add().Input = 100 + i;
			RunFrame(scheduler, 4);
			Assert.AreEqual(10, batch.Results.Length, "the new frame is still executing; the old results stay readable");
			Assert.AreEqual(9 * 9, batch.Results[9].Output);

			RunFrame(scheduler, 5);
			Assert.AreEqual(5, batch.Results.Length);
			Assert.AreEqual(100 * 100, batch.Results[0].Output);
		}

		[Test]
		public void Batch_RegisterTwiceIsIdempotent_OtherSchedulerThrows_UnregisterStopsRotation()
		{
			JobScheduler scheduler = Create(workers: 1);
			JobScheduler other     = Create(workers: 1);
			var          batch     = new JobBatch<SquareJob>(capacity: 8);

			scheduler.Register(batch);
			scheduler.Register(batch);
			Assert.IsTrue(batch.IsRegistered);
			Assert.Throws<InvalidOperationException>(() => other.Register(batch));

			batch.Add().Input = 3;
			scheduler.Unregister(batch);
			Assert.IsFalse(batch.IsRegistered);

			RunFrame(scheduler, 1);
			RunFrame(scheduler, 2);
			Assert.AreEqual(1, batch.PendingCount, "an unregistered batch is not rotated");
			Assert.AreEqual(0, batch.Results.Length);

			other.Register(batch);
			RunFrame(other, 1);
			RunFrame(other, 2);
			Assert.AreEqual(9, batch.Results[0].Output, "free to register elsewhere once unregistered");
		}

		[Test]
		public void Batch_SteadyState_AllocatesZeroBytesOnMainThread()
		{
			const int elements = 1000;

			JobScheduler scheduler = Create(workers: 2);
			var          batch     = new JobBatch<SquareJob>(capacity: elements);
			scheduler.Register(batch);

			for (int frame = 1; frame <= 5; frame++)
			{
				for (int i = 0; i < elements; i++) batch.Add().Input = i;
				RunFrame(scheduler, frame);
			}

			long before = GC.GetAllocatedBytesForCurrentThread();

			for (int frame = 6; frame <= 55; frame++)
			{
				for (int i = 0; i < elements; i++) batch.Add().Input = i;
				RunFrame(scheduler, frame);
			}

			long after = GC.GetAllocatedBytesForCurrentThread();

			Assert.AreEqual(0, after - before);
			Assert.AreEqual(elements, batch.Results.Length);
		}
	}
}
