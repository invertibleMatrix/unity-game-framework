using System.Collections;

namespace Utilities.Jobs
{
    public interface ICoroutineJob
    {
        public IEnumerator OnExecute();
        public void OnComplete();
        public void OnStop();
    }
}