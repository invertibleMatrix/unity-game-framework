using System;
using System.Collections.Generic;
using System.Globalization;

namespace AK.Kernel.Persistence
{
	/// <summary>
	/// An app's records in an <see cref="IKeyValueStore"/> that it may share with other code,
	/// such as PlayerPrefs. The store knows which keys are its own, so it can delete them all
	/// without touching anyone else's. It sets an unreadable value aside rather than letting the
	/// next write destroy it, and it tells live readers when their data was deleted.
	/// <list type="bullet">
	/// <item><b>Ownership.</b> Every key written, or found holding a value when read, is recorded
	/// in an index stored under <see cref="StorageKeys.Index"/>. A backend such as PlayerPrefs
	/// can't list its keys, so the index is how they are found again. Reads adopt keys written
	/// before the index existed.</item>
	/// <item><b>Quarantine.</b> <see cref="Quarantine"/> moves a value to
	/// <c>{key}.corrupt.{utc}</c> (see <see cref="StorageKeys"/>) and keeps the newest
	/// <see cref="QuarantineLimit"/> such copies of each key.</item>
	/// <item><b>Reset.</b> <see cref="DeleteAll"/> deletes every owned key, flushes, advances
	/// <see cref="Generation"/> and notifies each <see cref="IStoreResetListener"/>. Listeners
	/// are held weakly, so registering one never keeps it alive.</item>
	/// </list>
	/// Writes reach the backend at once; when they become durable is the backend's flush
	/// policy. Not thread-safe.
	/// </summary>
	public sealed class RecordStore
	{
		/// <summary>The set-aside copies of one key kept by default.</summary>
		public const int DefaultQuarantineLimit = 3;

		private const char IndexSeparator = '\n';

		private readonly IKeyValueStore _backend;

		// Sorted ordinally, so a key's set-aside copies come oldest first and the index text is stable.
		private readonly SortedSet<string> _owned = new(StringComparer.Ordinal);

		private readonly List<WeakReference<IStoreResetListener>> _listeners = new();
		private readonly List<string>                             _scratch   = new();

		/// <param name="backend">Where the records live.</param>
		/// <param name="quarantineLimit">How many set-aside copies of one key to keep; at least 1.</param>
		/// <exception cref="ArgumentNullException"><paramref name="backend"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="quarantineLimit"/> is below 1.</exception>
		public RecordStore(IKeyValueStore backend, int quarantineLimit = DefaultQuarantineLimit)
		{
			_backend = backend ?? throw new ArgumentNullException(nameof(backend));

			if (quarantineLimit < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(quarantineLimit), quarantineLimit, "At least one set-aside copy must be kept.");
			}

			QuarantineLimit = quarantineLimit;
			LoadIndex();
		}

		/// <summary>How many set-aside copies of one key are kept. Setting another aside deletes the oldest.</summary>
		public int QuarantineLimit { get; }

		/// <summary>Advances each time <see cref="DeleteAll"/> runs, so a cache can tell its data is gone.</summary>
		public int Generation { get; private set; }

		/// <summary>The number of keys this store owns.</summary>
		public int OwnedCount => _owned.Count;

		/// <summary>Adds the keys this store owns to <paramref name="into"/>, in ordinal order.</summary>
		public void GetOwnedKeys(ICollection<string> into)
		{
			if (into == null) throw new ArgumentNullException(nameof(into));

			foreach (string key in _owned)
			{
				into.Add(key);
			}
		}

		/// <summary>True when <paramref name="key"/> holds a value.</summary>
		public bool Contains(string key)
		{
			StorageKeys.Validate(key);
			return _backend.Contains(key);
		}

		/// <summary>The value under <paramref name="key"/>, if any. A key found holding a value becomes owned.</summary>
		public bool TryRead(string key, out string value)
		{
			StorageKeys.Validate(key);
			if (!_backend.TryGet(key, out value)) return false;

			Own(key);
			return true;
		}

		/// <summary>Stores <paramref name="value"/> under <paramref name="key"/>, which becomes owned.</summary>
		/// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
		public void Write(string key, string value)
		{
			StorageKeys.Validate(key);
			if (value == null) throw new ArgumentNullException(nameof(value));

			_backend.Set(key, value);
			Own(key);
		}

		/// <summary>Deletes the value under <paramref name="key"/>. False when there was none.</summary>
		public bool Delete(string key)
		{
			StorageKeys.Validate(key);

			bool existed = _backend.Delete(key);
			if (_owned.Remove(key)) SaveIndex();

			return existed;
		}

		/// <summary>
		/// Moves the value under <paramref name="fromKey"/> to <paramref name="toKey"/>. False,
		/// changing nothing, when <paramref name="fromKey"/> holds no value.
		/// </summary>
		/// <exception cref="ArgumentException">The keys are the same.</exception>
		/// <exception cref="InvalidOperationException"><paramref name="toKey"/> already holds a value; the move would destroy it.</exception>
		public bool Move(string fromKey, string toKey)
		{
			StorageKeys.Validate(fromKey);
			StorageKeys.Validate(toKey);

			if (string.Equals(fromKey, toKey, StringComparison.Ordinal))
			{
				throw new ArgumentException($"Can't move '{fromKey}' onto itself.", nameof(toKey));
			}

			if (_backend.Contains(toKey))
			{
				throw new InvalidOperationException($"'{toKey}' already holds a value; moving '{fromKey}' there would destroy it.");
			}

			if (!_backend.TryGet(fromKey, out string value)) return false;

			_backend.Set(toKey, value);
			_backend.Delete(fromKey);

			_owned.Remove(fromKey);
			_owned.Add(toKey);
			SaveIndex();
			return true;
		}

