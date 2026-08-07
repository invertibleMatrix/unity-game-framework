using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Utilities.AudioSpawner
{
	[CreateAssetMenu(fileName = "AudioRegistry", menuName = "AK/Registries/Audio Registry")]
	public class AudioRegistry : ScriptableObject
	{
		[SerializeField] private TypedUIDRegistry<AudioConfig> _registry;

		// Cache for fast lookups
		private Dictionary<Type, Dictionary<string, AudioConfig>> _typeToConfigs;
		private Dictionary<string, AudioConfig>                   _uidToConfig;

		public IReadOnlyList<AudioConfig> AudioConfigs => _registry.Objects;

		public void BuildCache()
		{
			_registry.Initialize();

			_typeToConfigs = new Dictionary<Type, Dictionary<string, AudioConfig>>();
			_uidToConfig = new Dictionary<string, AudioConfig>();

			foreach (AudioConfig audioConfig in _registry.Objects)
			{
				if (audioConfig == null || audioConfig.Prefab == null)
				{
					Debug.LogWarning($"Audio config '{audioConfig?.name}' has no prefab. Skipping cache build.");
					continue;
				}

				string uid = audioConfig.UniqueID.Id;

				if (string.IsNullOrEmpty(uid) || uid == Guid.Empty.ToString())
				{
					Debug.LogWarning($"Audio config '{audioConfig.name}' has invalid UID. Skipping cache build.");
					continue;
				}

				// Build UID lookup
				if (_uidToConfig.ContainsKey(uid))
				{
					Debug.LogError($"Duplicate UID '{uid}' found for config '{audioConfig.name}'. This will cause pooling issues!");
				}
				else
				{
					_uidToConfig.Add(uid, audioConfig);
				}

				// Build type lookup
				Type prefabType = audioConfig.Prefab.GetType();

				if (!_typeToConfigs.ContainsKey(prefabType))
				{
					_typeToConfigs.Add(prefabType, new Dictionary<string, AudioConfig>());
				}

				if (_typeToConfigs[prefabType].ContainsKey(uid))
				{
					Debug.LogWarning($"Duplicate config for type '{prefabType.Name}' with UID '{uid}'. Skipping duplicate.");
				}
				else
				{
					_typeToConfigs[prefabType].Add(uid, audioConfig);
				}
			}

			Debug.Log($"AudioRegistry cache built: {_uidToConfig.Count} configs, {_typeToConfigs.Count} types.");
		}

		/// <summary>
		/// Strict type lookup: Only returns config if it matches the exact type.
		/// Returns null if no config found for the exact type (no fallback).
		/// Used by Spawn<T>() for type-safe spawning.
		/// </summary>
		public AudioConfig GetConfigStrict(Type type, UID variantId)
		{
			if (type == null)
			{
				Debug.LogError("GetConfigStrict() failed: Type is null.");
				return null;
			}

			if (variantId == null || variantId.IsEmpty())
			{
				variantId = UID.EmptyUID();
			}

			string uid = variantId.Id;

			// Check if we have configs for this type
			if (!_typeToConfigs.TryGetValue(type, out var configs))
			{
				return null;
			}

			// Try to find exact match for variant ID
			if (configs.TryGetValue(uid, out AudioConfig config))
			{
				return config;
			}

			// If variant is empty, return first config of this type (default variant)
			if (uid == Guid.Empty.ToString() && configs.Count > 0)
			{
				return configs.First().Value;
			}

			// No match found
			return null;
		}

		/// <summary>
		/// UID-based lookup: Returns config by UID regardless of type.
		/// Used by PlayAudio() for simple audio playback.
		/// </summary>
		public AudioConfig GetConfigByUID(UID variantId)
		{
			if (variantId == null || variantId.IsEmpty())
			{
				Debug.LogError("GetConfigByUID() failed: UID is null or empty.");
				return null;
			}

			string uid = variantId.Id;

			if (_uidToConfig.TryGetValue(uid, out AudioConfig config))
			{
				return config;
			}

			return null;
		}

		/// <summary>
		/// Legacy method: Get config by type and variant ID with fallback.
		/// OBSOLETE: Use GetConfigStrict() for type-safe lookup or GetConfigByUID() for UID-based lookup.
		/// </summary>
		[Obsolete("Use GetConfigStrict() for type-safe lookup or GetConfigByUID() for UID-based lookup.")]
		public AudioConfig GetConfig(Type type, UID variantId)
		{
			// For backward compatibility, try strict lookup first
			var config = GetConfigStrict(type, variantId);
			if (config != null)
			{
				return config;
			}

			// Fallback to UID-based lookup (for cross-assembly scenarios)
			if (variantId != null && !variantId.IsEmpty())
			{
				config = GetConfigByUID(variantId);
				if (config != null)
				{
					Debug.LogWarning($"GetConfig() fallback: Using UID-based lookup for type '{type.Name}'. " +
					                 $"Config '{config.name}' has prefab type '{config.Prefab.GetType().Name}'. " +
					                 $"Consider using PlayAudio(uid) instead.");
					return config;
				}
			}

			return null;
		}

		/// <summary>
		/// Legacy method: Get config by generic type.
		/// OBSOLETE: Use GetConfigStrict() for type-safe lookup.
		/// </summary>
		[Obsolete("Use GetConfigStrict() for type-safe lookup.")]
		public AudioConfig GetConfig<T>(UID variantId) where T : AudioComponent
		{
			return GetConfigStrict(typeof(T), variantId);
		}

		// Editor helpers
#if UNITY_EDITOR
		[ContextMenu("Refresh All Objects")]
		public void RefreshAllObjects()
		{
			_registry.RefreshAllObjects();
			EditorUtility.SetDirty(this);
		}

		[ContextMenu("Validate Objects")]
		public void ValidateObjects()
		{
			_registry.ValidateObjects();
		}

		[ContextMenu("Log Registry Statistics")]
		public void LogRegistryStatistics()
		{
			if (_typeToConfigs == null || _uidToConfig == null)
			{
				Debug.Log("Registry cache not built. Call BuildCache() first.");
				return;
			}

			Debug.Log($"=== Audio Registry Statistics ===");
			Debug.Log($"Total Configs: {_uidToConfig.Count}");
			Debug.Log($"Total Types: {_typeToConfigs.Count}");

			foreach (var kvp in _typeToConfigs)
			{
				Type type = kvp.Key;
				var configs = kvp.Value;
				Debug.Log($"Type '{type.Name}': {configs.Count} config(s)");

				foreach (var configKvp in configs)
				{
					string uid = configKvp.Key;
					AudioConfig config = configKvp.Value;
					Debug.Log($"  - UID: {uid}, Config: {config.name}");
				}
			}
		}
#endif
	}
}