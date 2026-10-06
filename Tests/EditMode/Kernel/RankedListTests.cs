using System;
using System.Collections.Generic;
using AK.Kernel.Collections;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class RankedListTests
	{
		[Test]
		public void Items_AreInRankOrder_HigherPriorityFirst_ThenLowestOrder()
		{
			var list = new RankedList<string>();
			list.Add("a", new Rank(0, 1));
			list.Add("b", new Rank(5, 2));
			list.Add("c", new Rank(0, 3));
			list.Add("d", new Rank(5, 4));
			list.Add("e", new Rank(-1, 5));
			list.Add("f", new Rank(5, 0));

			CollectionAssert.AreEqual(new[] { "f", "b", "d", "a", "c", "e" }, Items(list));
		}

		[Test]
		public void Add_RefusesARankAnItemHas()
		{
			var list = new RankedList<string>();
			list.Add("a", new Rank(1, 1));

			Assert.Throws<ArgumentException>(() => list.Add("b", new Rank(1, 1)));
			list.Add("z", new Rank(0f, 7));
			Assert.Throws<ArgumentException>(() => list.Add("w", new Rank(-0f, 7)), "0 and -0 are one priority");
			Assert.AreEqual(2, list.Count);
		}

		[Test]
		public void RemoveLatest_RemovesTheEqualItemWithTheHighestOrder()
		{
			var list = new RankedList<string>();
			list.Add("h", new Rank(5, 1));
			list.Add("g", new Rank(3, 2));
			list.Add("h", new Rank(1, 3));

			Assert.IsTrue(list.RemoveLatest("h"));
			CollectionAssert.AreEqual(new[] { "h", "g" }, Items(list));
			Assert.AreEqual(new Rank(5, 1), list.RankAt(0), "the one added first stays");

			Assert.IsTrue(list.RemoveLatest("h"));
			Assert.IsFalse(list.RemoveLatest("h"));
			CollectionAssert.AreEqual(new[] { "g" }, Items(list));
		}

		[Test]
		public void RemoveLatest_ComparesWithTheComparerGiven()
		{
			var list = new RankedList<string>();
			list.Add("A", new Rank(0, 1));

			Assert.IsFalse(list.RemoveLatest("a"));
			Assert.IsTrue(list.RemoveLatest("a", StringComparer.OrdinalIgnoreCase));
		}

		[Test]
		public void AWalk_SurvivesChangesMadeWhileItWalks()
		{
			var list = new RankedList<string>();
			list.Add("first", new Rank(9, 1));
			list.Add("second", new Rank(5, 2));
			list.Add("doomed", new Rank(3, 3));
			list.Add("last", new Rank(0, 4));
			long order = 5;

			var visited = new List<string>();
			for (Rank last = Rank.BeforeAll; ;)
			{
				int next = list.IndexAfter(last);
				if (next == list.Count) break;

				last = list.RankAt(next);
				string item = list.ItemAt(next);
				visited.Add(item);

				if (item == "second")
				{
					list.RemoveLatest("doomed");
					list.Add("too early", new Rank(7, order++));
					list.Add("in time", new Rank(5, order++));
					list.Add("later", new Rank(1, order++));
				}
			}

			CollectionAssert.AreEqual(new[] { "first", "second", "in time", "later", "last" }, visited);
			CollectionAssert.AreEqual(new[] { "first", "too early", "second", "in time", "later", "last" }, Items(list));
		}

		[Test]
		public void IndexAfter_FindsTheFirstItemRankedAfter()
		{
			var list = new RankedList<int>();
			Assert.AreEqual(0, list.IndexAfter(Rank.BeforeAll), "empty");

			list.Add(1, new Rank(2, 1));
			list.Add(2, new Rank(2, 2));
			list.Add(3, new Rank(1, 3));

			Assert.AreEqual(0, list.IndexAfter(Rank.BeforeAll));
			Assert.AreEqual(1, list.IndexAfter(new Rank(2, 1)));
			Assert.AreEqual(2, list.IndexAfter(new Rank(2, 2)));
			Assert.AreEqual(2, list.IndexAfter(new Rank(1.5f, 0)), "a rank that isn't in the list");
			Assert.AreEqual(3, list.IndexAfter(new Rank(1, 3)));
			Assert.AreEqual(0, list.IndexAfter(new Rank(float.PositiveInfinity, 0)));
			Assert.AreEqual(3, list.IndexAfter(new Rank(float.NegativeInfinity, long.MaxValue)));
		}

		[Test]
		public void Ranks_CompareByPriorityThenOrder()
		{
			Assert.Less(new Rank(2, 9).CompareTo(new Rank(1, 0)), 0, "higher priority first");
			Assert.Less(new Rank(1, 1).CompareTo(new Rank(1, 2)), 0, "then lower order first");
			Assert.Less(Rank.BeforeAll.CompareTo(new Rank(float.PositiveInfinity, long.MinValue + 1)), 0);
			Assert.AreEqual(new Rank(0f, 3), new Rank(-0f, 3), "0 and -0 rank alike");
			Assert.AreEqual(new Rank(0f, 3).GetHashCode(), new Rank(-0f, 3).GetHashCode());
			Assert.Throws<ArgumentOutOfRangeException>(() => new Rank(float.NaN, 0));
		}

		[Test]
		public void IndexAndItemAccess_AreBoundsChecked()
		{
			var list = new RankedList<string>(0);
			list.Add("a", new Rank(0, 0));

			Assert.Throws<ArgumentOutOfRangeException>(() => list.ItemAt(1));
			Assert.Throws<ArgumentOutOfRangeException>(() => list.RankAt(-1));
			Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(1));
		}

		[Test]
		public void SteadyStateUse_AllocatesNothing()
		{
			var list = new RankedList<string>(8);
			long order = 0;

			int Window() => GcAllocations.Count(() =>
			{
				for (int i = 0; i < 6; i++) list.Add("x", new Rank(i % 3, order++));
				for (Rank last = Rank.BeforeAll; ;)
				{
					int next = list.IndexAfter(last);
					if (next == list.Count) break;
					last = list.RankAt(next);
				}

				while (list.RemoveLatest("x")) { }
				list.Clear();
			});

			Window();
			Assert.AreEqual(0, Window());
		}

		private static List<T> Items<T>(RankedList<T> list)
		{
			var items = new List<T>();
			for (int i = 0; i < list.Count; i++) items.Add(list.ItemAt(i));
			return items;
		}
	}
}
