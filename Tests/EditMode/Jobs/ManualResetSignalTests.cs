using System;
using System.Threading;
using AK.Jobs;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Jobs
{
	/// <summary>
	/// The scheduler's kick and done events. They replaced <c>ManualResetEventSlim</c>, which
	/// allocated a lock the first time a thread blocked on it: the allocation landed in whichever
	/// frame first parked a worker, and made the zero-allocation tests flaky.
	/// </summary>
	[TestFixture]
	public sealed class ManualResetSignalTests
	{
		[Test]
		public void FirstBlockingWait_AllocatesNothing()
		{
			var    signals   = new[] { new ManualResetSignal(spinCount: 0), new ManualResetSignal(spinCount: 0) };
			int    next      = 0;
			bool   signalled = true;
			Action blockOnce = () => signalled = signals[next++].Wait(5);

			blockOnce(); // Compiles the lambda and the blocking path, on the first signal.
			int allocations = GcAllocations.Count(blockOnce);

			Assert.IsFalse(signalled, "nothing set the second signal, so its wait blocked and timed out");
			Assert.AreEqual(0, allocations, "the first block on a fresh signal");
		}

		[Test]
		public void Wait_TimesOutUnlessSet_AndResetClears()
		{
			var signal = new ManualResetSignal(spinCount: 20);

			Assert.IsFalse(signal.IsSet);
			Assert.IsFalse(signal.Wait(0));
			Assert.IsFalse(signal.Wait(10));

			signal.Set();
			Assert.IsTrue(signal.IsSet);
			Assert.IsTrue(signal.Wait(0));
			signal.Wait();

			signal.Reset();
			Assert.IsFalse(signal.IsSet);
			Assert.IsFalse(signal.Wait(0));
		}

		[Test]
		public void Set_WakesABlockedWaiter_WhichSeesWhatWasWrittenBefore()
		{
			var signal  = new ManualResetSignal(spinCount: 0);
			int payload = 0;
			int seen    = -1;

			var waiter = new Thread(() =>
			{
				signal.Wait();
				seen = payload;
			});
			waiter.Start();

			Thread.Sleep(20);
			payload = 42;
			signal.Set();

			Assert.IsTrue(waiter.Join(2000), "the waiter woke");
			Assert.AreEqual(42, seen);
		}

		[Test]
		public void PingPong_5000RoundTrips_NoWakeupIsLost()
		{
			const int rounds = 5000;

			var ping = new ManualResetSignal(spinCount: 0);
			var pong = new ManualResetSignal(spinCount: 0);
			int lost = 0;

			// The kick and done protocol: each side resets its own signal before setting the other's.
			var partner = new Thread(() =>
			{
				for (int i = 0; i < rounds; i++)
				{
					if (!ping.Wait(2000))
					{
						Interlocked.Increment(ref lost);
						return;
					}

					ping.Reset();
					pong.Set();
				}
			});
			partner.Start();

			for (int i = 0; i < rounds; i++)
			{
				ping.Set();
				if (!pong.Wait(2000))
				{
					Interlocked.Increment(ref lost);
					break;
				}

				pong.Reset();
			}

			Assert.IsTrue(partner.Join(2000));
			Assert.AreEqual(0, lost);
		}
	}
}
