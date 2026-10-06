using System;
using AK.Kernel.Persistence;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// Time helpers for saved data, usable without the generic type parameter. Times are saved
	/// as ISO 8601 text in the invariant culture (<see cref="PersistedTime"/>).
	/// </summary>
	public static class PersistableState
	{
		/// <summary><paramref name="dateTime"/> as saved text: a UTC time ends in Z, a local one in its offset.</summary>
		public static string GetFormattedTime(DateTime dateTime) => PersistedTime.Format(dateTime);

		/// <summary>Reads saved time text as UTC. False, with <paramref name="utc"/> left default, when it isn't an ISO 8601 time.</summary>
		public static bool TryGetDateTime(string text, out DateTime utc) => PersistedTime.TryParse(text, out utc);

		/// <summary>Saved time text as UTC, or <c>default(DateTime)</c> when it isn't an ISO 8601 time.</summary>
		public static DateTime GetDateTimeFromString(string dt) => PersistedTime.TryParse(dt, out DateTime utc) ? utc : default;
	}

	/// <summary>
	/// Base class for saved game state: one JsonUtility-serialized object under one key, with
	/// session tracking and versioned migration. Games extend it with their own fields.
	///
	/// <see cref="Load()"/> reads the state, or starts a fresh one.
	/// <list type="bullet">
	/// <item>A save from an older <see cref="CurrentSaveVersion"/> is migrated (<see cref="OnMigrate"/>)
	/// and written back. A fresh state is born at the current version and never migrates.</item>
	/// <item>A save from a newer version is set aside, like an unreadable one, and the state
	/// starts fresh. This build would drop the fields it doesn't know on its next save.</item>
	/// <item>The loaded instance saves to the store and key it came from. When that store deletes
	/// all its data, the instance goes back to a fresh state, so its next <see cref="Commit"/>
	/// can't write the deleted data back.</item>
	/// </list>
	///
	/// Override <see cref="SaveKey"/> to give each model its own key. It defaults to
	/// "UGFW_GAME_MODEL" for backward compatibility.
	/// <code>
	/// [Serializable]
	/// public class MyGameModel : PersistableState&lt;MyGameModel&gt;
	/// {
	///     protected override string SaveKey => "MY_GAME_SAVE";
	///     public int TotalStars;
	/// }
	/// </code>
	/// Main thread only.
	/// </summary>
	[Serializable]
	public abstract class PersistableState<T> : ISerializationCallbackReceiver where T : PersistableState<T>, new()
	{
		[Tooltip("Save format version for migration.")]
		public int SaveVersion = 1;

		[Tooltip("Incremented by Initialize each time the game starts: 1 in the first session.")]
		public int CurrentSession;

		[Tooltip("Incremented each new UTC calendar day.")]
		public int CurrentDay = 1;

		[Tooltip("UTC timestamp of session start.")]
		public string SessionStartTime = string.Empty;

		[Tooltip("UTC timestamp of the last commit.")]
		public string SessionEndTime = string.Empty;

		[SerializeField] private string _version;

		[NonSerialized] private PrefsStore    _store;
		[NonSerialized] private string        _storageKey;
		[NonSerialized] private bool          _loadedFromSave;
		[NonSerialized] private ResetListener _resetListener;

		/// <summary>
		/// Override to set a unique key for this model.
		/// Defaults to "UGFW_GAME_MODEL" for backward compatibility.
		/// </summary>
		protected virtual string SaveKey => "UGFW_GAME_MODEL";

		/// <summary>
		/// Override to define the current save version for migration.
		/// </summary>
		protected virtual int CurrentSaveVersion => 1;

		/// <summary>
		/// Override to migrate a loaded save whose <see cref="SaveVersion"/>, still the old one
		/// here, is behind <see cref="CurrentSaveVersion"/>. Runs during <see cref="Load()"/>, only
		/// for existing saves; the migrated state is written back.
		/// </summary>
		protected virtual void OnMigrate() { }

		/// <summary>
		/// Override to initialize state after loading. Called by Initialize() on fresh and existing saves.
		/// </summary>
		/// <param name="isFirstLaunch">True if no save data existed (fresh install).</param>
		public virtual void OnInitialized(bool isFirstLaunch) { }

		/// <summary>
		/// Override to reset state that isn't serialized, such as caches built from the saved
		/// fields. Called after the store deleted all its data and the serialized fields went
		/// back to their fresh values.
		/// </summary>
		protected virtual void OnReset() { }

		/// <summary>The key this instance saves under: <see cref="SaveKey"/>, scoped when loaded for a scope.</summary>
		public string StorageKey => _storageKey ?? SaveKey;

		/// <summary>True when this instance was read from a save, rather than started fresh.</summary>
		public bool IsLoadedFromSave => _loadedFromSave;

		public DateTime SessionStartTimeDT => GetDateTimeFromString(SessionStartTime);
		public DateTime SessionEndTimeDT => GetDateTimeFromString(SessionEndTime);

		/// <summary>Loads the device's save from <see cref="UniPrefs.Store"/> (see <see cref="Load(PrefsStore, string)"/>).</summary>
		public static T Load() => Load(UniPrefs.Store);

		/// <summary>
		/// Loads the save under <see cref="SaveKey"/>, scoped to <paramref name="scope"/> when one
		/// is given (<c>{SaveKey}@{scope}</c>), or starts a fresh state when there is none, or
		/// none this build can read.
		/// </summary>
		/// <param name="store">The store to load from and save to.</param>
		/// <param name="scope">Whose save, such as an account id; null for the device's.</param>
		public static T Load(PrefsStore store, string scope = null)
		{
			if (store == null) throw new ArgumentNullException(nameof(store));

			var    fresh = new T();
			string key   = StorageKeys.Scoped(fresh.SaveKey, scope);

			T state = fresh;
			if (store.TryGet(key, out T saved))
			{
				if (saved.SaveVersion > fresh.CurrentSaveVersion)
				{
					store.Quarantine(key, $"its save version {saved.SaveVersion} is newer than this build's {fresh.CurrentSaveVersion}");
				}
				else
				{
					state = saved;
					state._loadedFromSave = true;
				}
			}

			state.Bind(store, key);

			if (!state._loadedFromSave)
			{
				state.SaveVersion = state.CurrentSaveVersion;
			}
			else if (state.SaveVersion < state.CurrentSaveVersion)
			{
				state.Migrate();
				state.Write();
			}

			return state;
		}

		/// <summary>Deletes the device's save from <see cref="UniPrefs.Store"/>.</summary>
		public static void DeleteSave() => DeleteSave(UniPrefs.Store);

		/// <summary>Deletes the save for <paramref name="scope"/>, or the device's when it is null.</summary>
		public static void DeleteSave(PrefsStore store, string scope = null)
		{
			if (store == null) throw new ArgumentNullException(nameof(store));
			store.Delete(StorageKeys.Scoped(new T().SaveKey, scope));
		}

		/// <summary>True when the device has a save in <see cref="UniPrefs.Store"/>.</summary>
		public static bool HasSave() => HasSave(UniPrefs.Store);

		/// <summary>True when <paramref name="scope"/>, or the device when it is null, has a save.</summary>
		public static bool HasSave(PrefsStore store, string scope = null)
		{
			if (store == null) throw new ArgumentNullException(nameof(store));
			return store.Has(StorageKeys.Scoped(new T().SaveKey, scope));
		}

		/// <summary>
		/// Gives the device's save to <paramref name="scope"/> when the scope has none of its own,
		/// so progress saved before per-scope saves existed carries over. The device is left with
		/// no save. True when a save moved.
		/// </summary>
		public static bool AdoptDeviceSave(PrefsStore store, string scope)
		{
			if (store == null) throw new ArgumentNullException(nameof(store));
			if (scope == null) throw new ArgumentNullException(nameof(scope));

			string deviceKey = new T().SaveKey;
			string scopedKey = StorageKeys.Scoped(deviceKey, scope);
			return !store.Has(scopedKey) && store.Move(deviceKey, scopedKey);
		}

		/// <summary>
		/// Persists the state, stamping <see cref="SessionEndTime"/>. An instance made with
		/// <c>new</c> rather than loaded saves as the device's state in <see cref="UniPrefs.Store"/>.
		/// </summary>
		public void Commit()
		{
			SessionEndTime = GetFormattedTime(DateTime.UtcNow);
			Write();
		}

		/// <summary>
		/// Starts a session: counts it (the first is 1), counts a new UTC day since the last
		/// commit, then calls <see cref="OnInitialized"/> and commits. Call once, after loading.
		/// </summary>
		/// <param name="isFirstLaunch">True when no save existed and no session has started yet.</param>
		public void Initialize(out bool isFirstLaunch)
		{
			DateTime now = DateTime.UtcNow;
			isFirstLaunch = !_loadedFromSave && string.IsNullOrEmpty(_version);

			if (TryGetSessionEndTime(out DateTime lastCommit) && now.Date != lastCommit.Date)
			{
				CurrentDay++;
			}

			SessionStartTime = GetFormattedTime(now);
			CurrentSession++;
			_version = Application.version;

			OnInitialized(isFirstLaunch);
			Commit();
		}

		public static string GetFormattedTime(DateTime dateTime) => PersistableState.GetFormattedTime(dateTime);

		/// <summary>The last commit's time, as UTC. False when there was none, or its text isn't a time.</summary>
		public bool TryGetSessionEndTime(out DateTime time) => PersistedTime.TryParse(SessionEndTime, out time);

		public static DateTime GetDateTimeFromString(string dt) => PersistableState.GetDateTimeFromString(dt);

		public virtual void OnBeforeSerialize() { }

		public virtual void OnAfterDeserialize() { }

		// The store deleted this state's save: back to a fresh state.
		private void ResetToFresh()
		{
			JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new T()), this);
			SaveVersion     = CurrentSaveVersion;
			_loadedFromSave = false;

			OnReset();
		}

		private void Migrate()
		{
			Debug.Log($"[PersistableState] Migrating {typeof(T).Name} from version {SaveVersion} to {CurrentSaveVersion}");
			OnMigrate();
			SaveVersion = CurrentSaveVersion;
		}

		private void Write()
		{
			// Made with new rather than loaded: it's the device's state, at the current version.
			if (_store == null)
			{
				SaveVersion = CurrentSaveVersion;
				Bind(UniPrefs.Store, SaveKey);
			}

			_store.Set(_storageKey, (T)this);
		}

		private void Bind(PrefsStore store, string key)
		{
			_store      = store;
			_storageKey = key;

			_resetListener ??= new ResetListener(this);
			store.AddResetListener(_resetListener);
		}

		// Registered with the store in place of the state, which keeps it alive, so the state's
		// own type doesn't carry the kernel interface into every game assembly.
		private sealed class ResetListener : IStoreResetListener
		{
			private readonly PersistableState<T> _state;

			public ResetListener(PersistableState<T> state) => _state = state;

			public void OnStoreReset() => _state.ResetToFresh();
		}
	}
}
