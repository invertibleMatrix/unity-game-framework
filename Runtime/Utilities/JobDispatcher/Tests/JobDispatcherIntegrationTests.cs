using System;
using System.Collections;
using Utilities.Jobs;
using UnityEngine;

namespace Utilities.JobDispatcher.Tests
{
    /// <summary>
    /// Integration tests that validate JobDispatcher works correctly with Unity systems
    /// </summary>
    public class JobDispatcherIntegrationTests : MonoBehaviour
    {
        private IJobDispatcher _jobDispatcher;

        [Header("Integration Test Configuration")]
        public bool RunIntegrationTestsOnStart = false;

        void Start()
        {
            if (RunIntegrationTestsOnStart)
            {
                StartCoroutine(RunIntegrationTests());
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                StartCoroutine(RunIntegrationTests());
            }
        }

        private IEnumerator RunIntegrationTests()
        {
            Debug.Log("Starting JobDispatcher integration tests...");

            yield return StartCoroutine(TestUnityAPISafety());
            yield return new WaitForSeconds(0.5f);

            yield return StartCoroutine(TestGameObjectOperations());
            yield return new WaitForSeconds(0.5f);

            yield return StartCoroutine(TestCoroutineIntegration());
            yield return new WaitForSeconds(1f);

            yield return StartCoroutine(TestTimeSynchronization());
            yield return new WaitForSeconds(0.5f);

            Debug.Log("Integration tests completed!");
        }

        private IEnumerator TestUnityAPISafety()
        {
            Debug.Log("Testing Unity API safety...");

            bool mainThreadJobCompleted = false;
            bool workerThreadJobFailed = false;

            // Test that Unity API calls work on main thread
            _jobDispatcher.UnityThread.ExecuteJob(new UnityAPITestJob(
                                                                      onExecute: () =>
                                                                      {
                                                                          // This should work fine
                                                                          GameObject testObj = new GameObject("TestObject");
                                                                          UnityEngine.Object.DestroyImmediate(testObj);
                                                                      },
                                                                      onComplete: () => mainThreadJobCompleted = true));

            // Test that Unity API calls fail appropriately on worker thread
            _jobDispatcher.WorkerThread.ExecuteJob(new UnityAPITestJob(
                                                                       onExecute: () =>
                                                                       {
                                                                           try
                                                                           {
                                                                               GameObject testObj = new GameObject("TestObject");
                                                                               UnityEngine.Object.DestroyImmediate(testObj);
                                                                           }
                                                                           catch (Exception)
                                                                           {
                                                                               workerThreadJobFailed = true; // Expected to fail
                                                                           }
                                                                       }));

            yield return new WaitUntil(() => mainThreadJobCompleted);
            yield return new WaitForSeconds(0.1f); // Give worker thread time to execute

            Debug.Log($"Main thread Unity API: {(mainThreadJobCompleted ? "PASS" : "FAIL")}");
            Debug.Log($"Worker thread Unity API: {(workerThreadJobFailed ? "PASS" : "FAIL")}");
        }

        private IEnumerator TestGameObjectOperations()
        {
            Debug.Log("Testing GameObject operations...");

            GameObject testObject = new GameObject("IntegrationTestObject");
            var testComponent = testObject.AddComponent<IntegrationTestComponent>();

            bool jobCompleted = false;
            int initialValue = testComponent.TestValue;

            _jobDispatcher.UnityThread.ExecuteJob(new GameObjectTestJob(testComponent,
                                                                        onExecute: (component) =>
                                                                        {
                                                                            component.TestValue = initialValue + 42;
                                                                        },
                                                                        onComplete: () => jobCompleted = true));

            yield return new WaitUntil(() => jobCompleted);

            bool valueChanged = testComponent.TestValue == initialValue + 42;
            Debug.Log($"GameObject operation: {(valueChanged ? "PASS" : "FAIL")}");

            UnityEngine.Object.DestroyImmediate(testObject);
        }

