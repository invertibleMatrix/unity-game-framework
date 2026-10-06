using System;
using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace AK.Utilities.Audio
{
	/// <summary>
	/// Catalog of all AudioConfig assets. The base registry answers identity → config; this
	/// adds the per-component-type view used by type-safe spawning, so Spawn&lt;T&gt;() can
	/// ask "the config for T with this variant" or "the default config for T".
	/// </summary>
	[CreateAssetMenu(fileName = "AudioRegistry", menuName = "AK/Registries/Audio Registry")]
	public class AudioRegistry : UidRegistryAsset<AudioConfig>
	{
		private Dictionary<Type, Dictionary<Uid, AudioConfig>> _byType;
		private Dictionary<Type, AudioConfig>                  _defaultByType;
		private int                                            _builtForVersion = -1;

		public IReadOnlyList<AudioConfig> AudioConfigs => _registry.Objects;

		/// <summary>Exact-type lookup. A None variant returns the first config registered for the type.</summary>
		public AudioConfig GetConfigStrict(Type type, Uid<AudioConfig> variant)
		{
			if (type == null)
			{
				Debug.LogError("[AudioRegistry] GetConfigStrict failed: type is null.");
				return null;
			}

			EnsureTypeCache();

			if (!_byType.TryGetValue(type, out Dictionary<Uid, AudioConfig> configs))
			{
				return null;
			}

			if (variant.IsNone)
			{
				return _defaultByType.TryGetValue(type, out AudioConfig fallback) ? fallback : null;
			}

			return configs.TryGetValue(variant.Value, out AudioConfig config) ? config : null;
		}

		public AudioConfig GetConfigStrict<T>(Uid<AudioConfig> variant) where T : AudioComponent => GetConfigStrict(typeof(T), variant);

		private void EnsureTypeCache()
		{
			if (_byType != null && _builtForVersion == _registry.Version) return;

			_byType        = new Dictionary<Type, Dictionary<Uid, AudioConfig>>();
			_defaultByType = new Dictionary<Type, AudioConfig>();

			foreach (AudioConfig config in _registry.Objects)
			{
				if (config == null || config.Prefab == null || !config.HasIdentity) continue;

				Type prefabType = config.Prefab.GetType();
				if (!_byType.TryGetValue(prefabType, out Dictionary<Uid, AudioConfig> configs))
				{
					configs = new Dictionary<Uid, AudioConfig>();
					_byType.Add(prefabType, configs);
					_defaultByType.Add(prefabType, config);
				}

				configs.TryAdd(config.Id, config);
			}

			_builtForVersion = _registry.Version;
		}

#if UNITY_EDITOR
		[ContextMenu("Log Registry Statistics")]
		private void LogRegistryStatistics()
		{
			EnsureTypeCache();
			Debug.Log($"[AudioRegistry] {_registry.Count} configs across {_byType.Count} component types.");
			foreach (KeyValuePair<Type, Dictionary<Uid, AudioConfig>> kvp in _byType)
			{
				Debug.Log($"  {kvp.Key.Name}: {kvp.Value.Count} config(s)");
			}
		}
#endif
	}
}
