namespace Utilities.Jobs
{
    public interface IDispatchableJob
    {
        public void OnExecute();
        public void OnComplete();
        public void OnStop();
    }
}