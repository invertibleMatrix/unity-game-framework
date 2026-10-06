using System;
using System.Collections.Generic;

namespace AK.Kernel.Collections
{
	/// <summary>
	/// Bookkeeping for reference-counted acquisitions: per key, every handle acquired for it.
	///
	/// Each <see cref="Acquire"/> is matched by exactly one <see cref="TryRelease"/>, which hands
	/// one handle back for the caller to release; the key is forgotten when its last handle
	/// goes. Keeping every handle, rather than one handle and a count, means the table never
	/// assumes that two acquisitions share an underlying resource. Each handle is released
	/// exactly once, so the backing system's own reference count stays balanced whatever it
	/// does with repeat requests (for example, Addressables raises an operation's count on every
	/// load of a cached key, and two loads of one asset by different types are two operations).
	///
	/// Handles come back most recent first. A key's first handle is stored inline and the stacks
	/// for repeat acquisitions are pooled, so steady-state use does not allocate. Keys are
	/// compared with the supplied comparer (default: <see cref="EqualityComparer{T}.Default"/>).
	/// Not thread-safe.
	/// </summary>
	public sealed class ClaimTable<TKey, THandle>
	{
		private struct Entry
		{
			public THandle First;

			/// <summary>Handles acquired after the first; null until the key is acquired twice.</summary>
			public Stack<THandle> Repeats;
		}

		// An unconstrained `key == null` boxes a value-type key on Mono, so the null test only
		// runs for key types that can actually be null.
		private static readonly bool KeysCanBeNull = default(TKey) == null;

		private readonly Dictionary<TKey, Entry> _entries;
		private readonly Stack<Stack<THandle>> _repeatPool = new();
		private int _claimCount;

		public ClaimTable() : this(0)
		{
		}

		public ClaimTable(int capacity, IEqualityComparer<TKey> comparer = null)
		{
			_entries = new Dictionary<TKey, Entry>(Math.Max(0, capacity), comparer);
		}

		/// <summary>Keys holding at least one claim.</summary>
		public int Count => _entries.Count;

		/// <summary>Outstanding claims across every key.</summary>
		public int ClaimCount => _claimCount;

		public bool Contains(TKey key) => !IsNull(key) && _entries.ContainsKey(key);

		/// <summary>Outstanding claims on <paramref name="key"/>; 0 when it holds none.</summary>
		public int ClaimsOf(TKey key)
		{
			if (IsNull(key) || !_entries.TryGetValue(key, out Entry entry))
			{
				return 0;
			}

			return 1 + (entry.Repeats?.Count ?? 0);
		}

		/// <summary>Records one claim on <paramref name="key"/>, backed by <paramref name="handle"/>.</summary>
		public void Acquire(TKey key, THandle handle)
		{
			if (IsNull(key))
			{
				throw new ArgumentNullException(nameof(key));
			}

			if (_entries.TryGetValue(key, out Entry entry))
			{
				if (entry.Repeats == null)
				{
					entry.Repeats = RentRepeats();
					_entries[key] = entry;
				}

				entry.Repeats.Push(handle);
			}
			else
			{
				_entries.Add(key, new Entry { First = handle });
			}

			_claimCount++;
		}

		/// <summary>
		/// Takes back one claim on <paramref name="key"/> and returns the handle the caller must
		/// now release: the most recently acquired one. The key is forgotten with its last claim.
		/// False, with a default handle, when the key holds no claims.
		/// </summary>
		public bool TryRelease(TKey key, out THandle handle)
		{
			if (IsNull(key) || !_entries.TryGetValue(key, out Entry entry))
			{
				handle = default;
				return false;
			}

			if (entry.Repeats != null)
			{
				handle = entry.Repeats.Pop();
				if (entry.Repeats.Count == 0)
				{
					ReturnRepeats(entry.Repeats);
					entry.Repeats = null;
					_entries[key] = entry;
				}
			}
			else
			{
				handle = entry.First;
				_entries.Remove(key);
			}

			_claimCount--;
			return true;
		}

		/// <summary>
		/// Takes back every claim on <paramref name="key"/>, appending their handles to
		/// <paramref name="released"/> (most recent first), and forgets the key. Returns how many
		/// handles were appended.
		/// </summary>
		public int Drain(TKey key, List<THandle> released)
		{
			if (released == null)
			{
				throw new ArgumentNullException(nameof(released));
			}

			if (IsNull(key) || !_entries.TryGetValue(key, out Entry entry))
			{
				return 0;
			}

			_entries.Remove(key);
			int taken = TakeAll(entry, released);
			_claimCount -= taken;
			return taken;
		}

		/// <summary>
		/// Takes back every claim in the table, appending their handles to
		/// <paramref name="released"/>, and empties it. Returns how many handles were appended.
		/// </summary>
		public int DrainAll(List<THandle> released)
		{
			if (released == null)
			{
				throw new ArgumentNullException(nameof(released));
			}

			int taken = 0;
			foreach (Entry entry in _entries.Values)
			{
				taken += TakeAll(entry, released);
			}

			_entries.Clear();
			_claimCount = 0;
			return taken;
		}

		/// <summary>Appends every key that holds claims to <paramref name="destination"/>.</summary>
		public void CopyKeysTo(List<TKey> destination)
		{
			if (destination == null)
			{
				throw new ArgumentNullException(nameof(destination));
			}

			foreach (TKey key in _entries.Keys)
			{
				destination.Add(key);
			}
		}

		private static bool IsNull(TKey key) => KeysCanBeNull && key == null;

		private int TakeAll(Entry entry, List<THandle> released)
		{
			int taken = 1;

			if (entry.Repeats != null)
			{
				taken += entry.Repeats.Count;
				while (entry.Repeats.Count > 0)
				{
					released.Add(entry.Repeats.Pop());
				}

				ReturnRepeats(entry.Repeats);
			}

			released.Add(entry.First);
			return taken;
		}

		private Stack<THandle> RentRepeats() => _repeatPool.Count > 0 ? _repeatPool.Pop() : new Stack<THandle>(2);

		private void ReturnRepeats(Stack<THandle> repeats)
		{
			repeats.Clear();
			_repeatPool.Push(repeats);
		}
	}
}
