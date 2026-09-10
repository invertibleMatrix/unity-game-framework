using System;
using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// Container for all remote config variables with query methods and Firebase integration helpers.
	/// This is the main entry point for remote config operations.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteConfigMeta", menuName = "AK/MetaData/RemoteConfig/RemoteConfigMeta")]
	public class RemoteConfigMeta : MetaDataAsset, IMetaWithRegistry
	{
		[SerializeField] private RemoteVariablesRegistry _registry;

		/// <summary>
		/// The registry containing all remote variables.
		/// </summary>
		public RemoteVariablesRegistry Registry      => _registry;
		public UidRegistryAssetBase    RegistryAsset => _registry;

		public override void InitializeMeta() { }

		#region Query Methods

		/// <summary>
		/// Resolves the registry's live instance for an identity. Pass the identity of the
		/// asset you hold; the instance returned is the one remote config writes into, which
		/// matters when the held reference came from a different bundle.
		/// </summary>
		public RemoteVariableBase GetVariable(Uid id)
		{
			return _registry != null && _registry.TryResolve(id, out RemoteVariableBase variable) ? variable : null;
		}

		public RemoteVariableBase GetVariable(RemoteVariableBase asset) => asset != null ? GetVariable(asset.Id) : null;

		public RemoteVariable<T> GetVariable<T>(Uid id) => GetVariable(id) as RemoteVariable<T>;

		public RemoteVariable<T> GetVariable<T>(RemoteVariableBase asset) => GetVariable(asset) as RemoteVariable<T>;

		/// <summary>
		/// Gets a remote variable by its VariableKey.
		/// </summary>
		public RemoteVariableBase GetVariableByKey(string variableKey)
		{
			if (_registry == null || string.IsNullOrEmpty(variableKey))
				return null;

			var allVariables = _registry.Objects;
			foreach (var variable in allVariables)
			{
				if (variable != null && variable.VariableKey == variableKey)
					return variable;
			}

			return null;
		}

		/// <summary>
		/// Gets a typed remote variable by its VariableKey.
		/// </summary>
		public RemoteVariable<T> GetVariableByKey<T>(string variableKey)
		{
			return GetVariableByKey(variableKey) as RemoteVariable<T>;
		}

		/// <summary>
		/// Gets the value of a remote variable by its VariableKey.
		/// </summary>
		public T GetValue<T>(string variableKey)
		{
			var variable = GetVariableByKey(variableKey) as RemoteVariable<T>;
			return variable != null ? variable.Value : default;
		}

		/// <summary>
		/// Resolves the live <see cref="RemoteJson{T}"/> for the given asset. Pass the assigned
		/// asset (e.g. RemoteAdsConfig); the registry instance is the one RC writes into.
		/// </summary>
		public RemoteJson<T> GetJsonVariable<T>(RemoteVariableBase asset) where T : class, new()
		{
			return GetVariable(asset) as RemoteJson<T>;
		}

		public RemoteJson<T> GetJsonVariable<T>(Uid id) where T : class, new()
		{
			return GetVariable(id) as RemoteJson<T>;
		}

		/// <summary>Deserialized JSON value for the given asset, or default when unresolved.</summary>
		public T GetJsonValue<T>(RemoteVariableBase asset) where T : class, new()
		{
			RemoteJson<T> variable = GetJsonVariable<T>(asset);
			return variable != null ? variable.Value : default;
		}

		public T GetJsonValue<T>(Uid id) where T : class, new()
		{
			RemoteJson<T> variable = GetJsonVariable<T>(id);
			return variable != null ? variable.Value : default;
		}

		/// <summary>
		/// Gets all enabled remote variables.
		/// </summary>
		public List<RemoteVariableBase> GetEnabledVariables()
		{
			var result = new List<RemoteVariableBase>();
			if (_registry == null)
				return result;

			var allVariables = _registry.Objects;
			foreach (var variable in allVariables)
			{
				if (variable.IsEnabled)
					result.Add(variable);
			}

			return result;
		}

		/// <summary>
		/// Gets all remote variables (enabled and disabled).
		/// </summary>
		public IReadOnlyList<RemoteVariableBase> GetAllVariables()
		{
			if (_registry == null)
				return new List<RemoteVariableBase>();

			return _registry.Objects;
		}
		#endregion

		#region Firebase Integration Helpers

		/// <summary>
		/// Builds a dictionary of default values for Firebase Remote Config initialization.
		/// Only includes enabled variables.
		/// </summary>
		/// <returns>Dictionary mapping VariableKey to default value.</returns>
		public Dictionary<string, object> GetDefaultsForFirebase()
		{
			var defaults = new Dictionary<string, object>();

			if (_registry == null)
				return defaults;

			var enabledVariables = GetEnabledVariables();
			foreach (var variable in enabledVariables)
			{
				if (!string.IsNullOrEmpty(variable.VariableKey))
				{
					defaults[variable.VariableKey] = variable.GetDefaultValueObject();
				}
			}

			return defaults;
		}

		/// <summary>
		/// Gets all VariableKeys for enabled variables.
		/// Useful for registering keys with the remote config service.
		/// </summary>
		public List<string> GetEnabledVariableKeys()
		{
			var keys = new List<string>();
			if (_registry == null)
				return keys;

			var enabledVariables = GetEnabledVariables();
			foreach (var variable in enabledVariables)
			{
				if (!string.IsNullOrEmpty(variable.VariableKey))
				{
					keys.Add(variable.VariableKey);
				}
			}

			return keys;
		}

		/// <summary>
		/// Applies a fetched value to a remote variable.
		/// Called by the remote config service after fetching from server.
		/// </summary>
		public void ApplyRemoteValue(string variableKey, string value)
		{
			var variable = GetVariableByKey(variableKey);
			if (variable != null)
			{
				variable.SetRemoteValueFromString(value);
			}
			else
			{
				Debug.LogWarning($"RemoteConfigMeta: Variable with key '{variableKey}' not found.");
			}
		}

		/// <summary>
		/// Applies a fetched value directly to a remote variable.
		/// Called by the remote config service for typed values.
		/// </summary>
		public void ApplyRemoteValue<T>(string variableKey, T value)
		{
			var variable = GetVariableByKey(variableKey) as RemoteVariable<T>;
			if (variable != null)
			{
				variable.SetRemoteValue(value);
			}
			else
			{
				Debug.LogWarning($"RemoteConfigMeta: Variable with key '{variableKey}' not found or type mismatch.");
			}
		}

		/// <summary>
		/// Clears all remote values, resetting all variables to their defaults.
		/// </summary>
		public void ClearAllRemoteValues()
		{
			if (_registry == null)
				return;

			var allVariables = _registry.Objects;
			foreach (var variable in allVariables)
			{
				variable.ClearRemoteValue();
			}
		}

		/// <summary>
		/// Loads cached values for all variables that have caching enabled.
		/// Called during initialization to provide offline access.
		/// </summary>
		public void LoadAllCachedValues()
		{
			if (_registry == null)
				return;

			var allVariables = _registry.Objects;
			foreach (var variable in allVariables)
			{
				if (variable.CacheValue)
				{
					variable.LoadCachedValue();
				}
			}
		}

		/// <summary>
		/// Saves all current values to cache for variables with caching enabled.
		/// </summary>
		public void SaveAllCachedValues()
		{
			if (_registry == null)
				return;

			var allVariables = _registry.Objects;
			foreach (var variable in allVariables)
			{
				if (variable.CacheValue)
				{
					variable.SaveCachedValue();
				}
			}
		}

		/// <summary>
		/// Clears all cached values from PlayerPrefs.
		/// </summary>
		public void ClearAllCachedValues()
		{
			if (_registry == null)
				return;

			var allVariables = _registry.Objects;
			foreach (var variable in allVariables)
			{
				variable.ClearCachedValue();
			}
		}

		#endregion

		#region Editor Helpers

#if UNITY_EDITOR
		[ContextMenu("Refresh Registry")]
		public void RefreshRegistry()
		{
			if (_registry != null)
			{
				_registry.Editor_RefreshFromProject();
				UnityEditor.EditorUtility.SetDirty(this);
			}
		}

		[ContextMenu("Validate Variables")]
		public void ValidateVariables()
		{
			if (_registry == null)
			{
				Debug.LogError("RemoteConfigMeta: Registry is not assigned!");
				return;
			}

			var allVariables = _registry.Objects;
			var keySet = new HashSet<string>();
			int validCount = 0;
			int enabledCount = 0;

			foreach (var variable in allVariables)
			{
				// Check for missing VariableKey
				if (string.IsNullOrEmpty(variable.VariableKey))
				{
					Debug.LogWarning($"RemoteConfigMeta: Variable '{variable.name}' has no VariableKey set.");
					continue;
				}

				// Check for duplicate keys
				if (keySet.Contains(variable.VariableKey))
				{
					Debug.LogError($"RemoteConfigMeta: Duplicate VariableKey '{variable.VariableKey}' found!");
					continue;
				}

				keySet.Add(variable.VariableKey);
				validCount++;

				if (variable.IsEnabled)
					enabledCount++;
			}

			Debug.Log($"RemoteConfigMeta: Validation complete. {validCount} valid variables, {enabledCount} enabled.");
		}

		[ContextMenu("Print Defaults Dictionary")]
		public void PrintDefaultsDictionary()
		{
			var defaults = GetDefaultsForFirebase();
			foreach (var kvp in defaults)
			{
				Debug.Log($"  {kvp.Key}: {kvp.Value}");
			}

			Debug.Log($"Total: {defaults.Count} default values.");
		}
#endif

		#endregion
	}
}