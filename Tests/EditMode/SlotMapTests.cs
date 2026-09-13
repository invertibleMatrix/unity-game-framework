using System;
using System.Collections.Generic;
using AK.Core.Collections;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests
{
	public class SlotMapTests
	{
		private sealed class Thing
		{
			public int Tag;
		}

		[Test]
		public void Default_Handle_IsInvalid()
		{
			Handle<Thing> h = default;
			Assert.IsFalse(h.IsSet);
			Assert.AreEqual(Handle<Thing>.Invalid, h);
		}

		[Test]
		public void Add_ReturnsHandle_ThatResolves()
		{
			var map = new SlotMap<Thing>();
			var a = new Thing { Tag = 1 };

			Handle<Thing> h = map.Add(a);

			Assert.IsTrue(h.IsSet);
			Assert.IsTrue(map.Contains(h));
			Assert.IsTrue(map.TryGet(h, out Thing got));
			Assert.AreSame(a, got);
			Assert.AreSame(a, map.Get(h));
			Assert.AreEqual(1, map.Count);
		}

		[Test]
		public void Remove_InvalidatesHandle_AndReturnsFalseSecondTime()
		{
			var map = new SlotMap<Thing>();
			Handle<Thing> h = map.Add(new Thing());

			Assert.IsTrue(map.Remove(h));
			Assert.IsFalse(map.Contains(h));
			Assert.IsFalse(map.TryGet(h, out Thing got));
			Assert.IsNull(got);
			Assert.IsNull(map.Get(h));
			Assert.AreEqual(0, map.Count);

			Assert.IsFalse(map.Remove(h), "second remove of the same handle must be a no-op");
		}

		[Test]
		public void ReusedSlot_DoesNotResolve_StaleHandle()
		{
			var map = new SlotMap<Thing>();
			var first  = new Thing { Tag = 1 };
			var second = new Thing { Tag = 2 };

			Handle<Thing> stale = map.Add(first);
			map.Remove(stale);
			Handle<Thing> fresh = map.Add(second);

			Assert.AreEqual(stale.Index, fresh.Index, "the freed slot must be reused");
			Assert.AreNotEqual(stale, fresh);
			Assert.IsFalse(map.TryGet(stale, out _), "a handle from the previous lifetime must not resolve");
			Assert.IsTrue(map.TryGet(fresh, out Thing got));
			Assert.AreSame(second, got);
		}

		[Test]
		public void Generations_LiveAreOdd_FreeAreEven()
		{
			var map = new SlotMap<Thing>();
			Handle<Thing> h1 = map.Add(new Thing());
			Assert.AreEqual(1u, h1.Generation & 1u);

			map.Remove(h1);
			Handle<Thing> h2 = map.Add(new Thing());
			Assert.AreEqual(1u, h2.Generation & 1u);
			Assert.Greater(h2.Generation, h1.Generation);
		}

		[Test]
		public void FreeList_IsLifo()
		{
			var map = new SlotMap<Thing>();
			Handle<Thing> a = map.Add(new Thing());
			Handle<Thing> b = map.Add(new Thing());
			Handle<Thing> c = map.Add(new Thing());

			map.Remove(a);
			map.Remove(c);

			Handle<Thing> reuse1 = map.Add(new Thing());
			Handle<Thing> reuse2 = map.Add(new Thing());
			Assert.AreEqual(c.Index, reuse1.Index);
			Assert.AreEqual(a.Index, reuse2.Index);
			Assert.IsTrue(map.Contains(b));
		}

		[Test]
		public void Grows_PastInitialCapacity_KeepingHandlesValid()
		{
			var map = new SlotMap<Thing>(2);
			var handles = new List<Handle<Thing>>();
			var things  = new List<Thing>();

			for (int i = 0; i < 100; i++)
			{
				var t = new Thing { Tag = i };
				things.Add(t);
				handles.Add(map.Add(t));
			}

			Assert.AreEqual(100, map.Count);
			Assert.GreaterOrEqual(map.Capacity, 100);

			for (int i = 0; i < 100; i++)
			{
				Assert.IsTrue(map.TryGet(handles[i], out Thing got));
				Assert.AreSame(things[i], got);
			}
		}

		[Test]
		public void Foreign_OrOutOfRange_Handle_IsRejected()
		{
			var map = new SlotMap<Thing>();
			map.Add(new Thing());

			var other = new SlotMap<Thing>();
			for (int i = 0; i < 10; i++) other.Add(new Thing());
			Handle<Thing> far = other.Add(new Thing());

			Assert.IsFalse(map.Contains(far));
			Assert.IsFalse(map.TryGet(far, out _));
			Assert.IsFalse(map.Remove(far));
		}

		[Test]
		public void Enumeration_VisitsLiveOnly_InSlotOrder()
		{
			var map = new SlotMap<Thing>();
			Handle<Thing> a = map.Add(new Thing { Tag = 1 });
			Handle<Thing> b = map.Add(new Thing { Tag = 2 });
			Handle<Thing> c = map.Add(new Thing { Tag = 3 });
			map.Remove(b);

			var seen = new List<int>();
			foreach (Thing t in map) seen.Add(t.Tag);

			CollectionAssert.AreEqual(new[] { 1, 3 }, seen);
		}

		[Test]
		public void Enumeration_ToleratesRemoveMidWalk()
		{
			var map = new SlotMap<Thing>();
			for (int i = 0; i < 10; i++) map.Add(new Thing { Tag = i });

			int visited = 0;
			SlotMap<Thing>.Enumerator e = map.GetEnumerator();
			while (e.MoveNext())
			{
				visited++;
				if ((e.Current.Tag & 1) == 0) map.Remove(e.CurrentHandle);
			}

			Assert.AreEqual(10, visited);
			Assert.AreEqual(5, map.Count);
			foreach (Thing t in map) Assert.AreEqual(1, t.Tag & 1);
		}

		[Test]
		public void HandleAt_RebuildsLiveHandle_AndRejectsDead()
		{
			var map = new SlotMap<Thing>();
			Handle<Thing> a = map.Add(new Thing());
			Handle<Thing> b = map.Add(new Thing());
			map.Remove(b);

			Assert.AreEqual(a, map.HandleAt(a.Index));
			Assert.AreEqual(Handle<Thing>.Invalid, map.HandleAt(b.Index));
			Assert.AreEqual(Handle<Thing>.Invalid, map.HandleAt(999));
			Assert.AreEqual(Handle<Thing>.Invalid, map.HandleAt(-1));
		}

		[Test]
		public void Clear_InvalidatesEverything_AndReusesSlots()
		{
			var map = new SlotMap<Thing>();
			Handle<Thing> a = map.Add(new Thing());
			Handle<Thing> b = map.Add(new Thing());

			map.Clear();

			Assert.AreEqual(0, map.Count);
			Assert.IsFalse(map.Contains(a));
			Assert.IsFalse(map.Contains(b));

			Handle<Thing> c = map.Add(new Thing());
			Assert.AreEqual(0, c.Index);
			Assert.AreNotEqual(a, c);
			Assert.IsTrue(map.Contains(c));
		}

		[Test]
		public void RemoveWithOut_HandsBackStoredItem()
		{
			var map = new SlotMap<Thing>();
			var t = new Thing { Tag = 7 };
			Handle<Thing> h = map.Add(t);

			Assert.IsTrue(map.Remove(h, out Thing removed));
			Assert.AreSame(t, removed);
			Assert.IsFalse(map.Remove(h, out Thing again));
			Assert.IsNull(again);
		}

		[Test]
		public void Handle_Equality_And_Hashing_AreByValue()
		{
			var a1 = new Handle<Thing>(3, 5);
			var a2 = new Handle<Thing>(3, 5);
			var b  = new Handle<Thing>(3, 7);

			Assert.AreEqual(a1, a2);
			Assert.IsTrue(a1 == a2);
			Assert.IsTrue(a1 != b);
			Assert.AreEqual(a1.GetHashCode(), a2.GetHashCode());

			var set = new HashSet<Handle<Thing>> { a1 };
			Assert.IsTrue(set.Contains(a2));
			Assert.IsFalse(set.Contains(b));
		}

		[Test]
		public void WorksWithValueTypes()
		{
			var map = new SlotMap<int>();
			Handle<int> h = map.Add(42);
			Assert.IsTrue(map.TryGet(h, out int v));
			Assert.AreEqual(42, v);

			map.Remove(h);
			Assert.IsFalse(map.TryGet(h, out v));
			Assert.AreEqual(0, v);
		}

		// ---------------------------------------------------------------- allocation

		[Test]
		public void SteadyState_AddRemoveGet_DoesNotAllocate()
		{
			var map = new SlotMap<Thing>(64);
			var items = new Thing[64];
			for (int i = 0; i < items.Length; i++) items[i] = new Thing { Tag = i };

			var handles = new Handle<Thing>[items.Length];
			for (int i = 0; i < items.Length; i++) handles[i] = map.Add(items[i]);
			for (int i = 0; i < items.Length; i++) map.Remove(handles[i]);

			int allocations = GcAllocations.Count(() =>
			{
				for (int round = 0; round < 100; round++)
				{
					for (int i = 0; i < items.Length; i++) handles[i] = map.Add(items[i]);
					for (int i = 0; i < items.Length; i++) map.TryGet(handles[i], out _);
					for (int i = 0; i < items.Length; i++) map.Remove(handles[i]);
				}
			});

			Assert.AreEqual(0, allocations, "Add/TryGet/Remove allocated at steady state");
		}

		[Test]
		public void Foreach_DoesNotAllocate()
		{
			var map = new SlotMap<Thing>(64);
			for (int i = 0; i < 64; i++) map.Add(new Thing { Tag = i });

			int sum = 0;
			foreach (Thing t in map) sum += t.Tag;

			int allocations = GcAllocations.Count(() =>
			{
				for (int round = 0; round < 100; round++)
				{
					foreach (Thing t in map) sum += t.Tag;
				}
			});

			Assert.AreEqual(0, allocations, "struct enumerator must not allocate");
			Assert.Greater(sum, 0);
		}
	}
}
