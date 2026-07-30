using System;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Utilities.Jobs
{
    public sealed partial class JobDispatcher
    {
        internal class WorkerThreadDispatcher : ThreadDispatcherBase, IWorkerThreadDispatcher
        {
            private bool _isRunning  = true;
            private bool _isStopping = false;

            private int _frameCounter = -1;

            internal Thread WorkerThread { get; }

            internal AutoResetEvent   MainThreadFrameStartedSignal { get; } = new(true);
            private  ManualResetEvent _shutdownEvent = new ManualResetEvent(false);

            public WorkerThreadDispatcher(JobDispatcher jobDispatcher) : base(jobDispatcher)
            {
                WorkerThread = new Thread(StartThread)
                {
                    Name = "WorkerThreadDispatcher",
                    IsBackground = true
                };
                WorkerThread.Start();
            }

            private void StartThread()
            {
                while (_isRunning)
                {
                    // Tracks whether this iteration owes the frame barrier a signal. If a user job
                    // throws and we skip the signal, the main thread blocks on the barrier forever.
                    bool oweBarrierSignal = false;

                    try
                    {
                        // Wait for either frame start OR shutdown signal
                        int waitResult = WaitHandle.WaitAny(
                                                            new WaitHandle[] { MainThreadFrameStartedSignal, _shutdownEvent },
                                                            Timeout.Infinite);

                        if (waitResult == 1) // Shutdown event signaled
                        {
                            Debug.Log("WorkerThread: Shutdown signal received");
                            break;
                        }

                        if (!_isRunning || _isStopping)
                        {
                            break;
                        }

                        oweBarrierSignal = true;
                        _frameCounter++;

                        FrameStarted();
                    }
                    catch (ThreadAbortException)
                    {
                        Debug.LogWarning("WorkerThread: Thread abort requested");
                        break;
                    }
                    catch (Exception e)
                    {
                        // Contain the failure: log and keep the thread alive. A dead worker
                        // thread wedges the whole game on the frame barrier.
                        Debug.LogError($"Exception in Worker thread: {e.Message}\n{e.StackTrace}");
                    }
                    finally
                    {
                        if (oweBarrierSignal)
                        {
                            _instance._threadsBarrierJoinEvent.Signal();
                        }
                    }
                }

                Debug.Log("WorkerThread: Thread exiting");
            }

            internal void SignalShutdown()
            {
                _isRunning = false;
                _isStopping = true;
                _shutdownEvent.Set();
                MainThreadFrameStartedSignal.Set(); // Wake up if waiting
            }

            internal bool WaitForShutdown(int timeoutMs)
            {
                return WorkerThread.Join(timeoutMs);
            }

            internal void Abort()
            {
                try
                {
                    WorkerThread.Abort();
                }
                catch (PlatformNotSupportedException)
                {
                    // Some platforms don't support Thread.Abort()
                    Debug.LogWarning("Thread.Abort() not supported on this platform");
                }
            }

            protected override void CallCompleteOnJob(in IInternalJob job)
            {
                if (job.ShouldCallCompleteOnMainThread())
                {
                    _instance._unityThreadDispatcher.MainThreadActionsQueue.Enqueue(job);
                }
                else
                {
                    job.Job?.OnComplete();
                }
            }

            protected override void OnFrameStarted()
            {
                // The worker thread has no real FixedUpdate tick; its "FixedUpdate" jobs run once
                // per frame, after the regular frame work.
                ExecutePersistentFixedUpdateJobs();
            }

            protected override void InjectFrameDelayedJobIntoBuffer(FrameDelayedJobImpl jobImpl)
            {
                if (jobImpl.IsMarkedForExecution() || jobImpl.WillExecuteInNextFrame())
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
                if (timeDelayedJobImpl.IsMarkedForExecution() || timeDelayedJobImpl.WillExecuteInNextFrame())
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
                if (repeatingJobImpl.IsMarkedForExecution() || JobDispatcher.UnityTime >= repeatingJobImpl.ExecutionTime + repeatingJobImpl.ExecutionInterval)
                {
                    repeatingJobImpl.MarkForExecution();
                    repeatingJobImpl.AdvanceExecutionTime();
                    NextFrameJobs.TimeRepeatingJobs.Add(repeatingJobImpl);
                }

                HandlerBackBuffer.TimeRepeatingJobs.Add(repeatingJobImpl);
            }

            protected override void InjectUpdateJobIntoBuffer(FixedJobImpl fixedJobImpl)
            {
                NextFrameJobs.UpdateJobs.Add(fixedJobImpl);
                HandlerBackBuffer.UpdateJobs.Add(fixedJobImpl);
            }

            protected override void InjectLateUpdateJobIntoBuffer(FixedJobImpl fixedJobImpl)
            {
                NextFrameJobs.LateUpdateJobs.Add(fixedJobImpl);
                HandlerBackBuffer.LateUpdateJobs.Add(fixedJobImpl);
            }

            protected override void InjectFixedUpdateJobIntoBuffer(FixedJobImpl fixedJobImpl)
            {
                // FixedUpdate jobs live in a persistent list (see ThreadDispatcherBase) - the
                // transient frame buffers are cleared at frame boundaries and can't represent them.
                AddPersistentFixedUpdateJob(fixedJobImpl);
            }

            public void ExecuteJob(IDispatchableJob job, bool callCompleteOnMainThread = false)
            {
                DispatchTimeDelayedJob(job, 0f, callCompleteOnMainThread);
            }

            public void ExecuteJobInNextFrame(IDispatchableJob job, bool callCompleteOnMainThread = false)
            {
                DispatchFrameDelayedJob(job, 1, callCompleteOnMainThread);
            }

            public IDispatchedJobHandle ExecuteJobAtFrame(IDispatchableJob job, int frame, bool callCompleteOnMainThread = false)
            {
                return DispatchFrameDelayedJob(job, frame, callCompleteOnMainThread);
            }

            public IDispatchedJobHandle ExecuteJobAfterDelay(IDispatchableJob job, float delayInSeconds, bool callCompleteOnMainThread = false)
            {
                return DispatchTimeDelayedJob(job, delayInSeconds, callCompleteOnMainThread);
            }

            public IDispatchedJobHandle ExecuteRepeatingJob(IDispatchableJob job, float startingDelaySeconds, float repeatAfterSeconds,
                                                            bool callCompleteOnMainThread = false)
            {
                return DispatchTimeRepeatingJob(job, startingDelaySeconds, repeatAfterSeconds, callCompleteOnMainThread);
            }

            public IDispatchedJobHandle ExecuteJobEveryUpdate(IDispatchableJob job, bool callCompleteOnMainThread = false)
            {
                return DispatchUpdateJob(job, callCompleteOnMainThread);
            }

            public IDispatchedJobHandle ExecuteJobEveryLateUpdate(IDispatchableJob job, bool callCompleteOnMainThread = false)
            {
                return DispatchLateUpdateJob(job, callCompleteOnMainThread);
            }

            public IDispatchedJobHandle ExecuteJobEveryFixedUpdate(IDispatchableJob job, bool callCompleteOnMainThread = false)
            {
                return DispatchFixedUpdateJob(job, callCompleteOnMainThread);
            }

            public void Execute(Action job, Action onComplete = null, bool callCompleteOnMainThread = false)
            {
                DispatchTimeDelayedActions(job, onComplete, null, 0, callCompleteOnMainThread);
            }

            public void ExecuteAfterDelay(Action job, float delayInSeconds, Action onComplete = null, bool callCompleteOnMainThread = false)
            {
                DispatchTimeDelayedActions(job, onComplete, null, delayInSeconds, callCompleteOnMainThread);
            }

            public void ExecuteInNextFrame(Action job, Action onComplete = null, bool callCompleteOnMainThread = false)
            {
                DispatchFrameDelayedActions(job, onComplete, null, 1, callCompleteOnMainThread);
            }

            public void ExecuteAtFrame(Action job, uint frame, Action onComplete = null, bool callCompleteOnMainThread = false)
            {
                DispatchFrameDelayedActions(job, onComplete, null, frame, callCompleteOnMainThread);
            }

            public IDispatchedJobHandle InvokeRepeating(Action job, float startingDelaySeconds, float repeatIntervalSeconds, Action onComplete = null,
                                                        bool callCompleteOnMainThread = false)
            {
                return DispatchTimeRepeatingActions(job, onComplete, null, startingDelaySeconds, repeatIntervalSeconds, callCompleteOnMainThread);
            }

            public IDispatchedJobHandle ExecuteEveryUpdate(Action job, Action onStop = null, Action onComplete = null, bool callCompleteOnMainThread = false)
            {
                return DispatchUpdateJobActions(job, onComplete, onStop, callCompleteOnMainThread);
            }

            public IDispatchedJobHandle ExecuteEveryLateUpdate(Action job, Action onStop = null, Action onComplete = null,
                                                               bool callCompleteOnMainThread = false)
            {
                return DispatchLateUpdateJobActions(job, onComplete, onStop, callCompleteOnMainThread);
            }

            public IDispatchedJobHandle ExecuteEveryFixedUpdate(Action job, Action onStop = null, Action onComplete = null,
                                                                bool callCompleteOnMainThread = false)
            {
                return DispatchFixedUpdateJobActions(job, onComplete, onStop, callCompleteOnMainThread);
            }

            public override void Dispose()
            {
                SignalShutdown(); // Ensure thread is signaled
                base.Dispose();
                MainThreadFrameStartedSignal?.Dispose();
                _shutdownEvent?.Dispose();
            }
        }
    }
}