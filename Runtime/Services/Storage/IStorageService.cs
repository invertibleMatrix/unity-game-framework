using System;
using System.Threading.Tasks;

namespace AK.Services
{
	/// <summary>
	/// Main interface for the Storage Service.
	/// Provides a unified API for persistent data storage across different platforms.
	/// </summary>
	public interface IStorageService
	{
		/// <summary>
		/// Initializes the storage service.
		/// </summary>
		void Initialize();

		/// <summary>
		/// Saves a value to storage.
		/// </summary>
		/// <typeparam name="T">The type of value to save</typeparam>
		/// <param name="key">The key to save under</param>
		/// <param name="value">The value to save</param>
		void Save<T>(string key, T value);

		/// <summary>
		/// Loads a value from storage.
		/// </summary>
		/// <typeparam name="T">The type of value to load</typeparam>
		/// <param name="key">The key to load</param>
		/// <param name="defaultValue">The default value if key doesn't exist</param>
		/// <returns>The loaded value or default</returns>
		T Load<T>(string key, T defaultValue = default);

		/// <summary>
		/// Checks if a key exists in storage.
		/// </summary>
		/// <param name="key">The key to check</param>
		/// <returns>True if the key exists</returns>
		bool HasKey(string key);

		/// <summary>
		/// Deletes a key from storage.
		/// </summary>
		/// <param name="key">The key to delete</param>
		void DeleteKey(string key);

		/// <summary>
		/// Clears all data from storage.
		/// </summary>
		void ClearAll();

		/// <summary>
		/// Saves data to cloud storage (if available).
		/// </summary>
		/// <typeparam name="T">The type of value to save</typeparam>
		/// <param name="key">The key to save under</param>
		/// <param name="value">The value to save</param>
		/// <returns>True if save was successful</returns>
		Task<bool> SaveToCloud<T>(string key, T value);

		/// <summary>
		/// Loads data from cloud storage (if available).
		/// </summary>
		/// <typeparam name="T">The type of value to load</typeparam>
		/// <param name="key">The key to load</param>
		/// <returns>The loaded value or null</returns>
		Task<T> LoadFromCloud<T>(string key);

		/// <summary>
		/// Forces an immediate save of all pending data.
		/// </summary>
		void Flush();
	}
}
