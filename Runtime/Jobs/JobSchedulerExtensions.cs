using System;

namespace AK.Jobs
{
	/// <summary>
	/// Delegate forms for fire-and-forget work. Each call allocates a small wrapper, so per-frame
	/// work should implement <see cref="IFrameJob"/> on a reused object or use a <see cref="JobBatch{TJob}"/>.
	/// </summary>
	public static class JobSchedulerExtensions
	{
		private sealed class DelegateJob : IFrameJob, IFrameJobCallback
		{
			private readonly Action<FrameContext> _execute;
			private readonly Action<bool>         _onComplete;

			public DelegateJob(Action<FrameContext> execute, Action<bool> onComplete)
			{
				_execute    = execute;
				_onComplete = onComplete;
			}

			public void Execute(in FrameContext ctx) => _execute(ctx);

			public void OnComplete(bool cancelled) => _onComplete?.Invoke(cancelled);
		}

		/// <summary>Runs <paramref name="execute"/> once on a worker next frame; <paramref name="onComplete"/> runs on the main thread at the barrier after.</summary>
		public static FrameJobHandle Schedule(this IJobScheduler scheduler, Action<FrameContext> execute, Action<bool> onComplete = null, int phase = 0)
		{
			if (execute == null) throw new ArgumentNullException(nameof(execute));
			return scheduler.Schedule(new DelegateJob(execute, onComplete), phase);
		}

		/// <summary>Runs <paramref name="execute"/> on a worker every frame until the handle is cancelled.</summary>
		public static FrameJobHandle ScheduleRepeating(this IJobScheduler scheduler, Action<FrameContext> execute, Action<bool> onComplete = null, int phase = 0)
		{
			if (execute == null) throw new ArgumentNullException(nameof(execute));
			return scheduler.ScheduleRepeating(new DelegateJob(execute, onComplete), phase);
		}
	}
}
