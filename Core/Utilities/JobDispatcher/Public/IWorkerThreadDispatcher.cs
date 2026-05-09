using System;
using System.Threading;

namespace Utilities.Jobs
{
    public interface IWorkerThreadDispatcher
    {
        public void Execute(Action job, Action onComplete = null, bool callCompleteOnMainThread = false);
        public void ExecuteAfterDelay(Action job, float delayInSeconds, Action onComplete = null, bool callCompleteOnMainThread = false);
        public void ExecuteInNextFrame(Action job, Action onComplete = null, bool callCompleteOnMainThread = false);
        public void ExecuteAtFrame(Action job, uint frame, Action onComplete = null, bool callCompleteOnMainThread = false);

        public IDispatchedJobHandle InvokeRepeating(Action job, float startingDelaySeconds, float repeatIntervalSeconds, Action onComplete = null,
                                                    bool callCompleteOnMainThread = false);

        public IDispatchedJobHandle ExecuteEveryUpdate(Action job, Action onStop = null, Action onComplete = null, bool callCompleteOnMainThread = false);
        public IDispatchedJobHandle ExecuteEveryLateUpdate(Action job, Action onStop = null, Action onComplete = null, bool callCompleteOnMainThread = false);
        public IDispatchedJobHandle ExecuteEveryFixedUpdate(Action job, Action onStop = null, Action onComplete = null, bool callCompleteOnMainThread = false);

        public void ExecuteJob(IDispatchableJob job, bool callCompleteOnMainThread = false);
        public void ExecuteJobInNextFrame(IDispatchableJob job, bool callCompleteOnMainThread = false);
        public IDispatchedJobHandle ExecuteJobAtFrame(IDispatchableJob job, int frame, bool callCompleteOnMainThread = false);
        public IDispatchedJobHandle ExecuteJobAfterDelay(IDispatchableJob job, float delayInSeconds, bool callCompleteOnMainThread = false);

        public IDispatchedJobHandle ExecuteRepeatingJob(IDispatchableJob job, float startingDelaySeconds, float repeatAfterSeconds,
                                                        bool callCompleteOnMainThread = false);

        public IDispatchedJobHandle ExecuteJobEveryUpdate(IDispatchableJob job, bool callCompleteOnMainThread = false);
        public IDispatchedJobHandle ExecuteJobEveryLateUpdate(IDispatchableJob job, bool callCompleteOnMainThread = false);
        public IDispatchedJobHandle ExecuteJobEveryFixedUpdate(IDispatchableJob job, bool callCompleteOnMainThread = false);
    }
}