using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Utilities.Jobs
{
    internal class TimeRepeatingJobImpl : IInternalJob, IDispatchedJobHandle
    {
        public uint             Traits { get; set; }
        public IDispatchableJob Job    { get; set; }

        internal float ExecutionInterval;
        internal float ExecutionTime;

        internal TimeRepeatingJobImpl(IDispatchableJob job, float startingDelayInSecs, float repeatIntervalInSecs, bool executeCompleteOnMainThread = false)
        {
            Job = job;
            if (repeatIntervalInSecs < JobDispatcher.Dt)
            {
                repeatIntervalInSecs = JobDispatcher.Dt;
            }

            ExecutionInterval = repeatIntervalInSecs;
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

        internal void AdvanceExecutionTime()
        {
            ExecutionTime = JobDispatcher.UnityTime + ExecutionInterval;
        }

        internal bool WillExecuteInNextFrame()
        {
            return JobDispatcher.UnityTime + JobDispatcher.Dt >= ExecutionTime;
        }
    }
}