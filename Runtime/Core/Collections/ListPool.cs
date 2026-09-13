using System;
using System.Collections.Generic;

namespace AK.Core.Collections
{
	/// <summary>
	/// Scratch lists for the "collect, iterate, discard" pattern that otherwise allocates a
	/// fresh <c>List&lt;T&gt;</c> per call. Rent inside a <c>using</c>; the list goes back to
	/// the pool cleared when the scope ends, on every exit path.
	///
	/// <code>
	/// using PooledList&lt;IReward&gt; rewards = ListPool&lt;IReward&gt;.Rent();
	/// item.CollectRewards(rewards.List);
	/// for (int i = 0; i &lt; rewards.List.Count; i++) Grant(rewards.List[i]);
	/// </code>
	///
	/// Do not keep <c>.List</c> past the scope — the next renter gets the same instance.
	/// Main thread only, like everything else that touches Unity state.
	/// </summary>
	public static class ListPool<T>
	{
		private const int MaxPooled     = 32;
		private const int MaxKeptLength = 1024;

		private static readonly Stack<List<T>> Free = new();

		public static PooledList<T> Rent(int minCapacity = 0)
		{
			MainThreadGuard.Assert("ListPool<T>.Rent");

			List<T> list;
			if (Free.Count > 0)
			{
				list = Free.Pop();
				if (list.Capacity < minCapacity) list.Capacity = minCapacity;
			}
			else
			{
				list = new List<T>(Math.Max(minCapacity, 8));
			}

			return new PooledList<T>(list);
		}

		internal static void Return(List<T> list)
		{
			if (list == null) return;

			list.Clear();

			if (Free.Count < MaxPooled && list.Capacity <= MaxKeptLength)
			{
				Free.Push(list);
			}
		}

		/// <summary>How many lists are resting in the pool. For tests and diagnostics.</summary>
		public static int FreeCount => Free.Count;
	}

	/// <summary>
	/// The rented list plus its return-on-dispose. <c>ref struct</c>: it cannot be stored in a
	/// field, captured, boxed, or held across an <c>await</c>, so the list cannot outlive the
	/// scope that rented it.
	/// </summary>
	public readonly ref struct PooledList<T>
	{
		public readonly List<T> List;

		internal PooledList(List<T> list)
		{
			List = list;
		}

		public int Count => List.Count;

		public T this[int index] => List[index];

		public void Add(T item) => List.Add(item);

		public List<T>.Enumerator GetEnumerator() => List.GetEnumerator();

		public void Dispose() => ListPool<T>.Return(List);
	}
}
