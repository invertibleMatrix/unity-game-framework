using System;
using UnityEngine;

namespace GameplayCore.Models
{
	/// <summary>
	/// Handles persistence for GameModel using a two-slot write strategy.
	/// Each save alternates between two slots. On load, the active slot is read.
	/// If the active slot is corrupt, the backup slot is used.
	/// </summary>
	public static class GameSaveService
	{
		private const string SLOT_KEY = "UGFW_SAVE_SLOT";
		private const string SAVE_KEY_PREFIX = "UGFW_SAVE_";
		private const string VERSION_KEY = "UGFW_SAVE_VERSION";

		private static int CurrentSlot
		{
			get => PlayerPrefs.GetInt(SLOT_KEY, 1);
			set => PlayerPrefs.SetInt(SLOT_KEY, value);
		}

		private static string GetSlotKey(int slot) => $"{SAVE_KEY_PREFIX}{slot}";

		/// <summary>
		/// Saves the GameModel to the next slot, then flips the active slot.
		/// If the write fails, the previous slot remains active — no data loss.
		/// </summary>
		public static void Save(GameModel model)
		{
			if (model == null) return;

			int nextSlot = CurrentSlot == 1 ? 2 : 1;
			string key = GetSlotKey(nextSlot);

			try
			{
				string json = JsonUtility.ToJson(model);
				PlayerPrefs.SetString(key, json);
				PlayerPrefs.SetInt(VERSION_KEY, GameModel.CURRENT_SAVE_VERSION);
				CurrentSlot = nextSlot;
				PlayerPrefs.Save();
			}
			catch (Exception e)
			{
				Debug.LogError($"[GameSaveService] Failed to save to slot {nextSlot}: {e.Message}");
				// Don't flip the slot — previous save remains valid
			}
		}

		/// <summary>
		/// Loads the GameModel from the active slot.
		/// If the active slot is corrupt or empty, falls back to the other slot.
		/// Returns null only if both slots are corrupt.
		/// </summary>
		public static GameModel Load()
		{
			// Try active slot first
			var model = TryLoadSlot(CurrentSlot);
			if (model != null) return model;

			// Active slot failed, try backup
			int backupSlot = CurrentSlot == 1 ? 2 : 1;
			model = TryLoadSlot(backupSlot);
			if (model != null)
			{
				Debug.LogWarning($"[GameSaveService] Active slot {CurrentSlot} was corrupt, recovered from slot {backupSlot}");
				CurrentSlot = backupSlot;
				return model;
			}

			// Both slots corrupt or empty — fresh start
			Debug.LogWarning("[GameSaveService] No valid save data found. Starting fresh.");
			return new GameModel();
		}

		/// <summary>
		/// Checks if any save data exists.
		/// </summary>
		public static bool HasSave()
		{
			return PlayerPrefs.HasKey(GetSlotKey(1)) || PlayerPrefs.HasKey(GetSlotKey(2));
		}

		/// <summary>
		/// Deletes all save data.
		/// </summary>
		public static void DeleteSave()
		{
			PlayerPrefs.DeleteKey(GetSlotKey(1));
			PlayerPrefs.DeleteKey(GetSlotKey(2));
			PlayerPrefs.DeleteKey(SLOT_KEY);
			PlayerPrefs.DeleteKey(VERSION_KEY);
			PlayerPrefs.Save();
		}

		private static GameModel TryLoadSlot(int slot)
		{
			string key = GetSlotKey(slot);
			if (!PlayerPrefs.HasKey(key)) return null;

			try
			{
				string json = PlayerPrefs.GetString(key);
				if (string.IsNullOrEmpty(json)) return null;

				var model = JsonUtility.FromJson<GameModel>(json);
				return model;
			}
			catch (Exception e)
			{
				Debug.LogError($"[GameSaveService] Slot {slot} is corrupt: {e.Message}");
				return null;
			}
		}
	}
}
