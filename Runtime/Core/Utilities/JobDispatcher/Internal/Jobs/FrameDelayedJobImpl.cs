using System.Runtime.CompilerServices;

namespace Utilities.Jobs
{
    internal class FrameDelayedJobImpl : IInternalJob, IDispatchedJobHandle
    {
        public   uint             Traits { get; set; }
        public   IDispatchableJob Job    { get; set; }
        internal uint             ExecutionFrame;

        public FrameDelayedJobImpl(IDispatchableJob job, uint executionFrame, bool executeCompleteOnMainThread = true)
        {
            Job = job;
            ExecutionFrame = JobDispatcher.FrameCounter + executionFrame;
            Traits = 0;

            if (executeCompleteOnMainThread)
            {
                this.SetExecuteCompleteOnMainThread();
            }

            if (JobDispatcher.FrameCounter >= ExecutionFrame)
            {
                this.MarkForExecution();
            }
        }

        public void CancelJob()
        {
            if (Job != null)
            {
                this.ClearMarkForExecution();
                this.MarkForCancellation();
                Job.OnStop();
                Job = null; // CRITICAL: Release reference
            }
        }

        internal bool WillExecuteInNextFrame()
        {
            return JobDispatcher.FrameCounter + 1 >= ExecutionFrame;
        }
    }
}