using System.Diagnostics;

namespace AK.Utilities
{
    public static class StopwatchTimeProfiler
    {
        private static Stopwatch _stopwatch;

        public static void Start()
        {
            _stopwatch = Stopwatch.StartNew();
        }

        public static void Stop(string identifier = "")
        {
            _stopwatch.Stop();
            long   ticks = _stopwatch.ElapsedTicks;
            double ns    = 1000000000.0 * (double)ticks / Stopwatch.Frequency;
            double ms    = ns / 1000000.0;
            double s     = ms / 1000;
            UnityEngine.Debug.Log($"Ticks for {identifier}: {ticks} ms: {ms}");
        }
    }
}