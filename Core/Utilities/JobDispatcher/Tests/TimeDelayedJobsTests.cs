using System.Threading;
using Utilities.Jobs;
using UnityEngine;

namespace Utilities.JobDispatcher.Tests
{
    public class TimeDelayedJobsTests : MonoBehaviour
    {
        private IJobDispatcher _jobDispatcher;

        private IDispatchedJobHandle _jobMain;
        private IDispatchedJobHandle _jobWorker;

        public bool  CallCompleteOnMain;
        public float ExecuteAfterTimeMain;
        public float ExecuteAfterTimeWorker;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                StartJobMain();
            }

            if (Input.GetKeyDown(KeyCode.W))
            {
                StopJobMain();
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                StartJobWorker();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                StopJobWorker();
            }
        }

        [ContextMenu("StartJobMain")]
        public void StartJobMain()
        {
            _jobMain = _jobDispatcher.UnityThread.ExecuteJobAfterDelay(new TimeDelayedJobTest(), ExecuteAfterTimeMain);
            Debug.Log($"Dispatched at {Utilities.Jobs.JobDispatcher.UnityTime}");
        }

        [ContextMenu("StopJobMain")]
        public void StopJobMain()
        {
            _jobMain.CancelJob();
        }

        [ContextMenu("StartJobWorker")]
        public void StartJobWorker()
        {
            _jobWorker = _jobDispatcher.WorkerThread.ExecuteJobAfterDelay(new TimeDelayedJobTest(), ExecuteAfterTimeWorker,
                                                                          CallCompleteOnMain);
            Debug.Log($"Dispatched at {Utilities.Jobs.JobDispatcher.UnityTime}");
        }

        [ContextMenu("StopJobWorker")]
        public void StopJobWorker()
        {
            _jobWorker.CancelJob();
        }

        public class TimeDelayedJobTest : IDispatchableJob
        {
            public void OnExecute()
            {
                Debug.Log($"OnExecute at {Utilities.Jobs.JobDispatcher.UnityTime}");
            }

            public void OnComplete()
            {
                Debug.Log($"OnComplete at {Utilities.Jobs.JobDispatcher.UnityTime} {Thread.CurrentThread.Name}");
            }

            public void OnStop()
            {
                Debug.Log($"OnStop at {Utilities.Jobs.JobDispatcher.UnityTime}");
            }
        }
    }
}