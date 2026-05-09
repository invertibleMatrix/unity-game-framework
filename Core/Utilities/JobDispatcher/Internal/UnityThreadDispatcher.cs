using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace Utilities.Jobs
{
    public sealed partial class JobDispatcher
    {
        internal class UnityThreadDispatcher : ThreadDispatcherBase, IUnityThreadDispatcher
        {
            private readonly  List<CoroutineJobImpl>        _coroutineJobs         = new(100);
            internal readonly ConcurrentQueue<IInternalJob> MainThreadActionsQueue = new();

            public UnityThreadDispatcher(JobDispatcher jobDispatcher) : base(jobDispatcher) { }
            internal void Update() { }

            internal void LateUpdate()
            {
                // Skip if shutting down
                if (_jobDispatcher._isShuttingDown) return;

                ExecuteRepeatingJobs(CurrentFrameJobs.UpdateJobs);
                ExecuteRepeatingJobs(CurrentFrameJobs.LateUpdateJobs);
                ExecuteRepeatingJobs(CurrentFrameJobs.TimeRepeatingJobs);
                ExecuteTimedJobs(CurrentFrameJobs.FrameDelayedJobs);
                ExecuteTimedJobs(CurrentFrameJobs.TimeDelayedJobs);
                CurrentFrameJobs.ClearBuffer();

                ExecuteCoroutineJobs();

                // Process main thread actions
                while (MainThreadActionsQueue.Count > 0)
                {
                    if (MainThreadActionsQueue.TryDequeue(out IInternalJob job))
                    {
                        try
                        {
                            job.Job.OnComplete();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error in main thread action: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                }
            }

            internal void FixedUpdate() { }

            public IDispatchedJobHandle ExecuteCoroutineJob(ICoroutineJob job)
            {
                CoroutineJobImpl coroutineJobImpl = new CoroutineJobImpl(_jobDispatcher, job);
                _coroutineJobs.Add(coroutineJobImpl);
                return coroutineJobImpl;
            }

            public override void Dispose()
            {
                // Stop all running coroutines
                foreach (CoroutineJobImpl job in _coroutineJobs)
                {
                    if (job.CoroutineHandle != null)
                    {
                        job.CancelJob();
                    }
                }

                _coroutineJobs.Clear();

                // Clear any pending main thread actions
                while (MainThreadActionsQueue.TryDequeue(out _)) { }

                base.Dispose();
                // MainThreadActionsQueue?.Dispose();
            }

            private void ExecuteCoroutineJobs()
            {
                if (_coroutineJobs.Count > 0)
                {
                    foreach (CoroutineJobImpl job in _coroutineJobs)
                    {
                        if (job.CoroutineHandle == null && !_jobDispatcher._isShuttingDown)
                        {
                            job.Execute();
                        }
                    }

                    _coroutineJobs.Clear();
                }
            }

            protected override void InjectFrameDelayedJobIntoBuffer(FrameDelayedJobImpl jobImpl)
            {
                if (jobImpl.IsMarkedForExecution())
                {
                    CurrentFrameJobs.FrameDelayedJobs.Add(jobImpl);
                }
                else if (jobImpl.WillExecuteInNextFrame())
                {
                    jobImpl.MarkForExecution();
                    NextFrameJobs.FrameDelayedJobs.Add(jobImpl);
                }
                else
                {
                    HandlerBackBuffer.FrameDelayedJobs.Add(jobImpl);
                }
            }

            protected override void InjectTimeDelayedJobIntoBuffer(TimeDelayedJobImpl timeDelayedJobImpl)
            {
                if (timeDelayedJobImpl.IsMarkedForExecution())
                {
                    CurrentFrameJobs.TimeDelayedJobs.Add(timeDelayedJobImpl);
                }

                else if (timeDelayedJobImpl.WillExecuteInNextFrame())
                {
                    timeDelayedJobImpl.MarkForExecution();
                    NextFrameJobs.TimeDelayedJobs.Add(timeDelayedJobImpl);
                }
                else
                {
                    HandlerBackBuffer.TimeDelayedJobs.Add(timeDelayedJobImpl);
                }
            }

            protected override void InjectTimeRepeatingJobIntoBuffer(TimeRepeatingJobImpl repeatingJobImpl)
            {
                if (repeatingJobImpl.IsMarkedForExecution())
                {
                    CurrentFrameJobs.TimeRepeatingJobs.Add(repeatingJobImpl);
                    repeatingJobImpl.AdvanceExecutionTime();
                }

                if (JobDispatcher.UnityTime >= repeatingJobImpl.ExecutionTime + repeatingJobImpl.ExecutionInterval)
                {
                    repeatingJobImpl.MarkForExecution();
                    repeatingJobImpl.AdvanceExecutionTime();
                    NextFrameJobs.TimeRepeatingJobs.Add(repeatingJobImpl);
                }

                HandlerBackBuffer.TimeRepeatingJobs.Add(repeatingJobImpl);
            }

            protected override void InjectUpdateJobIntoBuffer(FixedJobImpl fixedJobImpl)
            {
                CurrentFrameJobs.UpdateJobs.Add(fixedJobImpl);
                NextFrameJobs.UpdateJobs.Add(fixedJobImpl);
                HandlerBackBuffer.UpdateJobs.Add(fixedJobImpl);
            }

            protected override void InjectLateUpdateJobIntoBuffer(FixedJobImpl fixedJobImpl)
            {
                CurrentFrameJobs.LateUpdateJobs.Add(fixedJobImpl);
                NextFrameJobs.LateUpdateJobs.Add(fixedJobImpl);
                HandlerBackBuffer.LateUpdateJobs.Add(fixedJobImpl);
            }

            protected override void InjectFixedUpdateJobIntoBuffer(FixedJobImpl fixedJobImpl)
            {
                CurrentFrameJobs.FixedUpdateJobs.Add(fixedJobImpl);
                NextFrameJobs.FixedUpdateJobs.Add(fixedJobImpl);
                HandlerBackBuffer.FixedUpdateJobs.Add(fixedJobImpl);
            }

            public void ExecuteJob(IDispatchableJob job)
            {
                DispatchTimeDelayedJob(job, 0f, true);
            }

            public void ExecuteJobInNextFrame(IDispatchableJob job)
            {
                DispatchFrameDelayedJob(job, 1, true);
            }

            public IDispatchedJobHandle ExecuteJobAtFrame(IDispatchableJob job, int frame)
            {
                return DispatchFrameDelayedJob(job, frame, true);
            }

            public IDispatchedJobHandle ExecuteJobAfterDelay(IDispatchableJob job, float delayInSeconds)
            {
                return DispatchTimeDelayedJob(job, delayInSeconds, true);
            }

            public IDispatchedJobHandle ExecuteRepeatingJob(IDispatchableJob job, float startingDelaySeconds, float repeatAfterSeconds)
            {
                return DispatchTimeRepeatingJob(job, startingDelaySeconds, repeatAfterSeconds, true);
            }

            public void Execute(Action job, Action onComplete = null)
            {
                DispatchTimeDelayedActions(job, onComplete, null, 0, true);
            }

            public void ExecuteAfterDelay(Action job, float delayInSeconds)
            {
                DispatchTimeDelayedActions(job, null, null, delayInSeconds, true);
            }

            public void ExecuteInNextFrame(Action job)
            {
                DispatchFrameDelayedActions(job, null, null, 1, true);
            }

            public void ExecuteAtFrame(Action job, uint frame)
            {
                DispatchFrameDelayedActions(job, null, null, frame, true);
            }

            public IDispatchedJobHandle InvokeRepeating(Action job, float startingDelaySeconds, float repeatIntervalSeconds)
            {
                return DispatchTimeRepeatingActions(job, null, null, startingDelaySeconds, repeatIntervalSeconds, true);
            }

            public IDispatchedJobHandle ExecuteJobEveryUpdate(IDispatchableJob job)
            {
                return DispatchUpdateJob(job, true);
            }

            public IDispatchedJobHandle ExecuteJobEveryLateUpdate(IDispatchableJob job)
            {
                return DispatchLateUpdateJob(job, true);
            }

            public IDispatchedJobHandle ExecuteJobEveryFixedUpdate(IDispatchableJob job)
            {
                return DispatchFixedUpdateJob(job, true);
            }

            public IDispatchedJobHandle ExecuteEveryUpdate(Action job, Action onStop = null)
            {
                return DispatchUpdateJobActions(job, null, onStop, true);
            }

            public IDispatchedJobHandle ExecuteEveryLateUpdate(Action job, Action onStop = null)
            {
                return DispatchLateUpdateJobActions(job, null, onStop, true);
            }

            public IDispatchedJobHandle ExecuteEveryFixedUpdate(Action job, Action onStop = null)
            {
                return DispatchFixedUpdateJobActions(job, null, onStop, true);
            }
        }
    }
}