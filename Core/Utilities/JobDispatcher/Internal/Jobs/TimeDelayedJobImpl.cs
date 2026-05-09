using System.Runtime.CompilerServices;

namespace Utilities.Jobs
{
    internal class TimeDelayedJobImpl : IInternalJob, IDispatchedJobHandle
    {
        public   uint             Traits { get; set; }
        public   IDispatchableJob Job    { get; set; }
        internal float            ExecutionTime;

        internal TimeDelayedJobImpl(IDispatchableJob job, float startingDelayInSecs, bool executeCompleteOnMainThread = false)
        {
            Job = job;
            ExecutionTime = JobDispatcher.UnityTime + startingDelayInSecs;
            Traits = 0;
            if (executeCompleteOnMainThread)
            {
                this.SetExecuteCompleteOnMainThread();
            }

            if (JobDispatcher.UnityTime >= ExecutionTime)
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
            return JobDispatcher.UnityTime + JobDispatcher.Dt >= ExecutionTime;
        }
    }
}