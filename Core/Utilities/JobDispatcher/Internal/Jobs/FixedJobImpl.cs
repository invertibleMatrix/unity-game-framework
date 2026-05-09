namespace Utilities.Jobs
{
    /// <summary>
    /// Update, LateUpdate and FixedUpdate are Fixed Job and will keep on running until stopped
    /// </summary>
    internal class FixedJobImpl : IInternalJob, IDispatchedJobHandle
    {
        public uint             Traits { get; set; }
        public IDispatchableJob Job    { get; set; }

        public FixedJobImpl(IDispatchableJob job, bool executeCompleteOnMainThread)
        {
            Job = job;
            Traits = 0;
            this.MarkForExecution();
            if (executeCompleteOnMainThread)
            {
                this.SetExecuteCompleteOnMainThread();
            }
        }

        public void CancelJob()
        {
            if (Job != null)
            {
                this.ClearMarkForExecution();
                this.MarkForCancellation();
                Job.OnStop();
                Job = null; // CRITICAL: Release reference to prevent memory leak
            }
        }
    }
}