using System;
using System.Threading;
using Utilities.Jobs;
using UnityEngine;

namespace Utilities.JobDispatcher.Tests
{
    public class UpdateJobsTest : MonoBehaviour
    {
        private IJobDispatcher _jobDispatcher;

        private IDispatchedJobHandle _jobMain;
        private IDispatchedJobHandle _jobWorker;

        public bool CallCompleteOnMain;

        public float Timing;
        public int   ExecuteCounter;
        public int   CompleteCounter;
        public int   FramesCounter;

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
            _jobMain = _jobDispatcher.UnityThread.ExecuteJobEveryUpdate(new UpdateJob(this));
            Debug.Log($"Dispatched at {Utilities.Jobs.JobDispatcher.FrameCounter} {Time.time}");
        }

        [ContextMenu("StopJobMain")]
        public void StopJobMain()
        {
            _jobMain.CancelJob();
        }

        [ContextMenu("StartJobWorker")]
        public void StartJobWorker()
        {
            _jobWorker = _jobDispatcher.WorkerThread.ExecuteJobEveryUpdate(new UpdateJob(this), CallCompleteOnMain);

            Debug.Log($"Dispatched at {Utilities.Jobs.JobDispatcher.FrameCounter} {Utilities.Jobs.JobDispatcher.UnityTime}");
        }

        [ContextMenu("StopJobWorker")]
        public void StopJobWorker()
        {
            _jobWorker.CancelJob();
        }

        public class UpdateJob : IDispatchableJob
        {
            private UpdateJobsTest _tester;

            public UpdateJob(UpdateJobsTest tester)
            {
                _tester = tester;
            }

            public void OnExecute()
            {
                _tester.FramesCounter = (int)Utilities.Jobs.JobDispatcher.FrameCounter;
                _tester.ExecuteCounter++;
                _tester.Timing = Utilities.Jobs.JobDispatcher.UnityTime;
                if (_tester.CallCompleteOnMain)
                {
                    Debug.Log($"OnExecute {Utilities.Jobs.JobDispatcher.FrameCounter} {Utilities.Jobs.JobDispatcher.UnityTime}");
                }

                Thread.Sleep(8);
            }

            public void OnComplete()
            {
                _tester.CompleteCounter++;
                // _tester.Timing -= Jobs.JobDispatcher.UnityTime;
                if (_tester.CallCompleteOnMain)
                {
                    Debug.Log($"OnComplete {Utilities.Jobs.JobDispatcher.FrameCounter} {Utilities.Jobs.JobDispatcher.UnityTime} {Thread.CurrentThread.Name}");
                }
            }

            public void OnStop()
            {
                Debug.Log($"OnStop {Utilities.Jobs.JobDispatcher.FrameCounter} {Utilities.Jobs.JobDispatcher.UnityTime}");
            }
        }
    }
}