        private IEnumerator TestCoroutineIntegration()
        {
            Debug.Log("Testing coroutine integration...");

            bool coroutineCompleted = false;
            int coroutineSteps = 0;

            _jobDispatcher.UnityThread.ExecuteCoroutineJob(new IntegrationCoroutineJob(
                                                                                       onExecute: () => TestCoroutine(),
                                                                                       onComplete: () => coroutineCompleted = true));

            IEnumerator TestCoroutine()
            {
                coroutineSteps++;
                yield return new WaitForSeconds(0.1f);
                coroutineSteps++;
                yield return new WaitForEndOfFrame();
                coroutineSteps++;
                yield return null;
                coroutineSteps++;
            }

            yield return new WaitUntil(() => coroutineCompleted);

            Debug.Log($"Coroutine integration: {(coroutineCompleted && coroutineSteps == 4 ? "PASS" : "FAIL")}");
        }

        private IEnumerator TestTimeSynchronization()
        {
            Debug.Log("Testing time synchronization...");

            float[] executionTimes = new float[5];
            int executionIndex = 0;

            // Schedule jobs at different times and verify they execute at correct times
            for (int i = 0; i < 5; i++)
            {
                float delay = i * 0.1f;
                _jobDispatcher.UnityThread.ExecuteJobAfterDelay(new TimeSyncTestJob(i,
                                                                                    onExecute: (index) =>
                                                                                    {
                                                                                        executionTimes[index] = Time.time;
                                                                                    }), delay);
            }

            yield return new WaitForSeconds(1f);

            bool timingCorrect = true;
            for (int i = 1; i < 5; i++)
            {
                float actualInterval = executionTimes[i] - executionTimes[i - 1];
                float expectedInterval = 0.1f;
                if (Mathf.Abs(actualInterval - expectedInterval) > 0.05f)
                {
                    timingCorrect = false;
                    break;
                }
            }

            Debug.Log($"Time synchronization: {(timingCorrect ? "PASS" : "FAIL")}");
        }

        #region Helper Classes
        private class UnityAPITestJob : IDispatchableJob
        {
            private readonly System.Action _onExecute;
            private readonly System.Action _onComplete;

            public UnityAPITestJob(System.Action onExecute, System.Action onComplete = null)
            {
                _onExecute = onExecute;
                _onComplete = onComplete;
            }

            public void OnExecute() => _onExecute?.Invoke();
            public void OnComplete() => _onComplete?.Invoke();
            public void OnStop() { }
        }

        private class GameObjectTestJob : IDispatchableJob
        {
            private readonly IntegrationTestComponent                _component;
            private readonly System.Action<IntegrationTestComponent> _onExecute;
            private readonly System.Action                           _onComplete;

            public GameObjectTestJob(IntegrationTestComponent component,
                                     System.Action<IntegrationTestComponent> onExecute,
                                     System.Action onComplete = null)
            {
                _component = component;
                _onExecute = onExecute;
                _onComplete = onComplete;
            }

            public void OnExecute() => _onExecute?.Invoke(_component);
            public void OnComplete() => _onComplete?.Invoke();
            public void OnStop() { }
        }

        private class IntegrationCoroutineJob : ICoroutineJob
        {
            private readonly System.Func<IEnumerator> _onExecute;
            private readonly System.Action            _onComplete;

            public IntegrationCoroutineJob(System.Func<IEnumerator> onExecute, System.Action onComplete = null)
            {
                _onExecute = onExecute;
                _onComplete = onComplete;
            }

            public IEnumerator OnExecute() => _onExecute?.Invoke();
            public void OnComplete() => _onComplete?.Invoke();
            public void OnStop() { }
        }

        private class TimeSyncTestJob : IDispatchableJob
        {
            private readonly int                _index;
            private readonly System.Action<int> _onExecute;

            public TimeSyncTestJob(int index, System.Action<int> onExecute)
            {
                _index = index;
                _onExecute = onExecute;
            }

            public void OnExecute() => _onExecute?.Invoke(_index);
            public void OnComplete() { }
            public void OnStop() { }
        }
        #endregion
    }

    public class IntegrationTestComponent : MonoBehaviour
    {
        public int TestValue = 0;
    }
}