using System;

namespace Utilities.Jobs.AnonymousImpl
{
    internal class InternalDispatchableJob : IDispatchableJob
    {
        private Action _onExecute;
        private Action _onComplete;
        private Action _onStop;

        internal InternalDispatchableJob(Action onExecute, Action onComplete, Action onStop)
        {
            _onExecute  = onExecute;
            _onComplete = onComplete;
            _onStop     = onStop;
        }

        public void OnExecute()
        {
            _onExecute?.Invoke();
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