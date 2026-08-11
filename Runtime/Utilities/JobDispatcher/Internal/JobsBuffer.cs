using System;
using System.Collections.Generic;

namespace Utilities.Jobs
{
    internal class JobsBuffer
    {
        private const int INITIAL_CAPACITY = 100;

        public int ID;

        public List<FixedJobImpl> UpdateJobs      = new(INITIAL_CAPACITY);
        public List<FixedJobImpl> LateUpdateJobs  = new(INITIAL_CAPACITY);
        public List<FixedJobImpl> FixedUpdateJobs = new(INITIAL_CAPACITY);

        public List<FrameDelayedJobImpl>  FrameDelayedJobs  = new(INITIAL_CAPACITY);
        public List<TimeRepeatingJobImpl> TimeRepeatingJobs = new(INITIAL_CAPACITY);
        public List<TimeDelayedJobImpl>   TimeDelayedJobs   = new(INITIAL_CAPACITY);

        public void ClearBuffer()
        {
            UpdateJobs.Clear();
            LateUpdateJobs.Clear();
            FixedUpdateJobs.Clear();
            FrameDelayedJobs.Clear();
            TimeRepeatingJobs.Clear();
            TimeDelayedJobs.Clear();
        }
    }
}