using System;
using System.Diagnostics;

namespace AK.Utilities
{
    public class ScopedTimeProfiler : IDisposable
    {
        private Stopwatch _stopwatch;
        private string    _identifier;

        public ScopedTimeProfiler(string identifier = "")
        {
            _identifier = identifier;
            _stopwatch  = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            long ticks = _stopwatch.ElapsedTicks;
            double ns = 1000000000.0 * (double)ticks / Stopwatch.Frequency;
            double ms = ns / 1000000.0;
            double s = ms / 1000;
            UnityEngine.Debug.Log($"Ticks for {_identifier}: {ticks} ms: {ms}");
        }
    }
}