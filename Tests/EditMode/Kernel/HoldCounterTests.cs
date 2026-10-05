using System;
using System.Collections.Generic;
using AK.Kernel.Collections;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class HoldCounterTests
	{
		[Test]
		public void NewCounter_HoldsNothing()
		{
			var holds = new HoldCounter<string>();

			Assert.IsFalse(holds.IsHeld);
			Assert.AreEqual(0, holds.Count);
			Assert.AreEqual(double.PositiveInfinity, holds.NextDeadline);
		}

		[Test]
		public void Acquire_Holds_UntilItsTokenIsReleased()
		{
			var holds = new HoldCounter<string>();

			HoldToken token = holds.Acquire("a");

			Assert.IsTrue(token.IsSet);
			Assert.IsTrue(holds.IsHeld);
			Assert.IsTrue(holds.IsLive(token));
			Assert.AreEqual(1, holds.Count);

			Assert.IsTrue(holds.Release(token));
			Assert.IsFalse(holds.IsHeld);
			Assert.IsFalse(holds.IsLive(token));
			Assert.AreEqual(0, holds.Count);
		}

		[Test]
		public void NestedHolds_StayHeld_UntilTheLastOneEnds_InAnyOrder()
		{
			var holds = new HoldCounter<string>();
			HoldToken a = holds.Acquire("a");
			HoldToken b = holds.Acquire("b");
			HoldToken c = holds.Acquire("c");

			Assert.IsTrue(holds.Release(b));
			Assert.IsTrue(holds.IsHeld, "a and c remain");
			Assert.IsTrue(holds.Release(a));
			Assert.IsTrue(holds.IsHeld, "c remains");
			Assert.IsTrue(holds.Release(c));
			Assert.IsFalse(holds.IsHeld);
		}

		[Test]
		public void ReleasingTwice_EndsTheHoldOnce()
		{
			var holds = new HoldCounter<string>();
			HoldToken a = holds.Acquire("a");
			holds.Acquire("b");

			Assert.IsTrue(holds.Release(a));
			Assert.IsFalse(holds.Release(a), "the second release finds nothing");
			Assert.AreEqual(1, holds.Count, "and doesn't end b's hold");
		}

		[Test]
		public void StaleToken_CannotEndTheHoldThatReusedItsSlot()
		{
			var holds = new HoldCounter<string>();
			HoldToken old = holds.Acquire("old");
			holds.Release(old);

			HoldToken reuse = holds.Acquire("new");
			Assert.AreEqual(old.Slot, reuse.Slot, "the freed slot is reused");
			Assert.AreNotEqual(old, reuse);

			Assert.IsFalse(holds.Release(old));
			Assert.IsTrue(holds.IsLive(reuse));
			Assert.IsTrue(holds.TryGetTag(reuse, out string tag));
			Assert.AreEqual("new", tag);
		}

		[Test]
		public void DefaultToken_NamesNoHold()
		{
			var holds = new HoldCounter<string>();
			holds.Acquire("slot 0 is live");

			HoldToken none = default;

			Assert.IsFalse(none.IsSet);
			Assert.IsFalse(holds.IsLive(none));
			Assert.IsFalse(holds.Release(none));
			Assert.AreEqual(1, holds.Count);
		}

		[Test]
		public void TryGetTag_ReadsTheTag_OnlyWhileTheHoldIsOutstanding()
		{
			var holds = new HoldCounter<string>();
			HoldToken token = holds.Acquire("owner");

			Assert.IsTrue(holds.TryGetTag(token, out string tag));
			Assert.AreEqual("owner", tag);

			holds.Release(token);

			Assert.IsFalse(holds.TryGetTag(token, out tag));
			Assert.IsNull(tag);
		}

		[Test]
		public void ReleaseAll_EndsEveryHold_AndMakesEveryTokenStale()
		{
			var holds = new HoldCounter<string>();
			HoldToken a = holds.Acquire("a");
			HoldToken b = holds.Acquire("b", 10d);
			var released = new List<string>();

			Assert.AreEqual(2, holds.ReleaseAll(released));
			CollectionAssert.AreEqual(new[] { "a", "b" }, released, "in slot order");
			Assert.IsFalse(holds.IsHeld);
			Assert.AreEqual(double.PositiveInfinity, holds.NextDeadline);

			HoldToken c = holds.Acquire("c");

			Assert.IsFalse(holds.Release(a));
			Assert.IsFalse(holds.Release(b));
			Assert.IsTrue(holds.IsLive(c), "tokens from before can't end a hold taken after");
			Assert.AreEqual(1, holds.ReleaseAll(), "the list is optional");
		}

		// Each case: a hold's deadline, the time ReleaseExpired runs at, and whether that ends it.
		private static IEnumerable<TestCaseData> Deadlines()
		{
			yield return new TestCaseData(5d, 4.999d, false).SetName("Deadline: before it, kept");
			yield return new TestCaseData(5d, 5d, true).SetName("Deadline: at it, ended");
			yield return new TestCaseData(5d, 6d, true).SetName("Deadline: after it, ended");
			yield return new TestCaseData(-1d, 0d, true).SetName("Deadline: already past, ended at the first check");
			yield return new TestCaseData(double.NegativeInfinity, double.MinValue, true).SetName("Deadline: negative infinity, always ended");
			yield return new TestCaseData(double.PositiveInfinity, double.MaxValue, false).SetName("Deadline: none, kept");
			yield return new TestCaseData(double.PositiveInfinity, double.PositiveInfinity, false).SetName("Deadline: none, kept even at infinity");
		}

		[TestCaseSource(nameof(Deadlines))]
		public void ReleaseExpired_EndsAHold_OnceItsDeadlineIsReached(double deadline, double now, bool ends)
		{
			var holds = new HoldCounter<string>();
			HoldToken token = holds.Acquire("a", deadline);
			var expired = new List<string>();

			Assert.AreEqual(ends ? 1 : 0, holds.ReleaseExpired(now, expired));
			Assert.AreEqual(!ends, holds.IsLive(token));
			CollectionAssert.AreEqual(ends ? new[] { "a" } : Array.Empty<string>(), expired);
		}

		[Test]
		public void ReleaseExpired_EndsOnlyTheExpiredHolds_AndNextDeadlineFollows()
		{
			var holds = new HoldCounter<string>();
			HoldToken early = holds.Acquire("early", 1d);
			HoldToken late = holds.Acquire("late", 3d);
			HoldToken open = holds.Acquire("open");

			Assert.AreEqual(1d, holds.NextDeadline);

			var expired = new List<string>();
			Assert.AreEqual(1, holds.ReleaseExpired(2d, expired));
			CollectionAssert.AreEqual(new[] { "early" }, expired);
			Assert.IsFalse(holds.IsLive(early));
			Assert.IsTrue(holds.IsLive(late));
			Assert.IsTrue(holds.IsLive(open));
			Assert.AreEqual(3d, holds.NextDeadline);

			Assert.IsFalse(holds.Release(early), "an expired hold's token is stale");

			holds.Release(late);
			Assert.AreEqual(double.PositiveInfinity, holds.NextDeadline, "only a hold without a deadline is left");
			Assert.AreEqual(0, holds.ReleaseExpired(double.MaxValue), "and it never expires");
		}

		[Test]
		public void NaN_Throws_ForADeadlineOrAClock_AndChangesNothing()
		{
			var holds = new HoldCounter<string>();

			Assert.Throws<ArgumentException>(() => holds.Acquire("a", double.NaN));
			Assert.AreEqual(0, holds.Count, "nothing was taken");

			holds.Acquire("b", 1d);

			Assert.Throws<ArgumentException>(() => holds.ReleaseExpired(double.NaN));
			Assert.AreEqual(1, holds.Count, "nothing was ended");
		}

		[Test]
		public void CopyTagsTo_AppendsTheOutstandingHolds_InSlotOrder()
		{
			var holds = new HoldCounter<string>();
			holds.Acquire("a");
			HoldToken b = holds.Acquire("b");
			holds.Acquire("c");
			holds.Release(b);

			var tags = new List<string> { "kept" };
			holds.CopyTagsTo(tags);

			CollectionAssert.AreEqual(new[] { "kept", "a", "c" }, tags);
			Assert.Throws<ArgumentNullException>(() => holds.CopyTagsTo(null));
		}

		[Test]
		public void GrowsPastItsCapacity_AndEveryTokenStillReleasesOnce()
		{
			var holds = new HoldCounter<int>(capacity: 0);
			var tokens = new List<HoldToken>();

			for (int i = 0; i < 100; i++)
			{
				tokens.Add(holds.Acquire(i));
			}

			Assert.AreEqual(100, holds.Count);

			for (int i = 0; i < tokens.Count; i++)
			{
				Assert.IsTrue(holds.TryGetTag(tokens[i], out int tag), $"hold {i}");
				Assert.AreEqual(i, tag);
				Assert.IsTrue(holds.Release(tokens[i]), $"hold {i}");
				Assert.IsFalse(holds.Release(tokens[i]), $"hold {i}, again");
			}

			Assert.IsFalse(holds.IsHeld);
		}

		[Test]
		public void SteadyState_DoesNotAllocate()
		{
			var holds = new HoldCounter<string>();
			var expired = new List<string>(4);

			// Warm up: the slots the cycle uses.
			Cycle(holds, expired);

			int allocations = GcAllocations.Count(() =>
			{
				for (int i = 0; i < 100; i++)
				{
					Cycle(holds, expired);
				}
			});

			Assert.AreEqual(0, allocations);
		}

		private static void Cycle(HoldCounter<string> holds, List<string> expired)
		{
			HoldToken a = holds.Acquire("a");
			HoldToken b = holds.Acquire("b", 1d);
			holds.ReleaseExpired(2d, expired);
			expired.Clear();
			holds.Release(a);
			holds.Release(b);
		}
	}
}
