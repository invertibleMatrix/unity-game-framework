using System;
using System.Collections.Generic;

namespace AK.Kernel.Collections
{
	/// <summary>
	/// Items in <see cref="Rank"/> order, for walks that must survive changes made while they
	/// walk, such as listeners that subscribe or unsubscribe while an event is delivered.
	///
	/// A walk keeps the rank of the item it visited last and asks for the first one after it
	/// (<see cref="IndexAfter"/>). An item removed meanwhile is never reached; an item added
	/// meanwhile is reached if it ranks after the last one visited, and skipped otherwise. Walks
	/// can nest, and nothing tracks them, so nothing has to be fixed up when the list changes.
	///
	/// Each step of a walk is a binary search. Adding and removing shift the items after the
	/// change. Steady-state use doesn't allocate. Not thread-safe.
	/// </summary>
	public sealed class RankedList<T>
	{
		private Entry[] _entries;
		private int     _count;

		public RankedList(int capacity = 4)
		{
			_entries = capacity > 0 ? new Entry[capacity] : Array.Empty<Entry>();
		}

		public int Count => _count;

		public T ItemAt(int index) => _entries[CheckIndex(index)].Item;

		public Rank RankAt(int index) => _entries[CheckIndex(index)].Rank;

		/// <summary>Adds <paramref name="item"/> at <paramref name="rank"/>, which no item in the list may have.</summary>
		/// <exception cref="ArgumentException">An item already has <paramref name="rank"/>.</exception>
		public void Add(T item, Rank rank)
		{
			int index = IndexAfter(rank);
			if (index > 0 && _entries[index - 1].Rank == rank)
			{
				throw new ArgumentException($"An item already has rank {rank}.", nameof(rank));
			}

			if (_count == _entries.Length)
			{
				Array.Resize(ref _entries, Math.Max(4, _count * 2));
			}

			Array.Copy(_entries, index, _entries, index + 1, _count - index);
			_entries[index] = new Entry(item, rank);
			_count++;
		}

		/// <summary>
		/// Removes the item equal to <paramref name="item"/> that has the highest order: the one
		/// added last, when orders grow as items are added. False when none is equal.
		/// </summary>
		public bool RemoveLatest(T item, IEqualityComparer<T> comparer = null)
		{
			comparer ??= EqualityComparer<T>.Default;

			int found = -1;
			for (int i = 0; i < _count; i++)
			{
				if (comparer.Equals(_entries[i].Item, item) && (found < 0 || _entries[i].Rank.Order > _entries[found].Rank.Order))
				{
					found = i;
				}
			}

			if (found < 0) return false;

			RemoveAt(found);
			return true;
		}

		public void RemoveAt(int index)
		{
			CheckIndex(index);

			_count--;
			Array.Copy(_entries, index + 1, _entries, index, _count - index);
			_entries[_count] = default;
		}

		/// <summary>The index of the first item ranked after <paramref name="rank"/>, or <see cref="Count"/> when none is.</summary>
		public int IndexAfter(Rank rank)
		{
			int low = 0;
			int high = _count;

			while (low < high)
			{
				int middle = (int)((uint)(low + high) >> 1);
				if (_entries[middle].Rank.CompareTo(rank) > 0)
				{
					high = middle;
				}
				else
				{
					low = middle + 1;
				}
			}

			return low;
		}

		public void Clear()
		{
			Array.Clear(_entries, 0, _count);
			_count = 0;
		}

		private int CheckIndex(int index)
		{
			if ((uint)index >= (uint)_count)
			{
				throw new ArgumentOutOfRangeException(nameof(index), index, $"The list has {_count} items.");
			}

			return index;
		}

		private readonly struct Entry
		{
			public readonly T    Item;
			public readonly Rank Rank;

			public Entry(T item, Rank rank)
			{
				Item = item;
				Rank = rank;
			}
		}
	}
}
