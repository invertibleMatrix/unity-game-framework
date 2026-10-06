using System;
using System.Collections.Generic;
using AK.Kernel.Collections;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class ClaimTableTests
	{
		[Test]
		public void Acquire_ThenRelease_HandsBackTheHandle_AndForgetsTheKey()
		{
			var table = new ClaimTable<string, int>();

			table.Acquire("a", 7);

			Assert.IsTrue(table.Contains("a"));
			Assert.AreEqual(1, table.Count);
			Assert.AreEqual(1, table.ClaimCount);
			Assert.AreEqual(1, table.ClaimsOf("a"));

			Assert.IsTrue(table.TryRelease("a", out int handle));
			Assert.AreEqual(7, handle);
			Assert.IsFalse(table.Contains("a"));
			Assert.AreEqual(0, table.Count);
			Assert.AreEqual(0, table.ClaimCount);
		}

		[Test]
		public void Repeats_ComeBackMostRecentFirst_AndEachHandleExactlyOnce()
		{
			var table = new ClaimTable<string, int>();
			table.Acquire("a", 1);
			table.Acquire("a", 2);
			table.Acquire("a", 3);

			Assert.AreEqual(3, table.ClaimsOf("a"));
			Assert.AreEqual(1, table.Count, "repeats share one key");

			var released = new List<int>();
			while (table.TryRelease("a", out int handle))
			{
				released.Add(handle);
			}

			CollectionAssert.AreEqual(new[] { 3, 2, 1 }, released);
			Assert.AreEqual(0, table.ClaimCount);
		}

		[Test]
		public void Release_UnknownKey_IsFalse_WithDefaultHandle()
		{
			var table = new ClaimTable<string, int>();
			table.Acquire("a", 1);

			Assert.IsFalse(table.TryRelease("b", out int handle));
			Assert.AreEqual(0, handle);
			Assert.IsFalse(table.TryRelease(null, out _));
			Assert.AreEqual(1, table.ClaimCount, "a miss must not touch other keys");
		}

		[Test]
		public void Acquire_NullKey_Throws()
		{
			var table = new ClaimTable<string, int>();
			Assert.Throws<ArgumentNullException>(() => table.Acquire(null, 1));
			Assert.AreEqual(0, table.ClaimCount);
		}

		[Test]
		public void Drain_TakesEveryClaimOnOneKey_MostRecentFirst()
		{
			var table = new ClaimTable<string, int>();
			table.Acquire("a", 1);
			table.Acquire("a", 2);
			table.Acquire("b", 9);

			var released = new List<int>();
			Assert.AreEqual(2, table.Drain("a", released));

			CollectionAssert.AreEqual(new[] { 2, 1 }, released);
			Assert.IsFalse(table.Contains("a"));
			Assert.AreEqual(1, table.ClaimCount);
			Assert.AreEqual(1, table.ClaimsOf("b"));
			Assert.AreEqual(0, table.Drain("missing", released));
		}

		[Test]
		public void DrainAll_EmptiesTheTable_AndReturnsEveryHandle()
		{
			var table = new ClaimTable<string, int>();
			table.Acquire("a", 1);
			table.Acquire("a", 2);
			table.Acquire("b", 3);

			var released = new List<int>();
			Assert.AreEqual(3, table.DrainAll(released));

			CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, released);
			Assert.AreEqual(0, table.Count);
			Assert.AreEqual(0, table.ClaimCount);
		}

		[Test]
		public void CopyKeysTo_ListsEveryKeyOnce()
		{
			var table = new ClaimTable<string, int>();
			table.Acquire("a", 1);
			table.Acquire("a", 2);
			table.Acquire("b", 3);

			var keys = new List<string>();
			table.CopyKeysTo(keys);

			CollectionAssert.AreEquivalent(new[] { "a", "b" }, keys);
		}

		[Test]
		public void Comparer_DecidesKeyIdentity()
		{
			var table = new ClaimTable<string, int>(4, StringComparer.OrdinalIgnoreCase);
			table.Acquire("Key", 1);
			table.Acquire("KEY", 2);

			Assert.AreEqual(1, table.Count);
			Assert.AreEqual(2, table.ClaimsOf("key"));
		}

		[Test]
		public void SteadyState_DoesNotAllocate()
		{
			var table = new ClaimTable<int, int>(4);

			// Warm up: the key's slot and one pooled repeat stack.
			Cycle(table);

			int allocations = GcAllocations.Count(() =>
			{
				for (int i = 0; i < 100; i++)
				{
					Cycle(table);
				}
			});

			Assert.AreEqual(0, allocations);
		}

		private static void Cycle(ClaimTable<int, int> table)
		{
			table.Acquire(1, 10);
			table.Acquire(1, 11);
			table.Acquire(1, 12);
			table.TryRelease(1, out _);
			table.TryRelease(1, out _);
			table.TryRelease(1, out _);
		}
	}
}
