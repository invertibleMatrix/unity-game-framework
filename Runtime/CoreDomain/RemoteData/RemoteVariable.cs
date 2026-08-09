using System;
using System.Globalization;
using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// Generic remote config variable with type-safe access, default values,
	/// remote value fallback, and optional PlayerPrefs caching via PrefsProperty.
	/// </summary>
	/// <typeparam name="T">The type of the variable value. Must be serializable.</typeparam>
	public abstract class RemoteVariable<T> : RemoteVariableBase
	{
		[Tooltip("The default value used when no remote value is available.")]
		[SerializeField] protected T _defaultValue;

		[NonSerialized] protected T    _remoteValue;
		[NonSerialized] protected bool _hasRemoteValue;

		// Lazy-initialized PrefsProperty for caching
		private PrefsProperty<T> _cachedValueProperty;

		/// <summary>
		/// The default value for this variable.
		/// </summary>
		public T DefaultValue => _defaultValue;

		public override UID UniqueID => this;

		/// <summary>
		/// The current value. Returns remote value if fetched, otherwise cached value, otherwise default.
		/// </summary>
		public T Value
		{
			get
			{
				// Priority: Remote > Cached > Default
				if (_hasRemoteValue)
					return _remoteValue;

				if (_cacheValue)
					return CachedProperty.Read();

				return _defaultValue;
			}
		}

		/// <summary>
		/// Whether a remote value has been fetched from the server.
		/// </summary>
		public override bool HasRemoteValue => _hasRemoteValue;

		/// <summary>
		/// Gets the PrefsProperty for caching, lazily initialized.
		/// </summary>
		private PrefsProperty<T> CachedProperty
		{
			get
			{
				if (_cachedValueProperty == null && !string.IsNullOrEmpty(_variableKey))
				{
					_cachedValueProperty = new PrefsProperty<T>(GetCacheKey(), _defaultValue);
				}
				return _cachedValueProperty;
			}
		}

		#region Abstract Methods Implementation

		public override object GetDefaultValueObject() => _defaultValue;
		public override object GetValueObject() => Value;

		public override void ClearRemoteValue()
		{
			_hasRemoteValue = false;
			_remoteValue = default;
		}

		public override void SetRemoteValueFromString(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				Debug.LogWarning($"RemoteVariable '{name}': Attempted to set null or empty string value.");
				return;
			}

			try
			{
				// Handle primitive types directly.
				// CultureInfo.InvariantCulture ensures that decimal separators (e.g. "1.5")
				// are parsed correctly regardless of the device's locale (e.g. German "1,5").
				if (typeof(T) == typeof(int))
				{
					_remoteValue = (T)(object)int.Parse(value, CultureInfo.InvariantCulture);
				}
				else if (typeof(T) == typeof(float))
				{
					_remoteValue = (T)(object)float.Parse(value, CultureInfo.InvariantCulture);
				}
				else if (typeof(T) == typeof(bool))
				{
					_remoteValue = (T)(object)bool.Parse(value);
				}
				else if (typeof(T) == typeof(string))
				{
					_remoteValue = (T)(object)value;
				}
				else if (typeof(T) == typeof(long))
				{
					_remoteValue = (T)(object)long.Parse(value, CultureInfo.InvariantCulture);
				}
				else if (typeof(T) == typeof(double))
				{
					_remoteValue = (T)(object)double.Parse(value, CultureInfo.InvariantCulture);
				}
				else
				{
					// Complex type - deserialize from JSON
					_remoteValue = JsonUtility.FromJson<T>(value);
				}

				_hasRemoteValue = true;

				// Cache the value if caching is enabled
				if (_cacheValue)
				{
					SaveCachedValue();
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"RemoteVariable '{name}': Failed to parse value '{value}' as type {typeof(T).Name}. Error: {e.Message}");
			}
		}

		#endregion

		#region Remote Value Setter

		/// <summary>
		/// Sets the remote value directly. Called by the remote config service after fetching.
		/// </summary>
		/// <param name="value">The value fetched from the remote config server.</param>
		public void SetRemoteValue(T value)
		{
			_remoteValue = value;
			_hasRemoteValue = true;

			if (_cacheValue)
			{
				SaveCachedValue();
			}
		}

		#endregion

		#region Caching

		public override void SaveCachedValue()
		{
			if (!_cacheValue || string.IsNullOrEmpty(_variableKey))
				return;

			T valueToSave = _hasRemoteValue ? _remoteValue : _defaultValue;
			CachedProperty.Save(valueToSave);
		}

		public override void LoadCachedValue()
		{
			if (!_cacheValue || string.IsNullOrEmpty(_variableKey))
				return;

			// Reading from PrefsProperty will load from PlayerPrefs
			_remoteValue = CachedProperty.Read();
			_hasRemoteValue = true;
		}

		public override void ClearCachedValue()
		{
			if (string.IsNullOrEmpty(_variableKey))
				return;

			CachedProperty?.Reset();
		}

		#endregion

		#region Implicit Operator

		/// <summary>
		/// Implicit conversion to the value type for convenient access.
		/// </summary>
		public static implicit operator T(RemoteVariable<T> variable)
		{
			return variable != null ? variable.Value : default;
		}

		#endregion
	}
}