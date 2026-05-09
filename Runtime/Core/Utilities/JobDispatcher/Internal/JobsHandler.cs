using System;
using System.Collections.Generic;
using System.Threading;
using AK.Utilities.DataStructures;
using UnityEngine;
using UnityEngine.Profiling;

namespace Utilities.Jobs
{
    public sealed partial class JobDispatcher
    {
        public class JobsHandler : IDisposable
        {
            internal Thread _jobsHandlerThread;

            private readonly ThreadDispatcherBase _unityThreadDispatcher;
            private readonly ThreadDispatcherBase _workerThreadDispatcher;

            private JobsBackLog _unityJobsBacklog  = new();
            private JobsBackLog _workerJobsBacklog = new();

            private bool _isRunning  = true;
            private bool _isStopping = false;

            private CustomSampler _profilerSampler;
            private int           _frameCounter = -1;

            internal AutoResetEvent MainThreadFrameStartedSignal { get; } = new(true);

            private ManualResetEvent _shutdownEvent = new ManualResetEvent(false);

            internal JobsHandler(ThreadDispatcherBase unityThreadDispatcher, ThreadDispatcherBase workerThreadDispatcher)
            {
                _unityThreadDispatcher = unityThreadDispatcher;
                _workerThreadDispatcher = workerThreadDispatcher;

                _jobsHandlerThread = new Thread(PrepareBufferForNextFrame)
                {
                    Name = "JobsHandlerThread"
                };

                _jobsHandlerThread.Start();
                _jobsHandlerThread.IsBackground = true;
                _profilerSampler = CustomSampler.Create("JobsHandlerProfilerSampler");
            }

            private void PrepareBufferForNextFrame()
            {
                while (_isRunning)
                {
                    try
                    {
                        Profiler.BeginThreadProfiling("Jobs Threads", "JobsHandlerThread");
                        _profilerSampler.Begin();

                        // Wait for either frame start OR shutdown signal
                        int waitResult = WaitHandle.WaitAny(
                                                            new WaitHandle[] { MainThreadFrameStartedSignal, _shutdownEvent },
                                                            Timeout.Infinite);

                        if (waitResult == 1) // Shutdown event signaled
                        {
                            Debug.Log("JobsHandler: Shutdown signal received");
                            break;
                        }

                        if (!_isRunning || _isStopping)
                        {
                            break;
                        }

                        _frameCounter++;
                        if (!_isStopping)
                        {
                            DispatchJobsForMainThread();
                            DispatchJobsForWorkerThread();
                            _instance._threadsBarrierJoinEvent.Signal();
                        }

                        _profilerSampler.End();
                        Profiler.EndThreadProfiling();
                    }
                    catch (ThreadAbortException)
                    {
                        Debug.LogWarning("JobsHandler: Thread abort requested");
                        _profilerSampler.End();
                        Profiler.EndThreadProfiling();
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Exception in Handler thread: {ex.Message}\n{ex.StackTrace}");
                    }
                }

                Debug.Log("JobsHandler: Thread exiting");
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
                return _jobsHandlerThread.Join(timeoutMs);
            }

            internal void Abort()
            {
                try
                {
                    _jobsHandlerThread.Abort();
                }
                catch (PlatformNotSupportedException)
                {
                    // Some platforms don't support Thread.Abort()
                    Debug.LogWarning("Thread.Abort() not supported on this platform");
                }
            }

            private void DispatchJobsForMainThread()
            {
                PrepareFixedJobs(ref _unityThreadDispatcher.HandlerFrontBuffer.UpdateJobs,
                                 ref _unityThreadDispatcher.BackBuffer.UpdateJobs, ref _unityJobsBacklog.UpdateJobs);

                PrepareFixedJobs(ref _unityThreadDispatcher.HandlerFrontBuffer.LateUpdateJobs,
                                 ref _unityThreadDispatcher.BackBuffer.LateUpdateJobs, ref _unityJobsBacklog.LateUpdateJobs);

                // PrepareFixedJobs(ref _unityThreadDispatcher.HandlerFrontBuffer.FixedUpdateJobs,
                //                  ref _unityThreadDispatcher.DispatchedBackBuffer.FixedUpdateJobs, ref _unityJobsBacklog.FixedUpdateJobs);

                ProcessFrameDelayedJobs(ref _unityThreadDispatcher.HandlerFrontBuffer.FrameDelayedJobs,
                                        ref _unityThreadDispatcher.BackBuffer.FrameDelayedJobs,
                                        ref _unityJobsBacklog.FrameDelayedJobQueue);

                ProcessTimeDelayedJobs(ref _unityThreadDispatcher.HandlerFrontBuffer.TimeDelayedJobs,
                                       ref _unityThreadDispatcher.BackBuffer.TimeDelayedJobs,
                                       ref _unityJobsBacklog.TimeDelayedJobsQueue);

                ProcessTimeRepeatingJobs(ref _unityThreadDispatcher.HandlerFrontBuffer.TimeRepeatingJobs,
                                         ref _unityThreadDispatcher.BackBuffer.TimeRepeatingJobs,
                                         ref _unityJobsBacklog.TimeRepeatedJobsQueue);
            }

