using System;
using System.Globalization;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// Static utility methods for persistable state (time formatting, etc.).
	/// Accessible without the generic type parameter.
	/// </summary>
	public static class PersistableState
	{
		public static string GetFormattedTime(DateTime dateTime)
		{
			return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
		}

		public static DateTime GetDateTimeFromString(string dt)
		{
			if (DateTime.TryParse(dt, null, DateTimeStyles.RoundtripKind, out DateTime time))
			{
				return time;
			}

			return DateTime.Now;
		}
	}

	/// <summary>
	/// Generic base class for persistable game state. Provides save/load via PrefsProperty,
	/// session tracking, and save migration. Games extend this with their own fields.
	///
	/// Override <see cref="SaveKey"/> to set a unique prefs key per game model.
	/// Defaults to "UGFW_GAME_MODEL" for backward compatibility.
	///
	/// Usage:
	/// <code>
	/// [Serializable]
	/// public class MyGameModel : PersistableState<MyGameModel>
	/// {
	///     protected override string SaveKey => "MY_GAME_SAVE";
	///     public int TotalStars;
	///     // ... game-specific fields
	/// }
	/// </code>
	/// </summary>
	[Serializable]
	public abstract class PersistableState<T> : ISerializationCallbackReceiver where T : PersistableState<T>, new()
	{
		[Tooltip("Save format version for migration.")]
		public int SaveVersion = 1;

		[Tooltip("Incremented each time the game starts.")]
		public int CurrentSession = 1;

		[Tooltip("Incremented each new calendar day.")]
		public int CurrentDay = 1;

		[Tooltip("UTC timestamp of session start.")]
		public string SessionStartTime = string.Empty;

		[Tooltip("UTC timestamp of last session end.")]
		public string SessionEndTime = string.Empty;

		[SerializeField] private string _version;

		/// <summary>
		/// Override to set a unique PlayerPrefs key for this model.
		/// Defaults to "UGFW_GAME_MODEL" for backward compatibility.
		/// </summary>
		protected virtual string SaveKey => "UGFW_GAME_MODEL";

		/// <summary>
		/// Override to define the current save version for migration.
		/// </summary>
		protected virtual int CurrentSaveVersion => 1;

		/// <summary>
		/// Override to perform save migration when SaveVersion is behind CurrentSaveVersion.
		/// </summary>
		protected virtual void OnMigrate() { }

		/// <summary>
		/// Override to initialize state after loading. Called by Initialize() on fresh and existing saves.
		/// </summary>
		/// <param name="isFirstLaunch">True if no save data existed (fresh install).</param>
		public virtual void OnInitialized(bool isFirstLaunch) { }

		public DateTime SessionStartTimeDT => GetDateTimeFromString(SessionStartTime);
		public DateTime SessionEndTimeDT => GetDateTimeFromString(SessionEndTime);

		private PrefsProperty<T> _prefs;

		/// <summary>
		/// Lazily-initialized PrefsProperty using the overridden SaveKey.
		/// </summary>
		private PrefsProperty<T> Prefs => _prefs ??= new PrefsProperty<T>(SaveKey);

		/// <summary>
		/// Loads the state from prefs. Returns a fresh instance if no save exists.
		/// </summary>
		public static T Load()
		{
			// Read directly via UniPrefs - allocating a PrefsProperty here would leak it
			// (it subscribes to the static UniPrefs.OnReset event).
			var instance = new T();
			return UniPrefs.Get(instance.SaveKey, instance) ?? instance;
		}

		/// <summary>
		/// Deletes all saved data for this model.
		/// </summary>
		public static void DeleteSave()
		{
			var instance = new T();
			UniPrefs.Delete(instance.SaveKey);
		}

		/// <summary>
		/// Checks if any save data exists for this model.
		/// </summary>
		public static bool HasSave()
		{
			var instance = new T();
			return UniPrefs.HasKey(instance.SaveKey);
		}

		/// <summary>
		/// Persists the state to prefs.
		/// </summary>
		public void Commit()
		{
			SessionEndTime = GetFormattedTime(DateTime.UtcNow);
			Prefs.Save((T)this);
		}

		/// <summary>
		/// Initializes session tracking and save migration. Call this once after Load().
		/// </summary>
		public void Initialize(out bool isFirstLaunch)
		{
			DateTime now = DateTime.UtcNow;
			SessionStartTime = now.ToString("O", CultureInfo.InvariantCulture);
			CurrentSession++;

			isFirstLaunch = string.IsNullOrEmpty(_version);
			_version = Application.version;

			if (TryGetSessionEndTime(out DateTime lastSessionTime))
			{
				if (now.Date != lastSessionTime.Date)
				{
					CurrentDay++;
				}
			}

			Migrate();
			OnInitialized(isFirstLaunch);
			Commit();
		}

		private void Migrate()
		{
			if (SaveVersion < CurrentSaveVersion)
			{
				Debug.Log($"[PersistableState] Migrating {typeof(T).Name} from version {SaveVersion} to {CurrentSaveVersion}");
				OnMigrate();
				SaveVersion = CurrentSaveVersion;
			}
		}

		public static string GetFormattedTime(DateTime dateTime) => PersistableState.GetFormattedTime(dateTime);

		public bool TryGetSessionEndTime(out DateTime time)
		{
			time = DateTime.Now;
			if (DateTime.TryParse(SessionEndTime, null, DateTimeStyles.RoundtripKind, out DateTime t))
			{
				time = t;
				return true;
			}

			return false;
		}

		public static DateTime GetDateTimeFromString(string dt) => PersistableState.GetDateTimeFromString(dt);

		public virtual void OnBeforeSerialize() { }

		public virtual void OnAfterDeserialize() { }
	}
}
