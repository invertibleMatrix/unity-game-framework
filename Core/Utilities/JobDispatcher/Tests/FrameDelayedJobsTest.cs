using System.Threading;
using Utilities.Jobs;
using UnityEngine;

namespace Utilities.JobDispatcher.Tests
{
    public class FrameDelayedJobsTest : MonoBehaviour
    {
        private IJobDispatcher _jobDispatcher;

        private IDispatchedJobHandle _jobMain;
        private IDispatchedJobHandle _jobWorker;

        public bool CallCompleteOnMain;
        public int  ExecuteAtFrameMain;
        public int  ExecuteAtFrameWorker;

        private void Start() { }

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
            _jobMain = _jobDispatcher.UnityThread.ExecuteJobAtFrame(new FrameDelayedJobTest(), ExecuteAtFrameMain);
            Debug.Log($"Dispatched at {Utilities.Jobs.JobDispatcher.FrameCounter}");
        }

        [ContextMenu("StopJobMain")]
        public void StopJobMain()
        {
            _jobMain.CancelJob();
        }

        [ContextMenu("StartJobWorker")]
        public void StartJobWorker()
        {
            _jobWorker = _jobDispatcher.WorkerThread.ExecuteJobAtFrame(new FrameDelayedJobTest(), ExecuteAtFrameWorker, CallCompleteOnMain);
            Debug.Log($"Dispatched at {Utilities.Jobs.JobDispatcher.FrameCounter}");
        }

        [ContextMenu("StopJobWorker")]
        public void StopJobWorker()
        {
            _jobWorker.CancelJob();
        }

        public class FrameDelayedJobTest : IDispatchableJob
        {
            public void OnExecute()
            {
                Debug.Log($"OnExecute at {Utilities.Jobs.JobDispatcher.FrameCounter}");
            }

            public void OnComplete()
            {
                Debug.Log($"OnComplete at {Utilities.Jobs.JobDispatcher.FrameCounter} {Thread.CurrentThread.Name}");
            }

            public void OnStop()
            {
                Debug.Log($"OnStop at {Utilities.Jobs.JobDispatcher.FrameCounter}");
            }
        }
    }
}