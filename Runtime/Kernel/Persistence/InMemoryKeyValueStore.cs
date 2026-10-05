using System;
using System.Collections.Generic;

namespace AK.Kernel.Persistence
{
	/// <summary>
	/// An <see cref="IKeyValueStore"/> held in memory, for tests, tools and headless runs.
	/// Every write is final at once, so <see cref="Flush"/> only counts its calls.
	/// </summary>
	public sealed class InMemoryKeyValueStore : IKeyValueStore
	{
		private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

		/// <summary>The keys stored, in no particular order.</summary>
		public IReadOnlyCollection<string> Keys => _values.Keys;

		/// <summary>The number of keys stored.</summary>
		public int Count => _values.Count;

		/// <summary>Calls to <see cref="Flush"/> so far.</summary>
		public int FlushCount { get; private set; }

		public bool TryGet(string key, out string value) => _values.TryGetValue(key, out value);

		public bool Contains(string key) => _values.ContainsKey(key);

		public void Set(string key, string value) => _values[key] = value;

		public bool Delete(string key) => _values.Remove(key);

		public void Flush() => FlushCount++;
	}
}
