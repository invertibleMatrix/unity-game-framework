using System;
using System.Collections;

namespace Utilities.Jobs
{
    public interface IUnityThreadDispatcher
    {
        public void Execute(Action job, Action onComplete = null);
        public void ExecuteAfterDelay(Action job, float delayInSeconds);
        public void ExecuteInNextFrame(Action job);
        public void ExecuteAtFrame(Action job, uint frame);
        public IDispatchedJobHandle InvokeRepeating(Action job, float startingDelaySeconds, float repeatIntervalSeconds);
        public IDispatchedJobHandle ExecuteEveryUpdate(Action job, Action onStop = null);
        public IDispatchedJobHandle ExecuteEveryLateUpdate(Action job, Action onStop = null);
        public IDispatchedJobHandle ExecuteEveryFixedUpdate(Action job, Action onStop = null);

        public void ExecuteJob(IDispatchableJob job);
        public void ExecuteJobInNextFrame(IDispatchableJob job);
        public IDispatchedJobHandle ExecuteJobAtFrame(IDispatchableJob job, int frame);
        public IDispatchedJobHandle ExecuteJobAfterDelay(IDispatchableJob job, float delayInSeconds);
        public IDispatchedJobHandle ExecuteRepeatingJob(IDispatchableJob job, float startingDelaySeconds, float repeatAfterSeconds);
        public IDispatchedJobHandle ExecuteCoroutineJob(ICoroutineJob job);
        public IDispatchedJobHandle ExecuteJobEveryUpdate(IDispatchableJob job);
        public IDispatchedJobHandle ExecuteJobEveryLateUpdate(IDispatchableJob job);
        public IDispatchedJobHandle ExecuteJobEveryFixedUpdate(IDispatchableJob job);
    }
}