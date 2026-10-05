namespace AK.Core
{
	/// <summary>
	/// One saved value under one key, cached after the first read.
	///
	/// The cache follows its store: once the store has deleted all its data
	/// (<see cref="PrefsStore.DeleteAll"/>), the property reads as its default again, and
	/// <see cref="Save()"/> writes the default rather than the deleted value.
	///
	/// Nothing to dispose: the property holds no subscriptions. Main thread only.
	/// </summary>
	/// <typeparam name="T">The value's type; anything JsonUtility can serialize.</typeparam>
	public sealed class PrefsProperty<T>
	{
		private readonly string     _saveKey;
		private readonly T          _default;
		private readonly PrefsStore _store;

		private T          _current;
		private bool       _cached;
		private PrefsStore _cachedFrom;
		private int        _cachedGeneration;

		/// <param name="saveKey">The key the value is saved under.</param>
		/// <param name="default">The value until one is saved, and after <see cref="Reset"/>.</param>
		/// <param name="store">The store to save in; null for <see cref="UniPrefs.Store"/>, looked up at each use.</param>
		public PrefsProperty(string saveKey, T @default = default, PrefsStore store = null)
		{
			_saveKey = saveKey;
			_default = @default;
			_store   = store;
			_current = @default;
		}

		/// <summary>The key the value is saved under.</summary>
		public string SaveKey => _saveKey;

		private PrefsStore Store => _store ?? UniPrefs.Store;

		/// <summary>
		/// Saves the current value (<see cref="Read"/>), such as a list changed in place. Never
		/// <c>default(T)</c> unless that is the current value.
		/// </summary>
		public void Save()
		{
			Write(Store, Read());
		}

		/// <summary>Makes <paramref name="value"/> the current value and saves it.</summary>
		public void Save(T value)
		{
			_current = value;
			Write(Store, value);
		}

		/// <summary>The current value: the saved one, or the default when none is saved.</summary>
		public T Read()
		{
			PrefsStore store = Store;
			if (IsCachedFrom(store)) return _current;

			_current = store.TryGet(_saveKey, out T saved) ? saved : _default;
			MarkCached(store);
			return _current;
		}

		/// <summary>Deletes the saved value; the property reads as its default again.</summary>
		public void Reset()
		{
			Store.Delete(_saveKey);

			_current = _default;
			_cached  = false;
		}

		public override string ToString()
		{
			T value = Read();
			return value == null ? string.Empty : value.ToString();
		}

		/// <summary>The current value (<see cref="Read"/>).</summary>
		public static implicit operator T(PrefsProperty<T> property) => property.Read();

		private void Write(PrefsStore store, T value)
		{
			store.Set(_saveKey, value);
			MarkCached(store);
		}

		private bool IsCachedFrom(PrefsStore store)
		{
			return _cached && ReferenceEquals(_cachedFrom, store) && _cachedGeneration == store.Generation;
		}

		private void MarkCached(PrefsStore store)
		{
			_cached           = true;
			_cachedFrom       = store;
			_cachedGeneration = store.Generation;
		}
	}
}
