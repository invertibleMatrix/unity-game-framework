using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Utilities.Jobs;
using UnityEngine;
using UnityEngine.Assertions;

namespace Utilities.JobDispatcher.Tests
{
    /// <summary>
    /// Comprehensive test suite for JobDispatcher system
    /// Tests all job types, timing, thread affinity, and edge cases
    /// </summary>
    public class ComprehensiveJobDispatcherTests : MonoBehaviour
    {
        private IJobDispatcher             _jobDispatcher;
        private          List<IDispatchedJobHandle> _activeHandles = new();

        [Header("Test Configuration")]
        public bool EnableDetailedLogging = true;

        // Test tracking
        private Dictionary<string, TestResult> _testResults = new();
        private int                            _totalTests  = 0;
        private int                            _passedTests = 0;

        // Shared test data
        private static readonly object       TestLock        = new();
        private static          int          _sharedCounter  = 0;
        private static          List<string> _executionOrder = new();

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                StartCoroutine(RunAllTests());
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                CancelAllJobs();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                PrintTestResults();
            }
        }

        private IEnumerator RunAllTests()
        {
            Log("Starting comprehensive JobDispatcher tests...");
            ResetTestState();

            // Test immediate execution
            yield return StartCoroutine(TestImmediateExecution());
            yield return new WaitForSeconds(0.1f);

            // Test frame-delayed jobs
            yield return StartCoroutine(TestFrameDelayedJobs());
            yield return new WaitForSeconds(0.1f);

            // Test time-delayed jobs
            yield return StartCoroutine(TestTimeDelayedJobs());
            yield return new WaitForSeconds(0.5f);

            // Test repeating jobs
            yield return StartCoroutine(TestRepeatingJobs());
            yield return new WaitForSeconds(2f);

            // Test fixed jobs (Update/LateUpdate/FixedUpdate)
            yield return StartCoroutine(TestFixedJobs());
            yield return new WaitForSeconds(1f);

            // Test thread affinity
            yield return StartCoroutine(TestThreadAffinity());
            yield return new WaitForSeconds(0.5f);

            // Test job cancellation
            yield return StartCoroutine(TestJobCancellation());
            yield return new WaitForSeconds(0.5f);

            // Test coroutine jobs
            yield return StartCoroutine(TestCoroutineJobs());
            yield return new WaitForSeconds(2f);

            // Test edge cases
            yield return StartCoroutine(TestEdgeCases());
            yield return new WaitForSeconds(0.5f);

            // Test performance under load
            yield return StartCoroutine(TestPerformanceUnderLoad());
            yield return new WaitForSeconds(1f);

            PrintTestResults();
        }

        #region Immediate Execution Tests
        private IEnumerator TestImmediateExecution()
        {
            Log("Testing immediate execution...");

            // Test Unity thread immediate execution
            bool unityExecuted = false;
            bool unityCompleted = false;

            _jobDispatcher.UnityThread.ExecuteJob(new TestJob(
                                                              onExecute: () =>
                                                              {
                                                                  unityExecuted = true;
                                                                  Assert.AreEqual(Thread.CurrentThread.ManagedThreadId, 1,
                                                                                  "Unity job should execute on main thread");
                                                              },
                                                              onComplete: () =>
                                                              {
                                                                  unityCompleted = true;
                                                              }));

            yield return new WaitUntil(() => unityExecuted && unityCompleted);
            RecordTest("Unity Thread Immediate Execution", unityExecuted && unityCompleted);

            // Test Worker thread immediate execution
            bool workerExecuted = false;
            bool workerCompleted = false;
            int workerThreadId = -1;

            _jobDispatcher.WorkerThread.ExecuteJob(new TestJob(
                                                               onExecute: () =>
                                                               {
                                                                   workerExecuted = true;
                                                                   workerThreadId = Thread.CurrentThread.ManagedThreadId;
                                                               },
                                                               onComplete: () =>
                                                               {
                                                                   workerCompleted = true;
                                                                   Assert.AreNotEqual(1, workerThreadId,
                                                                                      "Worker job should execute on different thread");
                                                               }));

            yield return new WaitUntil(() => workerExecuted && workerCompleted);
            RecordTest("Worker Thread Immediate Execution", workerExecuted && workerCompleted && workerThreadId != 1);
        }
        #endregion

        #region Frame-Delayed Jobs Tests
        private IEnumerator TestFrameDelayedJobs()
        {
            Log("Testing frame-delayed jobs...");

            uint currentFrame = IJobDispatcher.FrameCounter;
            uint targetFrame = currentFrame + 3;
            bool frameJobExecuted = false;

            _jobDispatcher.UnityThread.ExecuteJobAtFrame(new TestJob(
                                                                     onExecute: () =>
                                                                     {
                                                                         frameJobExecuted = true;
                                                                         Assert.IsTrue(IJobDispatcher.FrameCounter >= targetFrame,
                                                                                       $"Job should execute at or after frame {targetFrame}, but executed at {IJobDispatcher.FrameCounter}");
                                                                     }), (int)targetFrame);

            // Wait until target frame
            yield return new WaitUntil(() => IJobDispatcher.FrameCounter >= targetFrame);
            yield return new WaitUntil(() => frameJobExecuted);

            RecordTest("Frame-Delayed Job Execution", frameJobExecuted);

            // Test frame-delayed job cancellation
            bool cancelledJobExecuted = false;
            var handle = _jobDispatcher.UnityThread.ExecuteJobAtFrame(new TestJob(
                                                                                  onExecute: () => cancelledJobExecuted = true),
                                                                      (int)(currentFrame + 10));

            yield return new WaitForSeconds(0.1f); // Wait a bit then cancel
            handle.CancelJob();

            // Wait past the execution frame
            yield return new WaitUntil(() => IJobDispatcher.FrameCounter >= currentFrame + 12);

            RecordTest("Frame-Delayed Job Cancellation", !cancelledJobExecuted);
        }
        #endregion

        #region Time-Delayed Jobs Tests
        private IEnumerator TestTimeDelayedJobs()
        {
            Log("Testing time-delayed jobs...");

            float startTime = Time.time;
            float delay = 0.3f;
            bool timeJobExecuted = false;
            float actualExecutionTime = 0;

            _jobDispatcher.UnityThread.ExecuteJobAfterDelay(new TestJob(
                                                                        onExecute: () =>
                                                                        {
                                                                            timeJobExecuted = true;
                                                                            actualExecutionTime = Time.time;
                                                                        }), delay);

            yield return new WaitUntil(() => timeJobExecuted);

            float actualDelay = actualExecutionTime - startTime;
            bool timingCorrect = Mathf.Abs(actualDelay - delay) < 0.05f; // 50ms tolerance

            RecordTest("Time-Delayed Job Execution", timeJobExecuted && timingCorrect);

            if (EnableDetailedLogging)
            {
                Log($"Expected delay: {delay:F3}s, Actual delay: {actualDelay:F3}s");
            }
        }
        #endregion

        #region Repeating Jobs Tests
        private IEnumerator TestRepeatingJobs()
        {
            Log("Testing repeating jobs...");

            int executionCount = 0;
            float interval = 0.2f;
            float startTime = Time.time;
            List<float> executionTimes = new();

            var handle = _jobDispatcher.UnityThread.ExecuteRepeatingJob(new TestJob(
                                                                                    onExecute: () =>
                                                                                    {
                                                                                        executionCount++;
                                                                                        executionTimes.Add(Time.time - startTime);
                                                                                    }), 0.1f, interval);

            // Wait for several executions
            yield return new WaitUntil(() => executionCount >= 4);
            handle.CancelJob();

            // Verify timing intervals
            bool intervalsCorrect = true;
            for (int i = 1; i < executionTimes.Count; i++)
            {
                float intervalDiff = Mathf.Abs(executionTimes[i] - executionTimes[i - 1] - interval);
                if (intervalDiff > 0.05f) // 50ms tolerance
                {
                    intervalsCorrect = false;
                    break;
                }
            }

            RecordTest("Repeating Job Timing", executionCount >= 4 && intervalsCorrect);

            if (EnableDetailedLogging)
            {
                Log($"Repeating job executed {executionCount} times");
                for (int i = 0; i < executionTimes.Count; i++)
                {
                    Log($"Execution {i}: {executionTimes[i]:F3}s");
                }
            }
        }
        #endregion

        #region Fixed Jobs Tests
        private IEnumerator TestFixedJobs()
        {
            Log("Testing fixed jobs (Update/LateUpdate/FixedUpdate)...");

            int updateCount = 0;
            int lateUpdateCount = 0;
            int fixedUpdateCount = 0;

            var updateHandle = _jobDispatcher.UnityThread.ExecuteJobEveryUpdate(new TestJob(
                                                                                            onExecute: () => updateCount++));

            var lateUpdateHandle = _jobDispatcher.UnityThread.ExecuteJobEveryLateUpdate(new TestJob(
                                                                                             onExecute: () => lateUpdateCount++));

            var fixedUpdateHandle = _jobDispatcher.UnityThread.ExecuteJobEveryFixedUpdate(new TestJob(
                                                                                               onExecute: () => fixedUpdateCount++));

            // Wait for several frames
            uint startFrame = IJobDispatcher.FrameCounter;
            yield return new WaitUntil(() => IJobDispatcher.FrameCounter >= startFrame + 10);

            updateHandle.CancelJob();
            lateUpdateHandle.CancelJob();
            fixedUpdateHandle.CancelJob();

            // Verify execution counts
            bool updateCorrect = updateCount >= 10;
            bool lateUpdateCorrect = lateUpdateCount >= 10;
            bool fixedUpdateCorrect = fixedUpdateCount >= 10; // FixedUpdate may run more or less depending on physics settings

            RecordTest("Update Job Execution", updateCorrect);
            RecordTest("LateUpdate Job Execution", lateUpdateCorrect);
            RecordTest("FixedUpdate Job Execution", fixedUpdateCorrect);

            if (EnableDetailedLogging)
            {
                Log($"Update: {updateCount}, LateUpdate: {lateUpdateCount}, FixedUpdate: {fixedUpdateCount}");
            }
        }
        #endregion

        #region Thread Affinity Tests
        private IEnumerator TestThreadAffinity()
        {
            Log("Testing thread affinity...");

            // Test worker job with main thread completion
            bool workerExecuted = false;
            bool mainThreadCompleted = false;
            int workerThreadId = -1;
            int completionThreadId = -1;

            _jobDispatcher.WorkerThread.ExecuteJob(new TestJob(
                                                               onExecute: () =>
                                                               {
                                                                   workerExecuted = true;
                                                                   workerThreadId = Thread.CurrentThread.ManagedThreadId;
                                                               },
                                                               onComplete: () =>
                                                               {
                                                                   mainThreadCompleted = true;
                                                                   completionThreadId = Thread.CurrentThread.ManagedThreadId;
                                                               }), callCompleteOnMainThread: true);

            yield return new WaitUntil(() => workerExecuted && mainThreadCompleted);

            bool affinityCorrect = workerThreadId != 1 && completionThreadId == 1;
            RecordTest("Thread Affinity (Worker Execute, Main Complete)", affinityCorrect);

            // Test worker job with worker thread completion
            bool workerExecuted2 = false;
            bool workerCompleted2 = false;
            int workerThreadId2 = -1;
            int completionThreadId2 = -1;

            _jobDispatcher.WorkerThread.ExecuteJob(new TestJob(
                                                               onExecute: () =>
                                                               {
                                                                   workerExecuted2 = true;
                                                                   workerThreadId2 = Thread.CurrentThread.ManagedThreadId;
                                                               },
                                                               onComplete: () =>
                                                               {
                                                                   workerCompleted2 = true;
                                                                   completionThreadId2 = Thread.CurrentThread.ManagedThreadId;
                                                               }), callCompleteOnMainThread: false);

            yield return new WaitUntil(() => workerExecuted2 && workerCompleted2);

            bool affinityCorrect2 = workerThreadId2 != 1 && completionThreadId2 != 1;
            RecordTest("Thread Affinity (Worker Execute, Worker Complete)", affinityCorrect2);
        }
        #endregion

        #region Job Cancellation Tests
        private IEnumerator TestJobCancellation()
        {
            Log("Testing job cancellation...");

            // Test immediate job cancellation
            bool immediateJobExecuted = false;
            bool immediateJobStopped = false;

            var handle1 = _jobDispatcher.UnityThread.ExecuteJobAtFrame(new TestJob(
                                                                                   onExecute: () => immediateJobExecuted = true,
                                                                                   onStop: () => immediateJobStopped = true), 1);

            handle1.CancelJob();
            yield return null; // Allow one frame for cancellation to process

            RecordTest("Immediate Job Cancellation", !immediateJobExecuted && immediateJobStopped);

            // Test repeating job cancellation
            int repeatingExecutions = 0;
            bool repeatingJobStopped = false;

            var handle2 = _jobDispatcher.UnityThread.ExecuteRepeatingJob(new TestJob(
                                                                                     onExecute: () => repeatingExecutions++,
                                                                                     onStop: () => repeatingJobStopped = true), 0f, 0.1f);

            yield return new WaitUntil(() => repeatingExecutions >= 2);
            handle2.CancelJob();

            int finalCount = repeatingExecutions;
            yield return new WaitForSeconds(0.2f); // Wait to ensure no more executions

            RecordTest("Repeating Job Cancellation", repeatingJobStopped && repeatingExecutions == finalCount);
        }
        #endregion

        #region Coroutine Jobs Tests
        private IEnumerator TestCoroutineJobs()
        {
            Log("Testing coroutine jobs...");

            bool coroutineStarted = false;
            bool coroutineCompleted = false;
            int coroutineSteps = 0;

            _jobDispatcher.UnityThread.ExecuteCoroutineJob(new TestCoroutineJob(
                                                                                onExecute: () =>
                                                                                {
                                                                                    coroutineStarted = true;
                                                                                    return TestCoroutine();
                                                                                },
                                                                                onComplete: () => coroutineCompleted = true));

            IEnumerator TestCoroutine()
            {
                coroutineSteps++;
                yield return new WaitForSeconds(0.1f);
                coroutineSteps++;
                yield return new WaitForSeconds(0.1f);
                coroutineSteps++;
                yield return null;
                coroutineSteps++;
            }

            yield return new WaitUntil(() => coroutineCompleted);

            RecordTest("Coroutine Job Execution", coroutineStarted && coroutineCompleted && coroutineSteps == 4);

            // Test coroutine cancellation
            bool longCoroutineStarted = false;
            bool longCoroutineStopped = false;

            var handle = _jobDispatcher.UnityThread.ExecuteCoroutineJob(new TestCoroutineJob(
                                                                                             onExecute: () =>
                                                                                             {
                                                                                                 longCoroutineStarted = true;
                                                                                                 return LongCoroutine();
                                                                                             },
                                                                                             onStop: () => longCoroutineStopped = true));

            IEnumerator LongCoroutine()
            {
                yield return new WaitForSeconds(1f);
            }

            yield return new WaitForSeconds(0.1f);
            handle.CancelJob();

            RecordTest("Coroutine Job Cancellation", longCoroutineStarted && longCoroutineStopped);
        }
        #endregion

        #region Edge Cases Tests
        private IEnumerator TestEdgeCases()
        {
            Log("Testing edge cases...");

            // Test zero delay job
            bool zeroDelayExecuted = false;
            _jobDispatcher.UnityThread.ExecuteJobAfterDelay(new TestJob(
                                                                        onExecute: () => zeroDelayExecuted = true), 0f);

            yield return new WaitUntil(() => zeroDelayExecuted);
            RecordTest("Zero Delay Job", zeroDelayExecuted);

            // Test job execution in same frame
            uint frameBefore = IJobDispatcher.FrameCounter;
            bool sameFrameExecuted = false;

            _jobDispatcher.UnityThread.ExecuteJob(new TestJob(
                                                              onExecute: () =>
                                                              {
                                                                  sameFrameExecuted = true;
                                                                  Assert.AreEqual(frameBefore, IJobDispatcher.FrameCounter,
                                                                                  "Job should execute in same frame");
                                                              }));

            yield return null; // Wait one frame
            RecordTest("Same Frame Execution", sameFrameExecuted);

            // Test multiple jobs in same frame
            int multiJobCount = 0;
            for (int i = 0; i < 10; i++)
            {
                _jobDispatcher.UnityThread.ExecuteJob(new TestJob(
                                                                  onExecute: () => multiJobCount++));
            }

            yield return new WaitUntil(() => multiJobCount == 10);
            RecordTest("Multiple Jobs Same Frame", multiJobCount == 10);
        }
        #endregion

        #region Performance Tests
        private IEnumerator TestPerformanceUnderLoad()
        {
            Log("Testing performance under load...");

            int jobCount = 1000;
            int completedJobs = 0;
            float startTime = Time.time;

            // Dispatch many jobs
            for (int i = 0; i < jobCount; i++)
            {
                _jobDispatcher.WorkerThread.ExecuteJob(new TestJob(
                                                                   onExecute: () =>
                                                                   {
                                                                       // Simulate some work
                                                                       float result = 0;
                                                                       for (int j = 0; j < 100; j++)
                                                                       {
                                                                           result += Mathf.Sin(j * 0.1f);
                                                                       }
                                                                   },
                                                                   onComplete: () => completedJobs++));
            }

            yield return new WaitUntil(() => completedJobs == jobCount);

            float totalTime = Time.time - startTime;
            float jobsPerSecond = jobCount / totalTime;

            bool performanceAcceptable = jobsPerSecond > 1000; // Should handle at least 1000 jobs/sec

            RecordTest("Performance Under Load", performanceAcceptable);

            if (EnableDetailedLogging)
            {
                Log($"Processed {jobCount} jobs in {totalTime:F3}s ({jobsPerSecond:F0} jobs/sec)");
            }
        }
        #endregion

        #region Helper Classes and Methods
        private class TestJob : IDispatchableJob
        {
            private readonly System.Action _onExecute;
            private readonly System.Action _onComplete;
            private readonly System.Action _onStop;

            public TestJob(System.Action onExecute = null, System.Action onComplete = null, System.Action onStop = null)
            {
                _onExecute = onExecute;
                _onComplete = onComplete;
                _onStop = onStop;
            }

            public void OnExecute() => _onExecute?.Invoke();
            public void OnComplete() => _onComplete?.Invoke();
            public void OnStop() => _onStop?.Invoke();
        }

        private class TestCoroutineJob : ICoroutineJob
        {
            private readonly System.Func<IEnumerator> _onExecute;
            private readonly System.Action            _onComplete;
            private readonly System.Action            _onStop;

            public TestCoroutineJob(System.Func<IEnumerator> onExecute, System.Action onComplete = null, System.Action onStop = null)
            {
                _onExecute = onExecute;
                _onComplete = onComplete;
                _onStop = onStop;
            }

            public IEnumerator OnExecute() => _onExecute?.Invoke();
            public void OnComplete() => _onComplete?.Invoke();
            public void OnStop() => _onStop?.Invoke();
        }

        private void ResetTestState()
        {
            _testResults.Clear();
            _totalTests = 0;
            _passedTests = 0;
            _sharedCounter = 0;
            _executionOrder.Clear();
            CancelAllJobs();
        }

        private void CancelAllJobs()
        {
            foreach (var handle in _activeHandles)
            {
                handle?.CancelJob();
            }

            _activeHandles.Clear();
        }

        private void RecordTest(string testName, bool passed)
        {
            _totalTests++;
            if (passed) _passedTests++;

            _testResults[testName] = new TestResult
            {
                Name = testName,
                Passed = passed,
                Timestamp = Time.time
            };

            Log($"{(passed ? "PASS" : "FAIL")}: {testName}");
        }

        private void PrintTestResults()
        {
            Log("\n=== TEST RESULTS ===");
            Log($"Total Tests: {_totalTests}");
            Log($"Passed: {_passedTests}");
            Log($"Failed: {_totalTests - _passedTests}");
            Log($"Success Rate: {(_passedTests * 100f / _totalTests):F1}%");

            Log("\nDetailed Results:");
            foreach (var result in _testResults.Values)
            {
                Log($"  {(result.Passed ? "✓" : "✗")} {result.Name}");
            }

            Log("==================\n");
        }

        private void Log(string message)
        {
            if (EnableDetailedLogging)
            {
                Debug.Log($"[JobDispatcherTests] {message}");
            }
        }

        private struct TestResult
        {
            public string Name;
            public bool   Passed;
            public float  Timestamp;
        }
        #endregion
    }
}