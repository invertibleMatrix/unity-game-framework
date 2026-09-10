using System;
using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace Utilities.ParticleSpawner
{
	/// <summary>
	/// Catalog of all ParticleConfigBase assets. The base registry answers identity → config;
	/// this adds the per-component-type view used by type-safe spawning.
	/// </summary>
	[CreateAssetMenu(fileName = "ParticlesRegistry", menuName = "AK/Registries/ParticlesRegistry")]
	public class ParticlesRegistry : UidRegistryAsset<ParticleConfigBase>
	{
		private Dictionary<Type, Dictionary<Uid, ParticleConfigBase>> _byType;
		private Dictionary<Type, ParticleConfigBase>                  _defaultByType;
		private int                                                   _builtForVersion = -1;

		public IReadOnlyList<ParticleConfigBase> ParticleConfigs => _registry.Objects;

		/// <summary>Exact-type lookup. A None variant returns the first config registered for the type.</summary>
		public ParticleConfigBase GetConfigStrict(Type type, Uid<ParticleConfigBase> variant)
		{
			if (type == null)
			{
				Debug.LogError("[ParticlesRegistry] GetConfigStrict failed: type is null.");
				return null;
			}

			EnsureTypeCache();

			if (!_byType.TryGetValue(type, out Dictionary<Uid, ParticleConfigBase> configs))
			{
				return null;
			}

			if (variant.IsNone)
			{
				return _defaultByType.TryGetValue(type, out ParticleConfigBase fallback) ? fallback : null;
			}

			return configs.TryGetValue(variant.Value, out ParticleConfigBase config) ? config : null;
		}

		public ParticleConfigBase GetConfig<T>(Uid<ParticleConfigBase> variant = default) where T : ParticleComponent => GetConfigStrict(typeof(T), variant);

		private void EnsureTypeCache()
		{
			if (_byType != null && _builtForVersion == _registry.Version) return;

			_byType        = new Dictionary<Type, Dictionary<Uid, ParticleConfigBase>>();
			_defaultByType = new Dictionary<Type, ParticleConfigBase>();

			foreach (ParticleConfigBase config in _registry.Objects)
			{
				if (config == null || config.Prefab == null || !config.HasIdentity) continue;

				Type prefabType = config.Prefab.GetType();
				if (!_byType.TryGetValue(prefabType, out Dictionary<Uid, ParticleConfigBase> configs))
				{
					configs = new Dictionary<Uid, ParticleConfigBase>();
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
			Debug.Log($"[ParticlesRegistry] {_registry.Count} configs across {_byType.Count} component types.");
			foreach (KeyValuePair<Type, Dictionary<Uid, ParticleConfigBase>> kvp in _byType)
			{
				Debug.Log($"  {kvp.Key.Name}: {kvp.Value.Count} config(s)");
			}
		}
#endif
	}
}
