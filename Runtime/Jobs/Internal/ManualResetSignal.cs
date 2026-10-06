using System.Diagnostics;
using System.Threading;

namespace AK.Jobs
{
	/// <summary>
	/// The scheduler's kick and frame-done events: a manual-reset event whose waiters spin briefly,
	/// then block on a monitor. <c>ManualResetEventSlim</c> creates its lock object the first time a
	/// thread blocks on it, so whichever frame first parked a thread allocated. This one owns its lock
	/// from construction, and blocking never allocates.
	///
	/// <see cref="Set"/> is a release, and a wait that sees the signal is an acquire: what a thread
	/// wrote before setting is visible to the thread that waited for it. In C++ it is an atomic flag,
	/// a mutex and a condition variable.
	/// </summary>
	internal sealed class ManualResetSignal
	{
		private readonly object _lock = new();
		private readonly int    _spinCount;
		private volatile bool   _set;
		private int             _waiters;

		/// <param name="spinCount">Spins before a waiter blocks; 0 blocks at once.</param>
		public ManualResetSignal(int spinCount)
		{
			_spinCount = spinCount;
		}

		public bool IsSet => _set;

		public void Set()
		{
			_set = true;

			// Pairs with the waiter's increment: either the waiter sees the flag before it blocks, or
			// this sees the waiter and wakes it.
			Interlocked.MemoryBarrier();
			if (Volatile.Read(ref _waiters) == 0) return;

			lock (_lock)
			{
				Monitor.PulseAll(_lock);
			}
		}

		public void Reset() => _set = false;

		public void Wait() => Wait(Timeout.Infinite);

		/// <summary>False if <paramref name="millisecondsTimeout"/> passed first; <see cref="Timeout.Infinite"/> waits for good.</summary>
		public bool Wait(int millisecondsTimeout)
		{
			if (_set) return true;

			var spinner = new SpinWait();
			for (int i = 0; i < _spinCount; i++)
			{
				spinner.SpinOnce();
				if (_set) return true;
			}

			return millisecondsTimeout != 0 && Block(millisecondsTimeout);
		}

		private bool Block(int millisecondsTimeout)
		{
			long start = Stopwatch.GetTimestamp();

			lock (_lock)
			{
				Interlocked.Increment(ref _waiters); // A full fence, paired with the one in Set.
				try
				{
					while (!_set)
					{
						int remaining = Remaining(start, millisecondsTimeout);
						if (remaining == 0) return false;

						Monitor.Wait(_lock, remaining);
					}

					return true;
				}
				finally
				{
					Interlocked.Decrement(ref _waiters);
				}
			}
		}

		private static int Remaining(long start, int millisecondsTimeout)
		{
			if (millisecondsTimeout == Timeout.Infinite) return Timeout.Infinite;

			long elapsed = (Stopwatch.GetTimestamp() - start) * 1000 / Stopwatch.Frequency;
			return elapsed >= millisecondsTimeout ? 0 : (int)(millisecondsTimeout - elapsed);
		}
	}
}
