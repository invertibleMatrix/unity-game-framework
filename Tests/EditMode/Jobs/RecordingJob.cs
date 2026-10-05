using System;
using System.Threading;
using AK.Jobs;

namespace AK.Tests.Jobs
{
	/// <summary>Test job that records where and how often it ran, and can block inside Execute until released.</summary>
	internal sealed class RecordingJob : IFrameJob, IFrameJobCallback
	{
		public int  ExecuteCount;
		public int  ExecuteThreadId;
		public int  ExecuteFrame;
		public int  CompleteCount;
		public bool CompletedCancelled;
		public int  CompleteThreadId;

		public ManualResetEventSlim        Gate;
		public Action                      OnExecuteAction;
		public Action<RecordingJob, bool>  OnCompleteAction;

		public void Execute(in FrameContext ctx)
		{
			Interlocked.Increment(ref ExecuteCount);
			ExecuteThreadId = Thread.CurrentThread.ManagedThreadId;
			ExecuteFrame    = ctx.Frame;
			Gate?.Wait();
			OnExecuteAction?.Invoke();
		}

		public void OnComplete(bool cancelled)
		{
			CompleteCount++;
			CompletedCancelled = cancelled;
			CompleteThreadId   = Thread.CurrentThread.ManagedThreadId;
			OnCompleteAction?.Invoke(this, cancelled);
		}
	}

	/// <summary>Job without a callback, for allocation and throughput tests.</summary>
	internal sealed class CountingJob : IFrameJob
	{
		public int Count;

		public void Execute(in FrameContext ctx)
		{
			Interlocked.Increment(ref Count);
		}
	}
}
