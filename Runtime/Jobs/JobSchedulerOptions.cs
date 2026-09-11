using System;

namespace AK.Jobs
{
	public sealed class JobSchedulerOptions
	{
		/// <summary>
		/// Worker threads. Unity already runs a render thread and its own job workers, so more than a
		/// few managed workers oversubscribes a phone; on big.LITTLE parts a worker may land on a
		/// little core. Measure before raising this.
		/// </summary>
		public int WorkerCount = DefaultWorkerCount;

		/// <summary>
		/// Phases per frame. Every chunk of phase <c>p</c> finishes before any chunk of <c>p + 1</c>
		/// starts, so a phase <c>p + 1</c> job may read what phase <c>p</c> wrote. One phase means
		/// no ordering guarantees between jobs within a frame.
		/// </summary>
		public int PhaseCount = 1;

		/// <summary>Automatic chunking aims for this many chunks per worker per source, so a slow chunk can be balanced by the others.</summary>
		public int ChunksPerWorker = 4;

		/// <summary>Batch elements are small; never split a batch into chunks shorter than this, or the claim atomic dominates.</summary>
		public int MinBatchChunkItems = 16;

		/// <summary>Spins a worker performs on its kick event before parking in the kernel. Small on battery-powered targets.</summary>
		public int SpinCount = 20;

		public static int DefaultWorkerCount => Math.Clamp(Environment.ProcessorCount - 2, 1, 4);

		internal void Validate()
		{
			if (WorkerCount < 1) throw new ArgumentOutOfRangeException(nameof(WorkerCount), WorkerCount, "at least one worker");
			if (PhaseCount < 1 || PhaseCount > 255) throw new ArgumentOutOfRangeException(nameof(PhaseCount), PhaseCount, "1..255");
			if (ChunksPerWorker < 1) throw new ArgumentOutOfRangeException(nameof(ChunksPerWorker), ChunksPerWorker, "at least one");
			if (MinBatchChunkItems < 1) throw new ArgumentOutOfRangeException(nameof(MinBatchChunkItems), MinBatchChunkItems, "at least one");
			if (SpinCount < 0 || SpinCount > 2047) throw new ArgumentOutOfRangeException(nameof(SpinCount), SpinCount, "0..2047");
		}
	}
}
