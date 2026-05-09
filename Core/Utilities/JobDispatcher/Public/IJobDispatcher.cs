namespace Utilities.Jobs
{
    public interface IJobDispatcher
    {
        public static uint  FrameCounter => JobDispatcher.FrameCounter;
        public static float UnityTime    => JobDispatcher.UnityTime;
        public static float RealTime     => JobDispatcher.RealTime;
        public static float Dt           => JobDispatcher.Dt;
        public static float FixedDt      => JobDispatcher.FixedDt;

        public IUnityThreadDispatcher  UnityThread  { get; }
        public IWorkerThreadDispatcher WorkerThread { get; }
    }
}