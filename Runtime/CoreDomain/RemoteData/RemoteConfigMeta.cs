using System;
using System.Collections.Generic;
using AK.Core;
using AK.Kernel.Persistence;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// All remote config variables, looked up by identity or by key, and the one place values
	/// reach them: a remote config service hands this meta what it fetched, and the meta applies
	/// it and keeps the cache.
	///
	/// <para><b>Fetches are whole.</b> <see cref="ApplyFetchedValues"/> takes everything the
	/// provider holds. A variable whose key isn't there, or whose value can't be read, loses its
	/// remote value and reads as its default.</para>
	///
	/// <para><b>Cache.</b> The fetched text of every variable that caches is kept in a
	/// <see cref="PrefsStore"/> under <see cref="CacheKey"/>, replaced after each fetch.
	/// <see cref="LoadCachedValues"/> brings it back at the next start, for when the provider
	/// can't be reached.</para>
	///
	/// <para>Main thread only.</para>
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteConfigMeta", menuName = "AK/MetaData/RemoteConfig/RemoteConfigMeta")]
	public class RemoteConfigMeta : MetaDataAsset, IMetaWithRegistry
	{
		/// <summary>The key the cached values are kept under.</summary>
		public const string CacheKey = "UGFW_REMOTE_CONFIG";

		// Earlier versions cached each value on its own, typed, under this prefix and the variable's key.
		private const string LegacyCachePrefix = "remote_config_";

		private const string LogTag       = "[RemoteConfig]";
		private const int    ExcerptLength = 80;

		[SerializeField] private RemoteVariablesRegistry _registry;

		[NonSerialized] private Dictionary<string, RemoteVariableBase> _byKey;
		[NonSerialized] private RemoteVariablesRegistry                _indexedRegistry;
		[NonSerialized] private int                                    _indexedVersion;

		/// <summary>The registry containing all remote variables.</summary>
		public RemoteVariablesRegistry Registry      => _registry;
		public UidRegistryAssetBase    RegistryAsset => _registry;

		public void InitializeMeta() { }

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

		/// <summary>The variable with <paramref name="variableKey"/>, or null. One hash lookup.</summary>
		public RemoteVariableBase GetVariableByKey(string variableKey)
		{
			Dictionary<string, RemoteVariableBase> index = KeyIndex;
			return index != null && variableKey != null && index.TryGetValue(variableKey, out RemoteVariableBase variable) ? variable : null;
		}

		/// <summary>The variable with <paramref name="variableKey"/> when it holds a <typeparamref name="T"/>, or null.</summary>
		public RemoteVariable<T> GetVariableByKey<T>(string variableKey)
		{
			return GetVariableByKey(variableKey) as RemoteVariable<T>;
		}

		/// <summary>The value of the variable with <paramref name="variableKey"/>, or <c>default</c> when there is none.</summary>
		public T GetValue<T>(string variableKey)
		{
			RemoteVariable<T> variable = GetVariableByKey<T>(variableKey);
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

		/// <summary>Gets all enabled remote variables, in a new list.</summary>
		public List<RemoteVariableBase> GetEnabledVariables()
		{
			var result = new List<RemoteVariableBase>();
			if (_registry == null)
			{
				return result;
			}

			foreach (RemoteVariableBase variable in _registry.Objects)
			{
				if (variable != null && variable.IsEnabled)
				{
					result.Add(variable);
				}
			}

			return result;
		}

		/// <summary>Gets all remote variables (enabled and disabled).</summary>
		public IReadOnlyList<RemoteVariableBase> GetAllVariables()
		{
			return _registry != null ? _registry.Objects : Array.Empty<RemoteVariableBase>();
		}

		#endregion

		#region Provider Integration

		/// <summary>
		/// Each enabled variable's default as text, by key, for a provider's in-app defaults.
		/// Text, never objects: Firebase, for one, stores an object as its type's name.
		/// </summary>
		public Dictionary<string, string> GetDefaultValueTexts()
		{
			var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
			if (_registry == null)
			{
				return defaults;
			}

			foreach (RemoteVariableBase variable in _registry.Objects)
			{
				if (IsLive(variable) && !defaults.ContainsKey(variable.VariableKey))
				{
					defaults.Add(variable.VariableKey, variable.GetDefaultValueText());
				}
			}

			return defaults;
		}

		/// <summary>The keys of the enabled variables, each once, in registry order.</summary>
		public List<string> GetEnabledVariableKeys()
		{
			var keys = new List<string>();
			if (_registry == null)
			{
				return keys;
			}

			var seen = new HashSet<string>(StringComparer.Ordinal);
			foreach (RemoteVariableBase variable in _registry.Objects)
			{
				if (IsLive(variable) && seen.Add(variable.VariableKey))
				{
					keys.Add(variable.VariableKey);
				}
			}

			return keys;
		}

		/// <summary>
		/// Brings back the values cached by the last fetch, for the variables that cache and have
		/// no value fetched this session yet. A cached value that can no longer be read, because
		/// the variable's type changed, is dropped with a warning. Returns how many were loaded.
		/// The per-variable cache of earlier versions is deleted, not read.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="cache"/> is null.</exception>
		public int LoadCachedValues(PrefsStore cache)
		{
			if (cache == null) throw new ArgumentNullException(nameof(cache));
			if (_registry == null) return 0;

			DeleteLegacyCache(cache);

			if (!cache.TryGet(CacheKey, out CachedValues saved) || saved.Values == null)
			{
				return 0;
			}

			int loaded = 0;
			foreach (CachedValue entry in saved.Values)
			{
				RemoteVariableBase variable = GetVariableByKey(entry.Key);
				if (variable == null || !variable.IsEnabled || !variable.CacheValue || variable.Origin == RemoteValueOrigin.Fetched)
				{
					continue;
				}

				if (variable.TrySetRemoteText(entry.Text, RemoteValueOrigin.Cached))
				{
					loaded++;
				}
				else
				{
					Debug.LogWarning($"{LogTag} The cached value of '{entry.Key}' can't be read as {variable.ValueType.Name} and was dropped: '{Excerpt(entry.Text)}'.", variable);
				}
			}

			return loaded;
		}

		/// <summary>
		/// Applies a fetch: <paramref name="values"/> is everything the provider holds, by key.
		/// Each enabled variable takes its value; one without a value there, or with one that
		/// can't be read, loses its remote value and reads as its default. A disabled variable
		/// always does. The cache is then replaced with the fetched values of the variables that
		/// cache.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="values"/> or <paramref name="cache"/> is null.</exception>
		public RemoteConfigUpdate ApplyFetchedValues(IReadOnlyDictionary<string, string> values, PrefsStore cache)
		{
			if (values == null) throw new ArgumentNullException(nameof(values));
			if (cache == null) throw new ArgumentNullException(nameof(cache));
			if (_registry == null) return default;

			int applied = 0, rejected = 0, cleared = 0;

			foreach (RemoteVariableBase variable in _registry.Objects)
			{
				if (variable == null)
				{
					continue;
				}

				if (IsLive(variable) && values.TryGetValue(variable.VariableKey, out string text) && !string.IsNullOrEmpty(text))
				{
					if (variable.TrySetRemoteText(text, RemoteValueOrigin.Fetched))
					{
						applied++;
						continue;
					}

					rejected++;
					Debug.LogError($"{LogTag} The fetched value of '{variable.VariableKey}' can't be read as {variable.ValueType.Name}, so it reads as its default: '{Excerpt(text)}'.", variable);
				}

				if (variable.HasRemoteValue)
				{
					variable.ClearRemoteValue();
					cleared++;
				}
			}

			SaveCache(cache);
			return new RemoteConfigUpdate(applied, rejected, cleared);
		}

		/// <summary>Clears all remote values, so every variable reads as its default. The cache is kept.</summary>
		public void ClearAllRemoteValues()
		{
			if (_registry == null)
			{
				return;
			}

			foreach (RemoteVariableBase variable in _registry.Objects)
			{
				if (variable != null)
				{
					variable.ClearRemoteValue();
				}
			}
		}

		/// <summary>Deletes the cached values. The values the variables hold now are kept.</summary>
		/// <exception cref="ArgumentNullException"><paramref name="cache"/> is null.</exception>
		public void ClearCachedValues(PrefsStore cache)
		{
			if (cache == null) throw new ArgumentNullException(nameof(cache));

			cache.Delete(CacheKey);
			DeleteLegacyCache(cache);
		}

		#endregion

		#region Cache

		private void SaveCache(PrefsStore cache)
		{
			var saved = new CachedValues();
			foreach (RemoteVariableBase variable in _registry.Objects)
			{
				if (IsLive(variable) && variable.CacheValue && variable.HasRemoteValue)
				{
					saved.Values.Add(new CachedValue(variable.VariableKey, variable.RemoteText));
				}
			}

			if (saved.Values.Count > 0)
			{
				cache.Set(CacheKey, saved);
			}
			else
			{
				cache.Delete(CacheKey);
			}
		}

		private void DeleteLegacyCache(PrefsStore cache)
		{
			foreach (RemoteVariableBase variable in _registry.Objects)
			{
				if (variable == null || string.IsNullOrEmpty(variable.VariableKey))
				{
					continue;
				}

				string legacyKey = LegacyCachePrefix + variable.VariableKey;
				if (StorageKeys.IsValid(legacyKey))
				{
					cache.Delete(legacyKey);
				}
			}
		}

		/// <summary>The values the last fetch left, as saved: <c>{"Values":[{"Key":…,"Text":…}]}</c>.</summary>
		[Serializable]
		private sealed class CachedValues
		{
			public List<CachedValue> Values = new();
		}

		[Serializable]
		private struct CachedValue
		{
			public string Key;
			public string Text;

			public CachedValue(string key, string text)
			{
				Key  = key;
				Text = text;
			}
		}

		#endregion

		#region Lookup

		/// <summary>Variables by key, rebuilt when the registry changes. Null without a registry.</summary>
		private Dictionary<string, RemoteVariableBase> KeyIndex
		{
			get
			{
				if (_registry == null)
				{
					return null;
				}

				int version = _registry.Registry.Version;
				if (_byKey == null || !ReferenceEquals(_indexedRegistry, _registry) || _indexedVersion != version)
				{
					BuildKeyIndex(version);
				}

				return _byKey;
			}
		}

		private void BuildKeyIndex(int version)
		{
			IReadOnlyList<RemoteVariableBase> variables = _registry.Objects;
			var index = new Dictionary<string, RemoteVariableBase>(variables.Count, StringComparer.Ordinal);

			for (int i = 0; i < variables.Count; i++)
			{
				RemoteVariableBase variable = variables[i];
				if (variable == null || string.IsNullOrEmpty(variable.VariableKey))
				{
					continue;
				}

				// The first variable with a key wins, as in a linear search; ValidateVariables reports the others.
				if (!index.ContainsKey(variable.VariableKey))
				{
					index.Add(variable.VariableKey, variable);
				}
			}

			_byKey           = index;
			_indexedRegistry = _registry;
			_indexedVersion  = version;
		}

		/// <summary>Whether the variable takes remote values: it exists, is enabled and has a key.</summary>
		private static bool IsLive(RemoteVariableBase variable) =>
			variable != null && variable.IsEnabled && !string.IsNullOrEmpty(variable.VariableKey);

		private static string Excerpt(string text) =>
			text == null ? string.Empty : text.Length <= ExcerptLength ? text : text.Substring(0, ExcerptLength) + "…";

		#endregion

		#region Editor Helpers

#if UNITY_EDITOR
		private void OnValidate()
		{
			_byKey = null;
		}

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

			var keySet = new HashSet<string>();
			int validCount = 0;
			int enabledCount = 0;

			foreach (RemoteVariableBase variable in _registry.Objects)
			{
				if (variable == null)
				{
					continue;
				}

				if (string.IsNullOrEmpty(variable.VariableKey))
				{
					Debug.LogWarning($"RemoteConfigMeta: Variable '{variable.name}' has no VariableKey set.", variable);
					continue;
				}

				if (!keySet.Add(variable.VariableKey))
				{
					Debug.LogError($"RemoteConfigMeta: Duplicate VariableKey '{variable.VariableKey}' on '{variable.name}'; the first variable with it wins.", variable);
					continue;
				}

				validCount++;

				if (variable.IsEnabled)
				{
					enabledCount++;
				}
			}

			Debug.Log($"RemoteConfigMeta: Validation complete. {validCount} valid variables, {enabledCount} enabled.");
		}

		[ContextMenu("Print Default Values")]
		public void PrintDefaultValues()
		{
			Dictionary<string, string> defaults = GetDefaultValueTexts();
			foreach (KeyValuePair<string, string> pair in defaults)
			{
				Debug.Log($"  {pair.Key}: {pair.Value}");
			}

			Debug.Log($"Total: {defaults.Count} default values.");
		}
#endif

		#endregion
	}

	/// <summary>What one fetch did to the variables (<see cref="RemoteConfigMeta.ApplyFetchedValues"/>).</summary>
	public readonly struct RemoteConfigUpdate : IEquatable<RemoteConfigUpdate>
	{
		/// <summary>Variables that took a fetched value.</summary>
		public readonly int Applied;

		/// <summary>Fetched values that couldn't be read; those variables read as their defaults.</summary>
		public readonly int Rejected;

		/// <summary>Variables that lost a remote value and read as their defaults again.</summary>
		public readonly int Cleared;

		public RemoteConfigUpdate(int applied, int rejected, int cleared)
		{
			Applied  = applied;
			Rejected = rejected;
			Cleared  = cleared;
		}

		public bool Equals(RemoteConfigUpdate other) => Applied == other.Applied && Rejected == other.Rejected && Cleared == other.Cleared;

		public override bool Equals(object obj) => obj is RemoteConfigUpdate other && Equals(other);

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = Applied;
				hash = hash * 397 ^ Rejected;
				return hash * 397 ^ Cleared;
			}
		}

		public override string ToString() => $"{Applied} applied, {Rejected} rejected, {Cleared} cleared";
	}
}
