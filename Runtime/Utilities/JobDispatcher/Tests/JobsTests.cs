using System;
using System.Collections;
using System.Collections.Generic;
using AK.Utilities;
using Utilities.Jobs;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;

namespace Gameplay
{
    public class JobsTests : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private       JobDispatcher _jobDispatcher;
        public static int           TempCounter1;
        public static int           TempCounter2;
        public        uint          JobsCount;
        public        int           JobsDelay;
        public        int           FrameDelay;
        public        int           Value1;
        public        int           Value2;

        void Start()
        {
            _jobDispatcher = _jobDispatcher = JobDispatcher.Construct();
        }

        private List<IDispatchedJobHandle> _handles = new();

        public class CounterJobTest : IDispatchableJob
        {
            private double _result;

            public void OnExecute()
            {
                using (new ScopedTimeProfiler("Loop Test"))
                {
                    uint counter = 1 << 18;
                    for (uint i = 0; i < counter; i++)
                    {
                        _result = Math.Sin(i);
                    }
                }
            }

            public void OnComplete()
            {
                Debug.Log($"Result is {_result}");
            }

            public void OnStop() { }
        }

        private IDispatchedJobHandle _jobHandle;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                using (new ScopedTimeProfiler("Time to Add in List"))
                {
                    for (int i = 0; i < JobsCount; i++)
                    {
                        _jobHandle = _jobDispatcher.UnityThread.ExecuteRepeatingJob(new CounterJob(), 3f, 1f);
                    }
                }

                Debug.Log($"Dispatched at {IJobDispatcher.FrameCounter}  {Time.time}");
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                _jobHandle.CancelJob();
            }

            Value1 = TempCounter1;
            Value2 = TempCounter2;
        }
    }

    public class CounterJob : IDispatchableJob
    {
        public void OnExecute()
        {
            JobsTests.TempCounter1++;
            Debug.Log($"{JobDispatcher.FrameCounter}:{JobDispatcher.UnityTime}:{JobsTests.TempCounter1}");
        }

        public void OnComplete()
        {
            JobsTests.TempCounter2++;
        }

        public void OnStop()
        {
            JobsTests.TempCounter1--;
        }
    }

    public class RepeatingJobTest : IDispatchableJob
    {
        public void OnExecute()
        {
            Debug.Log($"OnExecute {IJobDispatcher.FrameCounter} : {Time.time} ");
        }

        public void OnComplete()
        {
            Debug.Log("OnComplete");
        }

        public void OnStop()
        {
            Debug.Log($"OnStop {IJobDispatcher.FrameCounter} : {Time.time} ");
        }
    }

    public class CRJobTest : ICoroutineJob
    {
        public IEnumerator OnExecute()
        {
            while (true)
            {
                Debug.Log("This is from CRJobTest");
                yield return new WaitForSeconds(3f);
            }
        }

        public void OnComplete()
        {
            Debug.Log("This is from CRJobTest OnComplete");
        }

        public void OnStop()
        {
            Debug.Log("This is from CRJobTest OnStop");
        }
    }
}