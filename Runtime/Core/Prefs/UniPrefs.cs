using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// A wrapper class used for serializing and deserializing data in JSON format.
	/// </summary>
	/// <typeparam name="T">The type of the data to be wrapped.</typeparam>
	[System.Serializable]
	public struct DataWrapper<T>
	{
		/// <summary>
		/// The data to be wrapped.
		/// </summary>
		public T Data;

		/// <summary>
		/// Initializes a new instance of the <see cref="DataWrapper{T}"/> struct with the specified data.
		/// </summary>
		/// <param name="data">The data to be wrapped.</param>
		public DataWrapper(T data) => Data = data;
	}

	/// <summary>
	/// The app's <see cref="PrefsStore"/> over PlayerPrefs (<see cref="Store"/>), and shortcuts
	/// to it. Code that can take a store as a dependency should, so tests can hand it one over
	/// an <see cref="AK.Kernel.Persistence.InMemoryKeyValueStore"/>.
	///
	/// The store is created on first use and lives as long as the scripting domain. Its index of
	/// owned keys has to stay in step with PlayerPrefs, so a second instance over the same
	/// PlayerPrefs is never made. Main thread only.
	/// </summary>
	public static class UniPrefs
	{
		private static PlayerPrefsKeyValueStore _backend;
		private static PrefsStore               _store;

		/// <summary>
		/// The app's store: PlayerPrefs, written to disk at the end of each frame that changed
		/// something, and when the app loses focus or quits (<see cref="PlayerPrefsKeyValueStore"/>).
		/// </summary>
		public static PrefsStore Store => _store ??= CreateStore();

		// A Play session can start without a domain reload, after the editor left the last one
		// without running the flush it had scheduled.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void OnSessionStart() => _backend?.ResetFlushSchedule();

		/// <summary>Stores <paramref name="data"/> under <paramref name="key"/> (<see cref="PrefsStore.Set{T}"/>).</summary>
		public static void Set<T>(string key, T data) => Store.Set(key, data);

		/// <summary>
		/// The value under <paramref name="key"/>, or <paramref name="default"/> when there is
		/// none. An unreadable value is set aside first (<see cref="PrefsStore.TryGet{T}"/>).
		/// </summary>
		public static T Get<T>(string key, T @default = default) => Store.Get(key, @default);

		/// <summary>The value under <paramref name="key"/>, if there is a readable one (<see cref="PrefsStore.TryGet{T}"/>).</summary>
		public static bool TryGet<T>(string key, out T value) => Store.TryGet(key, out value);

		/// <summary>True when <paramref name="key"/> holds a value.</summary>
		public static bool HasKey(string key) => Store.Has(key);

		/// <summary>Deletes the value under <paramref name="key"/>.</summary>
		public static void Delete(string key) => Store.Delete(key);

		/// <summary>
		/// Deletes every key the store owns, and nothing else in PlayerPrefs, then resets the live
		/// models over it, so their next save starts from defaults instead of writing the deleted
		/// data back (<see cref="PrefsStore.DeleteAll"/>).
		/// </summary>
		public static void DeleteAll() => Store.DeleteAll();

		/// <summary>Writes every earlier change to disk now.</summary>
		public static void Flush() => Store.Flush();

		private static PrefsStore CreateStore()
		{
			_backend = new PlayerPrefsKeyValueStore();
			return new PrefsStore(_backend);
		}
	}
}
