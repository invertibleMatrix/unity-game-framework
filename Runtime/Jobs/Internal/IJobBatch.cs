namespace AK.Jobs
{
	/// <summary>What the scheduler needs from a batch without knowing its element type.</summary>
	internal interface IJobBatch : IChunkRunner
	{
		int Phase { get; }

		/// <summary>0 = let the scheduler choose.</summary>
		int ItemsPerChunk { get; }

		int ExecutingCount { get; }

		JobScheduler Owner { get; set; }

		/// <summary>Rotate buffers at the barrier. Only the main thread calls this, only while the workers are parked.</summary>
		void Rotate();
	}
}
