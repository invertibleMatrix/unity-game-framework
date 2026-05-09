using System.Collections;
using UnityEngine;

namespace Utilities.Jobs
{
    internal class CoroutineJobImpl : IDispatchedJobHandle
    {
        internal readonly IEnumerator Coroutine;
        internal          Coroutine   CoroutineHandle;

        internal ICoroutineJob Job;

        private JobDispatcher _jobDispatcher;

        public CoroutineJobImpl(JobDispatcher jobDispatcher, ICoroutineJob job)
        {
            _jobDispatcher = jobDispatcher;
            Job = job;
            Coroutine = Job.OnExecute();
            CoroutineHandle = null;
        }

        internal void Execute()
        {
            CoroutineHandle = _jobDispatcher.StartCoroutine(RunRoutine());
        }

        private IEnumerator RunRoutine()
        {
            yield return Coroutine;
            Job.OnComplete();
        }

        public void CancelJob()
        {
            if (CoroutineHandle != null && Job != null)
            {
                _jobDispatcher.StopCoroutine(CoroutineHandle);
                Job.OnStop();
                Job = null; // CRITICAL: Release reference
            }
        }
    }
}