            private void DispatchJobsForWorkerThread()
            {
                PrepareFixedJobs(ref _workerThreadDispatcher.HandlerFrontBuffer.UpdateJobs,
                                 ref _workerThreadDispatcher.BackBuffer.UpdateJobs, ref _workerJobsBacklog.UpdateJobs);

                PrepareFixedJobs(ref _workerThreadDispatcher.HandlerFrontBuffer.LateUpdateJobs,
                                 ref _workerThreadDispatcher.BackBuffer.LateUpdateJobs, ref _workerJobsBacklog.LateUpdateJobs);

                // PrepareFixedJobs(ref _workerThreadDispatcher.HandlerFrontBuffer.FixedUpdateJobs,
                //                  ref _workerThreadDispatcher.DispatchedBackBuffer.FixedUpdateJobs, ref _workerJobsBacklog.FixedUpdateJobs);

                ProcessFrameDelayedJobs(ref _workerThreadDispatcher.HandlerFrontBuffer.FrameDelayedJobs,
                                        ref _workerThreadDispatcher.BackBuffer.FrameDelayedJobs,
                                        ref _workerJobsBacklog.FrameDelayedJobQueue);

                ProcessTimeDelayedJobs(ref _workerThreadDispatcher.HandlerFrontBuffer.TimeDelayedJobs,
                                       ref _workerThreadDispatcher.BackBuffer.TimeDelayedJobs,
                                       ref _workerJobsBacklog.TimeDelayedJobsQueue);

                ProcessTimeRepeatingJobs(ref _workerThreadDispatcher.HandlerFrontBuffer.TimeRepeatingJobs,
                                         ref _workerThreadDispatcher.BackBuffer.TimeRepeatingJobs,
                                         ref _workerJobsBacklog.TimeRepeatedJobsQueue);
            }

            private void PrepareFixedJobs<T>(ref List<T> handlerFrontBufferJobs, ref List<T> threadBackBuffer, ref HashSet<T> backlog)
                where T : IInternalJob
            {
                foreach (T fixedJob in handlerFrontBufferJobs)
                {
                    backlog.Add(fixedJob);
                }

                backlog.RemoveWhere(x => x.IsMarkedForCancellation());
                foreach (T fixedJob in backlog)
                {
                    fixedJob.MarkForExecution();
                    threadBackBuffer.Add(fixedJob);
                }
            }

            private void ProcessFrameDelayedJobs(ref List<FrameDelayedJobImpl> handlerFrontBuffer,
                                                 ref List<FrameDelayedJobImpl> threadBackBuffer,
                                                 ref PriorityQueue<FrameDelayedJobImpl, uint> backlog)
            {
                foreach (FrameDelayedJobImpl job in handlerFrontBuffer)
                {
                    if (!job.IsMarkedForExecution() && !job.IsMarkedForCancellation())
                    {
                        backlog.Enqueue(job, job.ExecutionFrame);
                    }
                }

                while (backlog.Count > 0 && backlog.Peek().WillExecuteInNextFrame())
                {
                    FrameDelayedJobImpl job = backlog.Dequeue();
                    if (!job.IsMarkedForCancellation())
                    {
                        job.MarkForExecution();
                    }

                    threadBackBuffer.Add(job);
                }
            }

            private void ProcessTimeDelayedJobs(ref List<TimeDelayedJobImpl> frontBuffer,
                                                ref List<TimeDelayedJobImpl> backBuffer,
                                                ref PriorityQueue<TimeDelayedJobImpl, float> backlog)
            {
                foreach (TimeDelayedJobImpl job in frontBuffer)
                {
                    if (!job.IsMarkedForExecution() && !job.IsMarkedForCancellation())
                    {
                        backlog.Enqueue(job, job.ExecutionTime);
                    }
                }

                while (backlog.Count > 0 && backlog.Peek().WillExecuteInNextFrame())
                {
                    TimeDelayedJobImpl job = backlog.Dequeue();
                    if (!job.IsMarkedForCancellation())
                    {
                        job.MarkForExecution();
                    }

                    backBuffer.Add(job);
                }
            }

            private void ProcessTimeRepeatingJobs(ref List<TimeRepeatingJobImpl> frontBuffer,
                                                  ref List<TimeRepeatingJobImpl> backBuffer,
                                                  ref PriorityQueue<TimeRepeatingJobImpl, float> backlog)
            {
                foreach (TimeRepeatingJobImpl job in frontBuffer)
                {
                    if (!job.IsMarkedForCancellation())
                    {
                        backlog.Enqueue(job, job.ExecutionTime);
                    }
                }

                while (backlog.Count > 0)
                {
                    TimeRepeatingJobImpl nextJob = backlog.Peek();
                    if (nextJob.WillExecuteInNextFrame())
                    {
                        if (nextJob.IsMarkedForExecution())
                        {
                            break;
                        }

                        TimeRepeatingJobImpl job = backlog.Dequeue();
                        if (!job.IsMarkedForCancellation())
                        {
                            job.MarkForExecution();
                            job.AdvanceExecutionTime();
                            backBuffer.Add(job);
                            backlog.Enqueue(job, job.ExecutionTime);
                        }

                        continue;
                    }

                    break;
                }
            }

            public void Dispose()
            {
                // Clear backlogs to release job references
                _unityJobsBacklog.ClearBuffer();
                _workerJobsBacklog.ClearBuffer();

                // Clean up wait handles
                MainThreadFrameStartedSignal?.Dispose();
                _shutdownEvent?.Dispose();
            }
        }
    }
}