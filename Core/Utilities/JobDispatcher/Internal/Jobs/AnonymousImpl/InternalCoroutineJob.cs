using System;
using System.Collections;

namespace Utilities.Jobs.AnonymousImpl
{
    internal class InternalCoroutineJob : ICoroutineJob
    {
        private IEnumerator _coroutine;
        private Action      _onComplete;
        private Action      _onStop;

        public InternalCoroutineJob(IEnumerator coroutine, Action onComplete, Action onStop)
        {
            _coroutine  = coroutine;
            _onComplete = onComplete;
            _onStop     = onStop;
        }

        public IEnumerator OnExecute()
        {
            if (_coroutine != null)
            {
                yield return _coroutine;
            }
        }

        public void OnComplete()
        {
            if (_coroutine != null)
            {
                _onComplete?.Invoke();
            }
        }

        public void OnStop()
        {
            if (_coroutine != null)
            {
                _onStop?.Invoke();
            }
        }
    }
}