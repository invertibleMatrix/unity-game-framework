using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Profiling;
using Utilities.Jobs.AnonymousImpl;

namespace Utilities.Jobs
{
    public sealed partial class JobDispatcher
    {
        internal abstract class ThreadDispatcherBase : IDisposable
        {
            private readonly JobsBuffer _jobsBuffer1 = new();
            private readonly JobsBuffer _jobsBuffer2 = new();

            private readonly JobsBuffer _handlerBuffer1 = new();
            private readonly JobsBuffer _handlerBuffer2 = new();

            protected JobDispatcher _jobDispatcher;

            internal readonly JobsBuffer CurrentFrameJobs = new();
            internal readonly JobsBuffer NextFrameJobs    = new();

            internal JobsBuffer FrontBuffer;
            internal JobsBuffer BackBuffer;

            internal JobsBuffer HandlerFrontBuffer;
            internal JobsBuffer HandlerBackBuffer;

            internal ThreadDispatcherBase(JobDispatcher jobDispatcher)
            {
                _jobDispatcher = jobDispatcher;
                _jobsBuffer1.ID = 1;
                _jobsBuffer2.ID = 2;

                _handlerBuffer1.ID = 1;
                _handlerBuffer2.ID = 2;

                FrontBuffer = _jobsBuffer1;
                BackBuffer = _jobsBuffer2;

                HandlerFrontBuffer = _handlerBuffer1;
                HandlerBackBuffer = _handlerBuffer2;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            protected virtual void CallCompleteOnJob(in IInternalJob job)
            {
                job.Job.OnComplete();
            }

            internal void FrameStarted()
            {
                ExecuteRepeatingJobs(FrontBuffer.UpdateJobs);
                ExecuteRepeatingJobs(FrontBuffer.LateUpdateJobs);
                ExecuteRepeatingJobs(FrontBuffer.TimeRepeatingJobs);
                ExecuteTimedJobs(FrontBuffer.FrameDelayedJobs);
                ExecuteTimedJobs(FrontBuffer.TimeDelayedJobs);
                FrontBuffer.ClearBuffer();

                ExecuteRepeatingJobs(NextFrameJobs.UpdateJobs);
                ExecuteRepeatingJobs(NextFrameJobs.LateUpdateJobs);
                ExecuteRepeatingJobs(NextFrameJobs.TimeRepeatingJobs);
                ExecuteTimedJobs(NextFrameJobs.FrameDelayedJobs);
                ExecuteTimedJobs(NextFrameJobs.TimeDelayedJobs);
                NextFrameJobs.ClearBuffer();

                OnFrameStarted();
            }

            /// <summary>
            /// Called at the end of <see cref="FrameStarted"/>. The worker thread uses this to run
            /// its persistent FixedUpdate jobs once per frame (it has no real FixedUpdate tick).
            /// </summary>
            protected virtual void OnFrameStarted() { }

            // FixedUpdate jobs are persistent (run every FixedUpdate until cancelled), so they
            // cannot live in the transient frame buffers - those are cleared at frame boundaries.
            private readonly List<FixedJobImpl> _persistentFixedUpdateJobs = new();
            private readonly object             _fixedUpdateJobsLock = new();

            protected void AddPersistentFixedUpdateJob(FixedJobImpl job)
            {
                lock (_fixedUpdateJobsLock)
                {
                    _persistentFixedUpdateJobs.Add(job);
                }
            }

            protected void ExecutePersistentFixedUpdateJobs()
            {
                lock (_fixedUpdateJobsLock)
                {
                    for (int i = _persistentFixedUpdateJobs.Count - 1; i >= 0; i--)
                    {
                        var job = _persistentFixedUpdateJobs[i];

                        if (job.IsMarkedForCancellation() || job.Job == null)
                        {
                            _persistentFixedUpdateJobs.RemoveAt(i);
                            continue;
                        }

                        try
                        {
                            job.Job.OnExecute();
                            CallCompleteOnJob(job);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
            }

            private void SwapThreadBuffers()
            {
                if (FrontBuffer == _jobsBuffer1)
                {
                    FrontBuffer = _jobsBuffer2;
                    BackBuffer = _jobsBuffer1;
                }
                else if (FrontBuffer == _jobsBuffer2)
                {
                    FrontBuffer = _jobsBuffer1;
                    BackBuffer = _jobsBuffer2;
                }
            }

            private void SwapHandlerBuffers()
            {
                if (HandlerFrontBuffer == _handlerBuffer1)
                {
                    HandlerFrontBuffer = _handlerBuffer2;
                    HandlerBackBuffer = _handlerBuffer1;
                }
                else if (HandlerFrontBuffer == _handlerBuffer2)
                {
                    HandlerFrontBuffer = _handlerBuffer1;
                    HandlerBackBuffer = _handlerBuffer2;
                }
            }

            internal void SwapBuffers()
            {
                SwapThreadBuffers();
                SwapHandlerBuffers();
            }

            internal void ExecuteRepeatingJobs<T>(IReadOnlyList<T> jobs) where T : IInternalJob
            {
                if (jobs.Count > 0)
                {
                    Profiler.BeginSample("ExecuteRepeatingJobs");
                    for (int i = 0; i < jobs.Count; i++)
                    {
                        T job = jobs[i];
                        if (job.IsMarkedForExecution() && !job.IsMarkedForCancellation())
                        {
                            job.ClearMarkForExecution();
                            ExecuteJobIsolated(job);
                        }
                    }

                    Profiler.EndSample();
                }
            }

            internal void ExecuteTimedJobs<T>(IReadOnlyList<T> jobs) where T : IInternalJob
            {
                if (jobs.Count > 0)
                {
                    Profiler.BeginSample("ExecuteOneTimeJobs");
                    foreach (T job in jobs)
                    {
                        if (job.IsMarkedForExecution())
                        {
                            ExecuteJobIsolated(job);
                        }
                    }

                    Profiler.EndSample();
                }
            }

            /// <summary>
            /// Executes one job with exception isolation: a throwing job must not kill the
            /// dispatch loop (a dead worker thread wedges the frame barrier) nor skip the
            /// remaining jobs. Job can be nulled cross-thread by CancelJob, so capture first.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ExecuteJobIsolated<T>(T job) where T : IInternalJob
            {
                var userJob = job.Job;
                if (userJob == null) return;

                try
                {
                    userJob.OnExecute();
                    CallCompleteOnJob(job);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            protected IDispatchedJobHandle DispatchUpdateJob(IDispatchableJob job, bool callCompleteOnMainThread)
            {
                FixedJobImpl fixedJobImpl = new(job, callCompleteOnMainThread);
                InjectUpdateJobIntoBuffer(fixedJobImpl);
                return fixedJobImpl;
            }

            protected IDispatchedJobHandle DispatchLateUpdateJob(IDispatchableJob job, bool callCompleteOnMainThread)
            {
                FixedJobImpl fixedJobImpl = new(job, callCompleteOnMainThread);
                InjectLateUpdateJobIntoBuffer(fixedJobImpl);
                return fixedJobImpl;
            }

            protected IDispatchedJobHandle DispatchFixedUpdateJob(IDispatchableJob job, bool callCompleteOnMainThread)
            {
                FixedJobImpl fixedJobImpl = new(job, callCompleteOnMainThread);
                InjectFixedUpdateJobIntoBuffer(fixedJobImpl);
                return fixedJobImpl;
            }

            protected IDispatchedJobHandle DispatchFrameDelayedJob(IDispatchableJob job, int frame, bool callCompleteOnMainThread)
            {
                FrameDelayedJobImpl frameDelayedJobImpl = new(job, (uint)frame, callCompleteOnMainThread);
                InjectFrameDelayedJobIntoBuffer(frameDelayedJobImpl);
                return frameDelayedJobImpl;
            }

            protected IDispatchedJobHandle DispatchTimeDelayedJob(IDispatchableJob job, float delayInSeconds, bool callCompleteOnMainThread)
            {
                TimeDelayedJobImpl timeDelayedJobImpl = new(job, delayInSeconds, callCompleteOnMainThread);
                InjectTimeDelayedJobIntoBuffer(timeDelayedJobImpl);
                return timeDelayedJobImpl;
            }

            protected IDispatchedJobHandle DispatchTimeRepeatingJob(IDispatchableJob job, float startingDelaySeconds,
                                                                    float repeatAfterSeconds,
                                                                    bool callCompleteOnMainThread)
            {
                TimeRepeatingJobImpl repeatingJobImpl = new(job, startingDelaySeconds, repeatAfterSeconds, callCompleteOnMainThread);
                InjectTimeRepeatingJobIntoBuffer(repeatingJobImpl);
                return repeatingJobImpl;
            }

            protected TimeDelayedJobImpl DispatchTimeDelayedActions(Action job, Action onComplete, Action onStop,
                                                                    float startingDelayInSeconds,
                                                                    bool callCompleteOnMainThread)
            {
                InternalDispatchableJob j = new(job, onComplete, onStop);
                TimeDelayedJobImpl timeDelayedJobImpl = new(j, startingDelayInSeconds, callCompleteOnMainThread);
                InjectTimeDelayedJobIntoBuffer(timeDelayedJobImpl);
                return timeDelayedJobImpl;
            }

            protected FrameDelayedJobImpl DispatchFrameDelayedActions(Action job, Action onComplete, Action onStop, uint frame,
                                                                      bool callCompleteOnMainThread)
            {
                InternalDispatchableJob j = new(job, onComplete, onStop);
                FrameDelayedJobImpl frameDelayedJobImpl = new(j, frame, callCompleteOnMainThread);
                InjectFrameDelayedJobIntoBuffer(frameDelayedJobImpl);
                return frameDelayedJobImpl;
            }

            protected TimeRepeatingJobImpl DispatchTimeRepeatingActions(Action job, Action onComplete, Action onStop,
                                                                        float startingDelaySeconds,
                                                                        float repeatAfterSeconds,
                                                                        bool callCompleteOnMainThread)
            {
                InternalDispatchableJob j = new(job, onComplete, onStop);
                TimeRepeatingJobImpl timeRepeatingJobImpl = new(j, startingDelaySeconds, repeatAfterSeconds, callCompleteOnMainThread);
                InjectTimeRepeatingJobIntoBuffer(timeRepeatingJobImpl);
                return timeRepeatingJobImpl;
            }

            protected IDispatchedJobHandle DispatchUpdateJobActions(Action job, Action onComplete, Action onStop,
                                                                    bool callCompleteOnMainThread)

            {
                InternalDispatchableJob j = new(job, onComplete, onStop);
                FixedJobImpl fixedJobImpl = new(j, callCompleteOnMainThread);
                InjectUpdateJobIntoBuffer(fixedJobImpl);
                return fixedJobImpl;
            }

            protected IDispatchedJobHandle DispatchLateUpdateJobActions(Action job, Action onComplete, Action onStop,
                                                                        bool callCompleteOnMainThread)
            {
                InternalDispatchableJob j = new(job, onComplete, onStop);
                FixedJobImpl fixedJobImpl = new(j, callCompleteOnMainThread);
                InjectLateUpdateJobIntoBuffer(fixedJobImpl);
                return fixedJobImpl;
            }

            protected IDispatchedJobHandle DispatchFixedUpdateJobActions(Action job, Action onComplete, Action onStop,
                                                                         bool callCompleteOnMainThread)
            {
                InternalDispatchableJob j = new(job, onComplete, onStop);
                FixedJobImpl fixedJobImpl = new(j, callCompleteOnMainThread);
                InjectFixedUpdateJobIntoBuffer(fixedJobImpl);
                return fixedJobImpl;
            }

            public virtual void Dispose()
            {
                lock (_fixedUpdateJobsLock)
                {
                    _persistentFixedUpdateJobs.Clear();
                }

                lock (FrontBuffer)
                {
                    FrontBuffer.ClearBuffer();
                }

                lock (BackBuffer)
                {
                    BackBuffer.ClearBuffer();
                }

                lock (HandlerFrontBuffer)
                {
                    HandlerFrontBuffer.ClearBuffer();
                }

                lock (HandlerBackBuffer)
                {
                    HandlerBackBuffer.ClearBuffer();
                }
            }

            protected abstract void InjectFrameDelayedJobIntoBuffer(FrameDelayedJobImpl jobImpl);
            protected abstract void InjectTimeDelayedJobIntoBuffer(TimeDelayedJobImpl timeDelayedJobImpl);
            protected abstract void InjectTimeRepeatingJobIntoBuffer(TimeRepeatingJobImpl jobImpl);
            protected abstract void InjectUpdateJobIntoBuffer(FixedJobImpl fixedJobImpl);
            protected abstract void InjectLateUpdateJobIntoBuffer(FixedJobImpl fixedJobImpl);
            protected abstract void InjectFixedUpdateJobIntoBuffer(FixedJobImpl fixedJobImpl);
        }
    }
}