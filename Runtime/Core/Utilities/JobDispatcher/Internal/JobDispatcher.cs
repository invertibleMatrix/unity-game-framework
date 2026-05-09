using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;

namespace Utilities.Jobs
{
    [DefaultExecutionOrder(1)]
    public sealed partial class JobDispatcher : MonoBehaviour, IJobDispatcher, IDisposable
    {
        private static JobDispatcher _instance;

        private JobsHandler    _jobsHandler;
        private CountdownEvent _threadsBarrierJoinEvent;

        private UnityThreadDispatcher  _unityThreadDispatcher;
        private WorkerThreadDispatcher _workerThreadDispatcher;

        public static uint  FrameCounter = 0;
        public static float UnityTime;
        public static float RealTime;
        public static float Dt;
        public static float FixedDt;

        public IUnityThreadDispatcher  UnityThread  => _unityThreadDispatcher;
        public IWorkerThreadDispatcher WorkerThread => _workerThreadDispatcher;

        // Shutdown state tracking
        private volatile bool _isShuttingDown        = false;
        private const    int  THREAD_JOIN_TIMEOUT_MS = 2000; // 2 second timeout

        public static JobDispatcher Construct()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject obj = new GameObject("JobDispatcher");
            _instance = obj.AddComponent<JobDispatcher>();
            _instance.InitInternal();
            obj.AddComponent<ThreadSpinner>().Init(_instance);
            DontDestroyOnLoad(_instance);
            return _instance;
        }

        private void OnEnable()
        {
            // This handles the case where the object persists after a domain reload in the editor.
            // The static _instance will be null, but the MonoBehaviour instance still exists.
            if (_instance == null) _instance = this;
        }

        private void InitInternal()
        {
            _threadsBarrierJoinEvent = new CountdownEvent(2);
            _unityThreadDispatcher = new UnityThreadDispatcher(this);
            _workerThreadDispatcher = new WorkerThreadDispatcher(this);
            _jobsHandler = new JobsHandler(_unityThreadDispatcher, _workerThreadDispatcher);
        }

        private void OnDestroy()
        {
            // OnDestroy is called when exiting play mode or when the object is destroyed.
            // This is a crucial cleanup point.
            Dispose();
        }

        private void OnApplicationQuit()
        {
            // OnApplicationQuit ensures cleanup happens when the application/editor is closed entirely.
            Dispose();
        }

#if UNITY_EDITOR
        private void OnDisable()
        {
            // Editor-specific cleanup for domain reloads
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Dispose();
            }
        }

        private void OnValidate()
        {
            // Detect if this is a resurrected instance after domain reload
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("Duplicate JobDispatcher detected - destroying new instance");
                DestroyImmediate(gameObject);
            }
        }
#endif

        public void Dispose()
        {
            // Prevent double-disposal and re-entrance
            if (_isShuttingDown || _jobsHandler == null) return;

            _isShuttingDown = true;
            Debug.Log("JobDispatcher: Initiating graceful shutdown...");

            // Signal shutdown to all threads first
            _jobsHandler.SignalShutdown();
            _workerThreadDispatcher.SignalShutdown();

            // Wait for threads with timeout - this prevents debugger deadlock
            if (!_jobsHandler.WaitForShutdown(THREAD_JOIN_TIMEOUT_MS))
            {
                Debug.LogWarning("JobsHandler thread failed to shutdown gracefully within timeout - forcing abort");
                _jobsHandler.Abort();
            }

            if (!_workerThreadDispatcher.WaitForShutdown(THREAD_JOIN_TIMEOUT_MS))
            {
                Debug.LogWarning("Worker thread failed to shutdown gracefully within timeout - forcing abort");
                _workerThreadDispatcher.Abort();
            }

            // Now dispose resources
            _jobsHandler?.Dispose();
            _unityThreadDispatcher?.Dispose();
            _workerThreadDispatcher?.Dispose();

            _threadsBarrierJoinEvent?.Dispose();

            // Clear references to allow GC and prevent re-disposal
            _jobsHandler = null;
            _unityThreadDispatcher = null;
            _workerThreadDispatcher = null;
            _instance = null;

            Debug.Log("JobDispatcher: Shutdown complete");
        }

        internal void FrameStarted()
        {
            // Skip if shutting down
            if (_isShuttingDown) return;

            Profiler.BeginSample("WaitingForJobsHandler");
            _threadsBarrierJoinEvent.Wait();
            _threadsBarrierJoinEvent.Reset(2);
            Profiler.EndSample();

            _workerThreadDispatcher.HandlerFrontBuffer.ClearBuffer();
            _unityThreadDispatcher.HandlerFrontBuffer.ClearBuffer();

            _workerThreadDispatcher.SwapBuffers();
            _unityThreadDispatcher.SwapBuffers();

            UnityTime = Time.time;
            RealTime = Time.unscaledTime;
            Dt = Time.deltaTime;
            FrameCounter++;

            _jobsHandler.MainThreadFrameStartedSignal.Set();
            _workerThreadDispatcher.MainThreadFrameStartedSignal.Set();

            _unityThreadDispatcher.FrameStarted();
        }

        private void LateUpdate()
        {
            if (_isShuttingDown) return;
            _unityThreadDispatcher.Update();
            _unityThreadDispatcher.LateUpdate();
        }

        private void FixedUpdate()
        {
            if (_isShuttingDown) return;
            FixedDt = Time.fixedDeltaTime;
            _unityThreadDispatcher.FixedUpdate();
        }
    }

    [DefaultExecutionOrder(-10000)]
    internal sealed class ThreadSpinner : MonoBehaviour
    {
        private JobDispatcher _jobDispatcher;

        public void Init(JobDispatcher jobDispatcher)
        {
            _jobDispatcher = jobDispatcher;
        }

        private void Update()
        {
            _jobDispatcher.FrameStarted();
        }
    }
}