using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using UnityEngine;

namespace Utilities.ParticleSpawner
{
    /// <summary>
    /// Catalog of all ParticleConfigBase assets. Inherits the TypedUIDRegistryAsset
    /// machinery — self-initializing GUID lookups, auto-tracking of new/deleted configs,
    /// inspector maintenance buttons, and build-gate validation — and adds the
    /// particle-specific type+variant caches used by type-safe spawning.
    /// </summary>
    [CreateAssetMenu(fileName = "ParticlesRegistry", menuName = "AK/Registries/ParticlesRegistry")]
    public class ParticlesRegistry : TypedUIDRegistryAsset<ParticleConfigBase>
    {
        // Particle-specific caches (built on demand by ParticleSpawner at startup)
        private Dictionary<Type, Dictionary<string, ParticleConfigBase>> _typeToConfigs;
        private Dictionary<string, ParticleConfigBase>                    _uidToConfig;

        public IReadOnlyList<ParticleConfigBase> ParticleConfigs => _registry.Objects;

        public void BuildCache()
        {
            _registry.Initialize();

            _typeToConfigs = new Dictionary<Type, Dictionary<string, ParticleConfigBase>>();
            _uidToConfig   = new Dictionary<string, ParticleConfigBase>();

            foreach (ParticleConfigBase config in _registry.Objects)
            {
                if (config == null || config.Prefab == null)
                {
                    Debug.LogWarning($"Particle config '{config?.name}' has no prefab. Skipping cache build.");
                    continue;
                }

                string uid = config.UniqueID.Id;

                if (string.IsNullOrEmpty(uid) || uid == Guid.Empty.ToString())
                {
                    Debug.LogWarning($"Particle config '{config.name}' has invalid UID. Skipping cache build.");
                    continue;
                }

                // Build UID lookup
                if (_uidToConfig.ContainsKey(uid))
                {
                    Debug.LogError($"Duplicate UID '{uid}' found for config '{config.name}'. This will cause pooling issues!");
                }
                else
                {
                    _uidToConfig.Add(uid, config);
                }

                // Build type lookup
                Type prefabType = config.Prefab.GetType();

                if (!_typeToConfigs.ContainsKey(prefabType))
                {
                    _typeToConfigs.Add(prefabType, new Dictionary<string, ParticleConfigBase>());
                }

                if (_typeToConfigs[prefabType].ContainsKey(uid))
                {
                    Debug.LogWarning($"Duplicate config for type '{prefabType.Name}' with UID '{uid}'. Skipping duplicate.");
                }
                else
                {
                    _typeToConfigs[prefabType].Add(uid, config);
                }
            }

            Debug.Log($"ParticlesRegistry cache built: {_uidToConfig.Count} configs, {_typeToConfigs.Count} types.");
        }

        /// <summary>
        /// Strict type lookup: Only returns config if it matches the exact type.
        /// Returns null if no config found for the exact type (no fallback).
        /// Used by Spawn<T>() for type-safe spawning.
        /// </summary>
        public ParticleConfigBase GetConfigStrict(Type type, UID variantId)
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
            if (configs.TryGetValue(uid, out ParticleConfigBase config))
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
        /// Used for simple particle playback when you already have the UID reference.
        /// </summary>
        public ParticleConfigBase GetConfigByUID(UID variantId)
        {
            if (variantId == null || variantId.IsEmpty())
            {
                Debug.LogError("GetConfigByUID() failed: UID is null or empty.");
                return null;
            }

            string uid = variantId.Id;

            if (_uidToConfig.TryGetValue(uid, out ParticleConfigBase config))
            {
                return config;
            }

            return null;
        }

        /// <summary>
        /// Generic type-safe lookup: Returns config by generic type and optional variant UID.
        /// </summary>
        public ParticleConfigBase GetConfig<T>(UID variantId = null) where T : ParticleComponent
        {
            return GetConfigStrict(typeof(T), variantId);
        }

        // Editor helpers
#if UNITY_EDITOR
        [ContextMenu("Log Registry Statistics")]
        public void LogRegistryStatistics()
        {
            if (_typeToConfigs == null || _uidToConfig == null)
            {
                Debug.Log("Registry cache not built. Call BuildCache() first.");
                return;
            }

            Debug.Log($"=== Particles Registry Statistics ===");
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
                    ParticleConfigBase config = configKvp.Value;
                    Debug.Log($"  - UID: {uid}, Config: {config.name}");
                }
            }
        }
#endif
    }
}
