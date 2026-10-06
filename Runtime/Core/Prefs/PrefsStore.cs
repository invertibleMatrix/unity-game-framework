using System;
using System.Collections.Generic;
using AK.Kernel.Persistence;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// Typed values in a <see cref="RecordStore"/>, each stored as JsonUtility JSON of a
	/// <see cref="DataWrapper{T}"/>: <c>{"Data":…}</c>.
	///
	/// Reading never loses data. A value is unreadable when it isn't one well-formed JSON object
	/// with a <c>Data</c> member of the kind <c>T</c> is saved as, or when JsonUtility can't read
	/// it. An unreadable value is set aside under <c>{key}.corrupt.{utc}</c>, an error names
	/// both keys, and the read reports nothing stored. The caller starts fresh, and its next
	/// save can't overwrite the data that couldn't be read.
	///
	/// <see cref="DeleteAll"/> deletes only the keys this store owns, then resets the listeners
	/// registered with <see cref="AddResetListener"/>. Main thread only.
	/// </summary>
	public sealed class PrefsStore
	{
		private const string LogTag = "[UniPrefs]";

		private static readonly Func<DateTime> SystemUtcNow = () => DateTime.UtcNow;

		private readonly RecordStore    _records;
		private readonly Func<DateTime> _utcNow;

		/// <param name="backend">Where the values live.</param>
		/// <param name="utcNow">The clock that times set-aside values; the system clock by default.</param>
		public PrefsStore(IKeyValueStore backend, Func<DateTime> utcNow = null)
			: this(new RecordStore(backend), utcNow)
		{
		}

		/// <param name="records">The records the values live in.</param>
		/// <param name="utcNow">The clock that times set-aside values; the system clock by default.</param>
		public PrefsStore(RecordStore records, Func<DateTime> utcNow = null)
		{
			_records = records ?? throw new ArgumentNullException(nameof(records));
			_utcNow  = utcNow ?? SystemUtcNow;
		}

		/// <summary>The records underneath: owned keys, set-aside copies and the reset generation.</summary>
		public RecordStore Records => _records;

		/// <summary>Advances each time <see cref="DeleteAll"/> runs (<see cref="RecordStore.Generation"/>).</summary>
		public int Generation => _records.Generation;

		/// <summary>Stores <paramref name="value"/> under <paramref name="key"/>.</summary>
		public void Set<T>(string key, T value)
		{
			_records.Write(key, JsonUtility.ToJson(new DataWrapper<T>(value)));
		}

		/// <summary>
		/// The value under <paramref name="key"/>. False when nothing is stored there, or when
		/// the value was unreadable and has just been set aside.
		/// </summary>
		public bool TryGet<T>(string key, out T value)
		{
			value = default;

			// PlayerPrefs answers a missing string with an empty one; treat both the same.
			if (!_records.TryRead(key, out string json) || json.Length == 0) return false;

			string problem = Inspect<T>(json);
			if (problem == null)
			{
				try
				{
					value = JsonUtility.FromJson<DataWrapper<T>>(json).Data;
					if (value != null) return true;

					problem = "it reads back as null";
				}
				catch (Exception e)
				{
					problem = "JsonUtility can't read it: " + e.Message;
				}
			}

			Quarantine(key, problem);
			value = default;
			return false;
		}

		/// <summary>The value under <paramref name="key"/>, or <paramref name="default"/> when there is none (see <see cref="TryGet{T}"/>).</summary>
		public T Get<T>(string key, T @default = default) => TryGet(key, out T value) ? value : @default;

		/// <summary>True when <paramref name="key"/> holds a value, readable or not.</summary>
		public bool Has(string key) => _records.Contains(key);

		/// <summary>Deletes the value under <paramref name="key"/>. False when there was none.</summary>
		public bool Delete(string key) => _records.Delete(key);

		/// <summary>
		/// Moves the value under <paramref name="fromKey"/> to the empty <paramref name="toKey"/>
		/// (<see cref="RecordStore.Move"/>). False when <paramref name="fromKey"/> holds nothing.
		/// </summary>
		public bool Move(string fromKey, string toKey) => _records.Move(fromKey, toKey);

		/// <summary>
		/// Sets the value under <paramref name="key"/> aside, logging <paramref name="reason"/>,
		/// so <paramref name="key"/> starts fresh. Returns the key the value went to, or null when
		/// <paramref name="key"/> held nothing.
		/// </summary>
		public string Quarantine(string key, string reason)
		{
			string kept = _records.Quarantine(key, _utcNow());
			if (kept != null)
			{
				Debug.LogError($"{LogTag} '{key}' can't be used: {reason}. Its value is kept under '{kept}', and '{key}' starts fresh.");
			}

			return kept;
		}

		/// <summary>Deletes every key this store owns, flushes, then resets the registered listeners (<see cref="RecordStore.DeleteAll"/>).</summary>
		public void DeleteAll() => _records.DeleteAll();

		/// <summary>Makes every earlier write durable.</summary>
		public void Flush() => _records.Flush();

		/// <summary>Registers <paramref name="listener"/>, weakly, for <see cref="DeleteAll"/>.</summary>
		public void AddResetListener(IStoreResetListener listener) => _records.AddResetListener(listener);

		/// <summary>Ends <paramref name="listener"/>'s registration, if it has one.</summary>
		public void RemoveResetListener(IStoreResetListener listener) => _records.RemoveResetListener(listener);

		/// <summary>Why <paramref name="json"/> can't hold a <c>T</c>, or null when its shape is right.</summary>
		private static string Inspect<T>(string json)
		{
			JsonShape shape = JsonEnvelope.Inspect(json, nameof(DataWrapper<T>.Data), out JsonKind kind);

			switch (shape)
			{
				case JsonShape.Malformed:   return "it isn't well-formed JSON";
				case JsonShape.NotAnObject: return "it isn't a JSON object";
			}

			if (kind == JsonKind.None) return "it has no \"Data\" member";

			JsonKind expected = SavedKind<T>.Value;
			if (expected != JsonKind.None && kind != expected)
			{
				return $"its \"Data\" member is {Describe(kind)}, but {typeof(T).Name} is saved as {Describe(expected)}";
			}

			return null;
		}

		private static string Describe(JsonKind kind) => kind switch
		{
			JsonKind.Object  => "an object",
			JsonKind.Array   => "an array",
			JsonKind.String  => "a string",
			JsonKind.Number  => "a number",
			JsonKind.Boolean => "a boolean",
			JsonKind.Null    => "null",
			_                => "nothing",
		};

		/// <summary>The kind of JSON value JsonUtility writes for a <c>T</c>, worked out once per type.</summary>
		private static class SavedKind<T>
		{
			public static readonly JsonKind Value = Of(typeof(T));

			// None means the kind isn't checked: types JsonUtility can't save, or whose form isn't pinned down here.
			private static JsonKind Of(Type type)
			{
				if (type == typeof(string)) return JsonKind.String;
				if (type == typeof(bool))   return JsonKind.Boolean;
				if (type.IsEnum)            return JsonKind.Number;

				if (type.IsPrimitive)
				{
					return type == typeof(char) || type == typeof(IntPtr) || type == typeof(UIntPtr) ? JsonKind.None : JsonKind.Number;
				}

				if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return JsonKind.Array;

				if (type == typeof(decimal) || type == typeof(object) || type.IsInterface || type.IsAbstract ||
				    Nullable.GetUnderlyingType(type) != null)
				{
					return JsonKind.None;
				}

				return JsonKind.Object;
			}
		}
	}
}
