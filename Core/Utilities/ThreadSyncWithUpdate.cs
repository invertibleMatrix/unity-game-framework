using System.Threading;
using UnityEngine;

namespace AK.Utilities
{
    public class ThreadSyncWithUpdate : MonoBehaviour
    {
        private readonly ManualResetEvent _updateEvent = new ManualResetEvent(false);

        private Thread _workerThread;
        private bool   _isRunning = true;

        private volatile int   _frameCounter = 0;
        private volatile float _time;

        void Start()
        {
            _workerThread = new Thread(ThreadLoop);
            _workerThread.Start();
        }

        void Update()
        {
            _frameCounter++;
            _time = Time.time;
            _updateEvent.Set();
        }

        private void ThreadLoop()
        {
            while (_isRunning)
            {
                _updateEvent.WaitOne();
                _updateEvent.Reset();
            }
        }

        void OnDestroy()
        {
            _isRunning = false;
            _updateEvent.Set(); // Ensure the thread can exit
            _workerThread.Join();
        }
    }
}