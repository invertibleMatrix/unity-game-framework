using AK.Core;
using UnityEngine;

namespace GameplayCore.MetaData.RemoteConfig
{
	/// <summary>
	/// Abstract base class for remote config variables.
	/// Extends MetaDataAsset and implements IDefinition and IUIDObject for registry tracking.
	/// </summary>
	public abstract class RemoteVariableBase : MetaDataAsset
	{
		[Tooltip("The key used to identify this variable in the remote config server (e.g., Firebase).")]
		[SerializeField] protected string _variableKey;

		[Tooltip("Whether this variable should be registered with the remote config server.")]
		[SerializeField] protected bool _isEnabled = true;

		[Tooltip("If true, the fetched value will be cached to PlayerPrefs for offline access.")]
		[SerializeField] protected bool _cacheValue = true;
		
		/// <summary>
		/// 
		/// The unique identifier for this variable.
		/// </summary>
		public virtual UID UniqueID => this;

		/// <summary>
		/// The key used to identify this variable in the remote config server.
		/// </summary>
		public string VariableKey => _variableKey;

		/// <summary>
		/// Whether this variable is enabled for remote config registration.
		/// </summary>
		public bool IsEnabled => _isEnabled;

		/// <summary>
		/// Whether to cache the fetched value to PlayerPrefs.
		/// </summary>
		public bool CacheValue => _cacheValue;

		/// <summary>
		/// Whether a remote value has been fetched from the server.
		/// </summary>
		public abstract bool HasRemoteValue { get; }

		/// <summary>
		/// Clears the remote value, resetting to default.
		/// </summary>
		public abstract void ClearRemoteValue();

		/// <summary>
		/// Gets the default value as an object for building defaults dictionary.
		/// </summary>
		public abstract object GetDefaultValueObject();

		/// <summary>
		/// Gets the current value as an object.
		/// </summary>
		public abstract object GetValueObject();

		/// <summary>
		/// Sets the remote value from a string (JSON for complex types).
		/// </summary>
		public abstract void SetRemoteValueFromString(string value);

		/// <summary>
		/// Saves the current value to PlayerPrefs cache.
		/// </summary>
		public abstract void SaveCachedValue();

		/// <summary>
		/// Loads the cached value from PlayerPrefs.
		/// </summary>
		public abstract void LoadCachedValue();

		/// <summary>
		/// Clears the cached value from PlayerPrefs.
		/// </summary>
		public abstract void ClearCachedValue();

		/// <summary>
		/// Gets the cache key for this variable.
		/// </summary>
		protected string GetCacheKey() => $"remote_config_{_variableKey}";

		protected virtual void OnValidate()
		{
			if (string.IsNullOrEmpty(_variableKey))
			{
				Debug.LogWarning($"RemoteVariable '{name}' has no VariableKey set. It will not be registered with remote config.");
			}
		}
	}
}