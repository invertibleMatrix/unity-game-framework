using System.Collections;
using System.Threading;
using AK.Jobs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.TestTools;

namespace AK.Tests.Jobs
{
	/// <summary>The player-loop driver against the real engine loop: timing in frames, thread affinity, attach/detach.</summary>
	public sealed class JobSchedulerPlayModeTests
	{
		private sealed class FrameStampJob : IFrameJob, IFrameJobCallback
		{
			public int ExecuteCount;
			public int ExecuteThreadId;
			public int ExecuteCtxFrame;
			public int CompleteCount;
			public int CompleteFrame;
			public int CompleteThreadId;

			public void Execute(in FrameContext ctx)
			{
				Interlocked.Increment(ref ExecuteCount);
				ExecuteThreadId = Thread.CurrentThread.ManagedThreadId;
				ExecuteCtxFrame = ctx.Frame;
			}

			public void OnComplete(bool cancelled)
			{
				CompleteCount++;
				CompleteFrame    = Time.frameCount;
				CompleteThreadId = Thread.CurrentThread.ManagedThreadId;
			}
		}

		private JobScheduler _scheduler;

		[TearDown]
		public void TearDown()
		{
			_scheduler?.Dispose();
			_scheduler = null;
		}

		private JobScheduler CreateAttached(int workers = 2)
		{
			_scheduler = new JobScheduler(new JobSchedulerOptions { WorkerCount = workers });
			_scheduler.AttachToPlayerLoop();
			return _scheduler;
		}

		private static int CountAttachedSystems()
		{
			PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
			int count = 0;

			foreach (PlayerLoopSystem phase in root.subSystemList)
			{
				if (phase.type != typeof(EarlyUpdate) || phase.subSystemList == null) continue;
				foreach (PlayerLoopSystem system in phase.subSystemList)
				{
					if (system.type == typeof(JobScheduler.PlayerLoopTick)) count++;
				}
			}

			return count;
		}

		[UnityTest]
		public IEnumerator Scheduled_InUpdate_ExecutesOffMain_AndCompletesOnMain_TwoFramesLater()
		{
			JobScheduler scheduler = CreateAttached();
			int          mainId    = Thread.CurrentThread.ManagedThreadId;
			var          job       = new FrameStampJob();

			int scheduledFrame = Time.frameCount;
			scheduler.Schedule(job);

			yield return null;
			yield return null;

			Assert.AreEqual(1, job.ExecuteCount);
			Assert.AreNotEqual(mainId, job.ExecuteThreadId);
			Assert.AreEqual(scheduledFrame + 1, job.ExecuteCtxFrame, "handed off at the top of the following frame");

			Assert.AreEqual(1, job.CompleteCount);
			Assert.AreEqual(mainId, job.CompleteThreadId);
			Assert.AreEqual(scheduledFrame + 2, job.CompleteFrame, "collected at the barrier that opens the frame after that");
		}

		[UnityTest]
		public IEnumerator WithoutWorkers_RunsOnMainAtTheNextFrame_AndCompletesTwoFramesLater()
		{
			JobScheduler scheduler = CreateAttached(workers: 0);
			int          mainId    = Thread.CurrentThread.ManagedThreadId;
			var          job       = new FrameStampJob();

			int scheduledFrame = Time.frameCount;
			scheduler.Schedule(job);

			yield return null;
			yield return null;

			Assert.AreEqual(1, job.ExecuteCount);
			Assert.AreEqual(mainId, job.ExecuteThreadId, "no workers: the barrier runs it on the main thread");
			Assert.AreEqual(scheduledFrame + 1, job.ExecuteCtxFrame, "run by the barrier at the top of the following frame");

			Assert.AreEqual(1, job.CompleteCount);
			Assert.AreEqual(scheduledFrame + 2, job.CompleteFrame, "collected a frame later, as with workers");
		}

		[UnityTest]
		public IEnumerator Repeating_TickCadence_MatchesFrameCount_Over60Frames()
		{
			JobScheduler scheduler = CreateAttached();
			var          job       = new FrameStampJob();

			scheduler.ScheduleRepeating(job);
			int firstRunFrame = Time.frameCount + 1;

			for (int i = 0; i < 60; i++) yield return null;

			scheduler.WaitForIdle();
			int framesKicked = Time.frameCount - firstRunFrame + 1;

			Assert.AreEqual(framesKicked, job.ExecuteCount, "one execution per frame, none skipped, none doubled");
			Assert.AreEqual(Time.frameCount, job.ExecuteCtxFrame);
			Assert.AreEqual(0, scheduler.Stats.SkippedFrames);
		}

		[UnityTest]
		public IEnumerator Dispose_RemovesThePlayerLoopSystem()
		{
			int before = CountAttachedSystems();

			JobScheduler scheduler = CreateAttached();
			Assert.AreEqual(before + 1, CountAttachedSystems());
			Assert.IsTrue(scheduler.IsAttachedToPlayerLoop);

			scheduler.AttachToPlayerLoop();
			Assert.AreEqual(before + 1, CountAttachedSystems(), "attach is idempotent");

			yield return null;

			scheduler.Dispose();
			Assert.AreEqual(before, CountAttachedSystems());
			Assert.IsFalse(scheduler.IsAttachedToPlayerLoop);

			yield return null;
			yield return null;
		}

		[UnityTest]
		public IEnumerator Batch_DrivenByThePlayerLoop_DeliversResultsTwoFramesLater()
		{
			JobScheduler scheduler = CreateAttached(workers: 4);
			var          batch     = new JobBatch<SquareElement>(capacity: 256);
			scheduler.Register(batch);

			for (int i = 0; i < 256; i++) batch.Add().Input = i;

			yield return null;
			Assert.AreEqual(0, batch.Results.Length, "still executing during this frame");

			yield return null;
			Assert.AreEqual(256, batch.Results.Length);
			for (int i = 0; i < 256; i++) Assert.AreEqual(i * i, batch.Results[i].Output);
		}

		private struct SquareElement : IFrameJob
		{
			public int Input;
			public int Output;

			public void Execute(in FrameContext ctx) => Output = Input * Input;
		}
	}
}
