using System;
using System.Collections;
using System.Collections.Generic;
using Utilities.Jobs;
using UnityEngine;

namespace Utilities.JobDispatcher.Tests
{
    /// <summary>
    /// Stress tests for JobDispatcher to validate system stability under extreme conditions
    /// </summary>
    public class JobDispatcherStressTests : MonoBehaviour
    {
        private IJobDispatcher _jobDispatcher;

        private List<IDispatchedJobHandle> _stressTestHandles = new();

        [Header("Stress Test Configuration")]
        public int JobCount = 10000;

        public int  ConcurrentThreads     = 5;
        public bool RunStressTestsOnStart = false;

        void Start()
        {
            if (RunStressTestsOnStart)
            {
                StartCoroutine(RunStressTests());
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                StartCoroutine(RunStressTests());
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                CancelStressTests();
            }
        }

        private IEnumerator RunStressTests()
        {
            Debug.Log("Starting JobDispatcher stress tests...");

            yield return StartCoroutine(TestMassiveJobCreation());
            yield return new WaitForSeconds(1f);

            yield return StartCoroutine(TestRapidJobCancellation());
            yield return new WaitForSeconds(1f);

            yield return StartCoroutine(TestMixedJobTypes());
            yield return new WaitForSeconds(2f);

            yield return StartCoroutine(TestMemoryPressure());
            yield return new WaitForSeconds(1f);

            Debug.Log("Stress tests completed!");
        }

        private IEnumerator TestMassiveJobCreation()
        {
            Debug.Log($"Testing massive job creation ({JobCount} jobs)...");

            int completedJobs = 0;
            float startTime = Time.time;

            // Create massive number of jobs
            for (int i = 0; i < JobCount; i++)
            {
                _jobDispatcher.WorkerThread.ExecuteJob(new StressTestJob(i, onComplete: () => completedJobs++, null));
            }

            yield return new WaitUntil(() => completedJobs == JobCount);

            float totalTime = Time.time - startTime;
            Debug.Log($"Massive job creation test completed in {totalTime:F3}s");
            Debug.Log($"Throughput: {(JobCount / totalTime):F0} jobs/second");
        }

        private IEnumerator TestRapidJobCancellation()
        {
            Debug.Log("Testing rapid job cancellation...");

            int createdJobs = 0;
            int cancelledJobs = 0;

            // Create jobs and cancel them rapidly
            for (int i = 0; i < 1000; i++)
            {
                var handle = _jobDispatcher.WorkerThread.ExecuteJobAfterDelay(new StressTestJob(i, null, () => cancelledJobs++), 1f);

                _stressTestHandles.Add(handle);
                createdJobs++;

                // Cancel every other job immediately
                if (i % 2 == 0)
                {
                    handle.CancelJob();
                }
            }

            yield return new WaitForSeconds(0.5f);

            Debug.Log($"Created: {createdJobs}, Cancelled: {cancelledJobs}");
        }

        private IEnumerator TestMixedJobTypes()
        {
            Debug.Log("Testing mixed job types under stress...");

            int immediateCount = 0;
            int delayedCount = 0;
            int repeatingCount = 0;

            // Mix of different job types
            for (int i = 0; i < 1000; i++)
            {
                switch (i % 3)
                {
                    case 0:
                        _jobDispatcher.WorkerThread.ExecuteJob(new StressTestJob(i, onComplete: () => immediateCount++, null));
                        break;
                    case 1:
                        _jobDispatcher.WorkerThread.ExecuteJobAfterDelay(new StressTestJob(i, onComplete: () => delayedCount++, null),
                                                                         UnityEngine.Random.Range(0.1f, 0.5f));
                        break;
                    case 2:
                        var handle = _jobDispatcher.UnityThread.ExecuteRepeatingJob(new StressTestJob(i, null, null),
                                                                                    UnityEngine.Random.Range(0f, 0.2f), 0.1f);
                        _stressTestHandles.Add(handle);
                        repeatingCount++;
                        break;
                }
            }

            yield return new WaitForSeconds(1f);

            // Cancel repeating jobs
            foreach (var handle in _stressTestHandles)
            {
                handle.CancelJob();
            }

            _stressTestHandles.Clear();

            Debug.Log($"Immediate: {immediateCount}, Delayed: {delayedCount}, Repeating: {repeatingCount}");
        }

        private IEnumerator TestMemoryPressure()
        {
            Debug.Log("Testing memory pressure...");

            long initialMemory = GC.GetTotalMemory(false);

            // Create and destroy many jobs rapidly
            for (int iteration = 0; iteration < 10; iteration++)
            {
                var handles = new List<IDispatchedJobHandle>();

                for (int i = 0; i < 1000; i++)
                {
                    var handle = _jobDispatcher.WorkerThread.ExecuteJobEveryUpdate(new StressTestJob(i, null, null));
                    handles.Add(handle);
                }

                yield return new WaitForSeconds(0.1f);

                // Cancel all jobs
                foreach (var handle in handles)
                {
                    handle.CancelJob();
                }

                yield return new WaitForSeconds(0.1f);
            }

            long finalMemory = GC.GetTotalMemory(false);
            long memoryIncrease = finalMemory - initialMemory;

            Debug.Log($"Memory increase: {memoryIncrease / 1024f:F2} KB");

            // Force garbage collection to see if memory is properly cleaned up
            GC.Collect();
            yield return new WaitForSeconds(0.1f);

            long afterGCMemory = GC.GetTotalMemory(false);
            Debug.Log($"Memory after GC: {(afterGCMemory - initialMemory) / 1024f:F2} KB");
        }

        private void CancelStressTests()
        {
            Debug.Log("Cancelling all stress test jobs...");

            foreach (var handle in _stressTestHandles)
            {
                handle?.CancelJob();
            }

            _stressTestHandles.Clear();
        }

        private class StressTestJob : IDispatchableJob
        {
            private readonly int    _jobId;
            private          Action _onComplete;
            private          Action _onStop;

            public StressTestJob(int jobId, Action onComplete, Action onStop)
            {
                _jobId = jobId;
                _onComplete = onComplete;
                _onStop = onStop;
            }

            public void OnExecute()
            {
                // Simulate some computational work
                float result = 0;
                for (int i = 0; i < 50; i++)
                {
                    result += Mathf.Sin(i * _jobId * 0.001f);
                }
            }

            public void OnComplete()
            {
                _onComplete?.Invoke();
            }

            public void OnStop()
            {
                _onStop?.Invoke();
            }
        }
    }
}