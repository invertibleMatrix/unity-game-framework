using System;
using System.Collections.Generic;
using AK.Core.Collections;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests
{
	public class ListPoolTests
	{
		[Test]
		public void Rent_ReturnsClearedList_AndDisposeReturnsIt()
		{
			List<int> first;
			using (PooledList<int> rented = ListPool<int>.Rent())
			{
				first = rented.List;
				rented.Add(1);
				rented.Add(2);
				Assert.AreEqual(2, rented.Count);
				Assert.AreEqual(2, rented[1]);
			}

			using (PooledList<int> again = ListPool<int>.Rent())
			{
				Assert.AreSame(first, again.List, "the returned list is reused");
				Assert.AreEqual(0, again.Count, "and it came back cleared");
			}
		}

		[Test]
		public void Rent_HonoursMinCapacity()
		{
			using PooledList<int> rented = ListPool<int>.Rent(256);
			Assert.GreaterOrEqual(rented.List.Capacity, 256);
		}

		[Test]
		public void Foreach_OverPooledList_Works()
		{
			using PooledList<int> rented = ListPool<int>.Rent();
			rented.Add(3);
			rented.Add(4);

			int sum = 0;
			foreach (int v in rented) sum += v;
			Assert.AreEqual(7, sum);
		}

		[Test]
		public void SteadyState_RentReturn_DoesNotAllocate()
		{
			using (ListPool<int>.Rent()) { }

			int allocations = GcAllocations.Count(() =>
			{
				for (int i = 0; i < 1000; i++)
				{
					using PooledList<int> rented = ListPool<int>.Rent();
					rented.Add(i);
				}
			});

			Assert.AreEqual(0, allocations, "rent/return allocated at steady state");
		}
	}
}
