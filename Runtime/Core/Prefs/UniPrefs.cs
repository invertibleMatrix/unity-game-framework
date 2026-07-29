using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AK.Core.Extensions;

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
	/// Provides methods for storing and retrieving preferences using Unity's <see cref="PlayerPrefs"/>.
	/// </summary>
	public static class UniPrefs
	{
		/// <summary>
		/// <see cref="OnReset"/> is going to dispatch when <see cref="DeleteAll"/> is invoked...
		/// </summary>
		public static event Action OnReset = default;

		/// <summary>
		/// Stores a value in <see cref="PlayerPrefs"/> as a JSON string.
		/// </summary>
		/// <typeparam name="T">The type of the data to store.</typeparam>
		/// <param name="key">The key under which the value is stored.</param>
		/// <param name="data">The value to store.</param>
		public static void Set<T>(string key, T data)
		{
			ValidateKey(key);

			var json = JsonUtility.ToJson(new DataWrapper<T>(data));
			PlayerPrefs.SetString(key, json);
			PlayerPrefs.Save();
		}

		/// <summary>
		/// Retrieves a value from <see cref="PlayerPrefs"/> and deserializes it from JSON.
		/// </summary>
		/// <typeparam name="TReturn">The type of the data to retrieve.</typeparam>
		/// <param name="key">The key under which the value is stored.</param>
		/// <param name="default">The default value to return if the key does not exist or the value is null.</param>
		/// <returns>The retrieved value or the default value if the key does not exist or the value is null.</returns>
		public static TReturn Get<TReturn>(string key, TReturn @default = default)
		{
			ValidateKey(key);

			var json = PlayerPrefs.GetString(key);
			if (string.IsNullOrEmpty(json)) return @default;

			try
			{
				var dataWrapper = JsonUtility.FromJson<DataWrapper<TReturn>>(json);
				return dataWrapper.Data ?? @default;
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[UniPrefs] Failed to deserialize key '{key}': {e.Message}. Returning default.");
				return @default;
			}
		}

		/// <summary>
		/// Checks if the specified key exists in PlayerPrefs.
		/// </summary>
		/// <param name="key">The key to check for existence in PlayerPrefs.</param>
		/// <returns>True if the key exists; otherwise, false.</returns>
		public static bool HasKey(string key)
		{
			ValidateKey(key);
			return PlayerPrefs.HasKey(key);
		}

		/// <summary>
		/// Deletes the specified key from PlayerPrefs.
		/// </summary>
		/// <param name="key">The key to delete from PlayerPrefs.</param>
		public static void Delete(string key)
		{
			ValidateKey(key);
			PlayerPrefs.DeleteKey(key);
			PlayerPrefs.Save();
		}

		public static void DeleteAll()
		{
			PlayerPrefs.DeleteAll();
			PlayerPrefs.Save();

			OnReset.SafeInvoke();
		}

		/// <summary>
		/// Validates that the specified key is usable.
		/// </summary>
		/// <param name="key">The key to validate.</param>
		/// <exception cref="Exception">Thrown if the key is null or empty.</exception>
		private static void ValidateKey(string key)
		{
			if (string.IsNullOrEmpty(key)) throw new Exception("Cannot use a null or blank key");
		}
	}
}