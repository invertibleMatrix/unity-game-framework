using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AK.Core.Collections
{
	/// <summary>
	/// Dense, generational object storage addressed by <see cref="Handle{T}"/>.
	///
	/// Three parallel arrays — items, generations, and a free list threaded through the dead
	/// slots — so that Add, Remove, and TryGet are each O(1) with no allocation once the
	/// arrays have grown to working size. A removed slot's generation advances, which
	/// invalidates every outstanding handle to it; the slot is then reused by the next Add.
	///
	/// Iteration walks the item array once and skips dead slots. The enumerator is a struct,
	/// so <c>foreach</c> does not allocate. Removing during enumeration is allowed (the
	/// enumerator reads generation per slot), adding is not — the array may reallocate.
	///
	/// Not thread-safe. Odd generations are live, even generations are free, so a stale
	/// handle can never match a free slot by accident.
	/// </summary>
	public sealed class SlotMap<T>
	{
		private const int  EndOfList = -1;
		private const uint FirstLive = 1;

		private T[]    _items;
		private uint[] _generations;
		private int[]  _nextFree;

		private int _freeHead = EndOfList;
		private int _count;
		private int _highWater;

		public SlotMap(int capacity = 16)
		{
			if (capacity < 1) capacity = 1;

			_items       = new T[capacity];
			_generations = new uint[capacity];
			_nextFree    = new int[capacity];
		}

		/// <summary>Live objects.</summary>
		public int Count => _count;

		/// <summary>Slots allocated so far, live or dead. Bounds every valid <c>Handle.Index</c>.</summary>
		public int Capacity => _items.Length;

		// ---------------------------------------------------------------- mutation

		public Handle<T> Add(T item)
		{
			int index;

			if (_freeHead != EndOfList)
			{
				index     = _freeHead;
				_freeHead = _nextFree[index];
			}
			else
			{
				if (_highWater == _items.Length) Grow();
				index = _highWater++;
			}

			_items[index]        = item;
			_generations[index] += 1;
			_count++;

			return new Handle<T>(index, _generations[index]);
		}

		/// <summary>
		/// Frees the slot and invalidates every handle to it. False if the handle was already
		/// dead or never belonged here — never throws, because a stale handle is the expected case.
		/// </summary>
		public bool Remove(Handle<T> handle)
		{
			if (!IsLive(handle)) return false;

			int index = handle.Index;

			_items[index]        = default;
			_generations[index] += 1;
			_nextFree[index]     = _freeHead;
			_freeHead            = index;
			_count--;

			return true;
		}

		/// <summary>Remove that also hands back what was stored, for callers that own cleanup.</summary>
		public bool Remove(Handle<T> handle, out T removed)
		{
			if (!IsLive(handle))
			{
				removed = default;
				return false;
			}

			removed = _items[handle.Index];
			Remove(handle);
			return true;
		}

		/// <summary>Frees every slot. Outstanding handles all become stale. Keeps the arrays.</summary>
		public void Clear()
		{
			for (int i = 0; i < _highWater; i++)
			{
				if ((_generations[i] & 1) == FirstLive)
				{
					_generations[i] += 1;
				}

				_items[i] = default;
			}

			_freeHead  = EndOfList;
			_highWater = 0;
			_count     = 0;
		}

		// ---------------------------------------------------------------- access

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(Handle<T> handle) => IsLive(handle);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGet(Handle<T> handle, out T item)
		{
			if (IsLive(handle))
			{
				item = _items[handle.Index];
				return true;
			}

			item = default;
			return false;
		}

		/// <summary>Resolve-or-default. Prefer <see cref="TryGet"/> where a stale handle has meaning.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Get(Handle<T> handle) => IsLive(handle) ? _items[handle.Index] : default;

		/// <summary>
		/// Direct slot access for hot loops that already validated the handle this frame.
		/// No generation check; an index outside the allocated range throws.
		/// </summary>
		public ref T GetRefUnchecked(int index) => ref _items[index];

		/// <summary>Re-creates the handle for a slot known to be live (e.g. from an index kept during iteration).</summary>
		public Handle<T> HandleAt(int index)
		{
			return (uint)index < (uint)_highWater && (_generations[index] & 1) == FirstLive
				? new Handle<T>(index, _generations[index])
				: Handle<T>.Invalid;
		}

		// ---------------------------------------------------------------- iteration

		public Enumerator GetEnumerator() => new(this);

		/// <summary>Struct enumerator over live items. Skips dead slots; tolerates Remove mid-walk.</summary>
		public struct Enumerator : IEnumerator<T>
		{
			private readonly SlotMap<T> _map;
			private int _index;

			internal Enumerator(SlotMap<T> map)
			{
				_map   = map;
				_index = -1;
			}

			public T Current => _map._items[_index];

			/// <summary>Handle of the current item, for callers that need to remove or keep a reference while walking.</summary>
			public Handle<T> CurrentHandle => new(_index, _map._generations[_index]);

			object IEnumerator.Current => Current;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				uint[] gens = _map._generations;
				int    end  = _map._highWater;

				while (++_index < end)
				{
					if ((gens[_index] & 1) == FirstLive) return true;
				}

				return false;
			}

			public void Reset() => _index = -1;

			public void Dispose() { }
		}

		// ---------------------------------------------------------------- internals

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsLive(Handle<T> handle)
		{
			int index = handle.Index;
			return (uint)index < (uint)_highWater
			    && handle.Generation != 0
			    && _generations[index] == handle.Generation;
		}

		private void Grow()
		{
			int newCapacity = _items.Length * 2;

			Array.Resize(ref _items, newCapacity);
			Array.Resize(ref _generations, newCapacity);
			Array.Resize(ref _nextFree, newCapacity);
		}
	}
}
