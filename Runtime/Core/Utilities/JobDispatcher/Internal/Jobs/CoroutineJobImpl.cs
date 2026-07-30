using System.Collections;
using UnityEngine;

namespace Utilities.Jobs
{
    internal class CoroutineJobImpl : IDispatchedJobHandle
    {
        internal readonly IEnumerator Coroutine;
        internal          Coroutine   CoroutineHandle;

        internal ICoroutineJob Job;

        // Set even when cancelled before the coroutine ever started, so the dispatcher
        // skips (and drops) it instead of starting a dead job one frame late.
        internal volatile bool IsCancelled;

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
            IsCancelled = true;

            if (Job == null) return;

            if (CoroutineHandle != null)
            {
                _jobDispatcher.StopCoroutine(CoroutineHandle);
            }

            Job.OnStop();
            Job = null; // CRITICAL: Release reference
        }
    }
}