		/// <summary>
		/// Sets the value under <paramref name="key"/> aside as unreadable: moves it to
		/// <see cref="StorageKeys.Quarantined"/> for <paramref name="utcNow"/>, leaving
		/// <paramref name="key"/> empty, and deletes the oldest copies beyond
		/// <see cref="QuarantineLimit"/>. Returns the key the value went to, or null when
		/// <paramref name="key"/> held nothing.
		/// </summary>
		public string Quarantine(string key, DateTime utcNow)
		{
			StorageKeys.Validate(key);
			if (!_backend.TryGet(key, out string value)) return null;

			string stem   = StorageKeys.Quarantined(key, utcNow);
			string target = stem;
			for (int attempt = 2; _backend.Contains(target); attempt++)
			{
				target = stem + "-" + attempt.ToString(CultureInfo.InvariantCulture);
			}

			_backend.Set(target, value);
			_backend.Delete(key);

			_owned.Remove(key);
			_owned.Add(target);
			TrimQuarantine(key);
			SaveIndex();
			return target;
		}

		/// <summary>
		/// Deletes every key this store owns and its index, flushes, advances
		/// <see cref="Generation"/>, then notifies the reset listeners in the order they
		/// registered. Keys the store doesn't own are left alone.
		/// </summary>
		/// <exception cref="AggregateException">Listeners threw. Every listener was still notified.</exception>
		public void DeleteAll()
		{
			foreach (string key in _owned)
			{
				_backend.Delete(key);
			}

			_owned.Clear();
			_backend.Delete(StorageKeys.Index);
			_backend.Flush();

			Generation++;
			NotifyReset();
		}

		/// <summary>Makes every earlier write durable.</summary>
		public void Flush() => _backend.Flush();

		/// <summary>
		/// Registers <paramref name="listener"/> for <see cref="DeleteAll"/>, once however often
		/// it is added. Held weakly: the registration ends when the listener is collected.
		/// </summary>
		public void AddResetListener(IStoreResetListener listener)
		{
			if (listener == null) throw new ArgumentNullException(nameof(listener));

			for (int i = _listeners.Count - 1; i >= 0; i--)
			{
				if (!_listeners[i].TryGetTarget(out IStoreResetListener registered))
				{
					_listeners.RemoveAt(i);
				}
				else if (ReferenceEquals(registered, listener))
				{
					return;
				}
			}

			_listeners.Add(new WeakReference<IStoreResetListener>(listener));
		}

		/// <summary>Ends <paramref name="listener"/>'s registration, if it has one.</summary>
		public void RemoveResetListener(IStoreResetListener listener)
		{
			if (listener == null) throw new ArgumentNullException(nameof(listener));

			for (int i = _listeners.Count - 1; i >= 0; i--)
			{
				if (!_listeners[i].TryGetTarget(out IStoreResetListener registered) || ReferenceEquals(registered, listener))
				{
					_listeners.RemoveAt(i);
				}
			}
		}

		private void NotifyReset()
		{
			// Collected first: a listener may register, unregister or reset the store again
			// while being notified.
			var live = new List<IStoreResetListener>(_listeners.Count);
			for (int i = 0; i < _listeners.Count; i++)
			{
				if (_listeners[i].TryGetTarget(out IStoreResetListener listener))
				{
					live.Add(listener);
				}
			}

			List<Exception> failures = null;
			for (int i = 0; i < live.Count; i++)
			{
				try
				{
					live[i].OnStoreReset();
				}
				catch (Exception e)
				{
					(failures ??= new List<Exception>()).Add(e);
				}
			}

			if (failures != null)
			{
				throw new AggregateException("Store reset listeners failed; every listener was still notified.", failures);
			}
		}

		private void Own(string key)
		{
			if (_owned.Add(key)) SaveIndex();
		}

		private void TrimQuarantine(string key)
		{
			_scratch.Clear();
			foreach (string owned in _owned)
			{
				if (StorageKeys.IsQuarantineOf(owned, key)) _scratch.Add(owned);
			}

			for (int i = 0; i < _scratch.Count - QuarantineLimit; i++)
			{
				_backend.Delete(_scratch[i]);
				_owned.Remove(_scratch[i]);
			}

			_scratch.Clear();
		}

		private void LoadIndex()
		{
			if (!_backend.TryGet(StorageKeys.Index, out string text) || string.IsNullOrEmpty(text)) return;

			int start = 0;
			while (start < text.Length)
			{
				int end = text.IndexOf(IndexSeparator, start);
				if (end < 0) end = text.Length;

				if (end > start)
				{
					string key = text.Substring(start, end - start);
					if (StorageKeys.IsValid(key)) _owned.Add(key);
				}

				start = end + 1;
			}
		}

		private void SaveIndex()
		{
			if (_owned.Count == 0)
			{
				_backend.Delete(StorageKeys.Index);
				return;
			}

			_backend.Set(StorageKeys.Index, string.Join(IndexSeparator, _owned));
		}
	}
}
