using System.Collections.Generic;
using AK.Utilities.DataStructures;

namespace Utilities.Jobs
{
    internal class JobsBackLog
    {
        private const int INITIAL_CAPACITY = 100;

        public PriorityQueue<FrameDelayedJobImpl, uint>   FrameDelayedJobQueue  = new(INITIAL_CAPACITY);
        public PriorityQueue<TimeDelayedJobImpl, float>   TimeDelayedJobsQueue  = new(INITIAL_CAPACITY);
        public PriorityQueue<TimeRepeatingJobImpl, float> TimeRepeatedJobsQueue = new(INITIAL_CAPACITY);

        public HashSet<FixedJobImpl> UpdateJobs      = new();
        public HashSet<FixedJobImpl> LateUpdateJobs  = new();
        public HashSet<FixedJobImpl> FixedUpdateJobs = new();

        public void ClearBuffer()
        {
            FrameDelayedJobQueue.Clear();
            TimeDelayedJobsQueue.Clear();
            TimeRepeatedJobsQueue.Clear();

            UpdateJobs.Clear();
            LateUpdateJobs.Clear();
            FixedUpdateJobs.Clear();
        }
    }
}