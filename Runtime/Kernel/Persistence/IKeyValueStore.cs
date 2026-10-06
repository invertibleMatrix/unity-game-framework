namespace AK.Kernel.Persistence
{
	/// <summary>
	/// The storage port: a flat map from string keys to string values, such as PlayerPrefs, a
	/// file or memory. Engine adapters implement it; everything above it is engine-free.
	///
	/// Writes may be buffered. <see cref="Flush"/> makes every earlier write durable, and an
	/// adapter may also flush on its own schedule, such as once a frame. Reads always see the
	/// earlier writes, flushed or not.
	///
	/// The layer above validates keys (<see cref="StorageKeys.Validate"/>) and never passes a
	/// null value, so an adapter may assume a key is non-empty and free of control characters.
	/// Not thread-safe.
	/// </summary>
	public interface IKeyValueStore
	{
		/// <summary>The value stored under <paramref name="key"/>, if any.</summary>
		bool TryGet(string key, out string value);

		/// <summary>True when a value is stored under <paramref name="key"/>.</summary>
		bool Contains(string key);

		/// <summary>Stores <paramref name="value"/> under <paramref name="key"/>, replacing any value there.</summary>
		void Set(string key, string value);

		/// <summary>Removes the value under <paramref name="key"/>. False when there was none.</summary>
		bool Delete(string key);

		/// <summary>Makes every earlier write durable.</summary>
		void Flush();
	}
}
