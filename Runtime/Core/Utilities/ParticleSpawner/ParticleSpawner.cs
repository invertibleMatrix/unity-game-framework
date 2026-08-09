using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using AK.Core;
using UnityEngine;

namespace Utilities.ParticleSpawner
{
	public class ParticleSpawner : GameEntity, IParticleSpawner
	{
		[SerializeField]
		private ParticlesRegistry _particlesRegistry;

		private Dictionary<UID, Queue<ParticleComponent>> _pools;

		// Active effect tracking — drives the concurrency cap and StopAll
		private readonly List<ParticleComponent> _activeComponents = new();

		private void Awake()
		{
			_particlesRegistry.BuildCache();
			InitializePools();
		}

		public T Spawn<T>(UID variantId = null, Action onStop = null) where T : ParticleComponent
		{
			ParticleConfigBase config = _particlesRegistry.GetConfig<T>(variantId);
			if (config == null)
			{
				return null; // Error is logged by the registry
			}

			return SpawnByConfig(config, onStop) as T;
		}

		public async UniTask<T> SpawnAsync<T>(UID variantId = null, Action onStop = null) where T : ParticleComponent
		{
			ParticleConfigBase config = _particlesRegistry.GetConfig<T>(variantId);
			if (config == null)
			{
				return null; // Error is logged by the registry
			}

			if (!PassesConcurrencyGate(config))
			{
				return null;
			}

			ParticleComponent particleComponent;
			if (config.InitialPoolSize == 0)
			{
				particleComponent = await CreateNewParticleAsync(config.Prefab);
			}
			else
			{
				if (config.VariantId == null || !_pools.TryGetValue(config.VariantId, out var pool))
				{
					Debug.LogError($"No pool found for particle with key '{config.VariantId}'.");
					return null;
				}

				particleComponent = pool.Count > 0 ? pool.Dequeue() : await CreateNewParticleAsync(config.Prefab);
			}

			return FinalizeSpawn(particleComponent, config, onStop) as T;
		}

		/// <summary>
		/// Config-driven one-call play: spawn and show in a single step, no generic,
		/// no UID extraction. The particle equivalent of PlayAudio(config).
		/// </summary>
		public ParticleComponent Play(ParticleConfigBase config, Vector3 position,
		                              Quaternion? rotation = null, Color? color = null, Action onStop = null)
		{
			if (config == null)
			{
				Debug.LogError("Play() failed: config is null.");
				return null;
			}

			var component = SpawnByConfig(config, onStop);
			if (component == null)
			{
				return null;
			}

			if (color.HasValue)
			{
				component.Show(position, rotation ?? Quaternion.identity, color.Value);
			}
			else if (rotation.HasValue)
			{
				component.Show(position, rotation.Value);
			}
			else
			{
				component.Show(position);
			}

			return component;
		}

		/// <summary>Stops all active effects, or only the given config's.</summary>
		public void StopAll(ParticleConfigBase config = null)
		{
			string uid = config != null && config.VariantId != null ? config.VariantId.Id : null;

			// Snapshot — Stop() mutates the active list via the recycle callback
			foreach (var component in _activeComponents.ToList())
			{
				if (component == null) continue;
				if (uid != null && (component.ConfigVariantId == null || component.ConfigVariantId.Id != uid)) continue;

				component.Stop();
			}
		}

		private ParticleComponent SpawnByConfig(ParticleConfigBase config, Action onStop)
		{
			if (config.Prefab == null)
			{
				Debug.LogError($"SpawnByConfig() failed: config '{config.name}' has no prefab.", config);
				return null;
			}

			if (!PassesConcurrencyGate(config))
			{
				return null;
			}

			bool pooled = config.InitialPoolSize > 0;

			ParticleComponent particleComponent;
			if (!pooled)
			{
				particleComponent = CreateNewParticle(config.Prefab);
			}
			else
			{
				if (config.VariantId == null || !_pools.TryGetValue(config.VariantId, out var pool))
				{
					Debug.LogError($"No pool found for particle with key '{config.VariantId}'.");
					return null;
				}

				particleComponent = pool.Count > 0 ? pool.Dequeue() : CreateNewParticle(config.Prefab);
			}

			return FinalizeSpawn(particleComponent, config, onStop);
		}

		private bool PassesConcurrencyGate(ParticleConfigBase config)
		{
			if (config.MaxActiveInstances <= 0)
			{
				return true;
			}

			string uid = config.VariantId.Id;
			int active = 0;
			foreach (var component in _activeComponents)
			{
				if (component != null && component.ConfigVariantId != null && component.ConfigVariantId.Id == uid)
				{
					active++;
				}
			}

			return active < config.MaxActiveInstances;
		}

		private ParticleComponent FinalizeSpawn(ParticleComponent particleComponent, ParticleConfigBase config, Action onStop)
		{
			bool pooled = config.InitialPoolSize > 0;
			ParticleComponent captured = particleComponent;

			_activeComponents.Add(captured);

			captured.Init(config, () =>
			{
				onStop?.Invoke();
				_activeComponents.Remove(captured);
				captured.gameObject.SetActive(false);

				if (pooled)
				{
					if (captured.ConfigVariantId != null && _pools.TryGetValue(captured.ConfigVariantId, out var queue))
					{
						queue.Enqueue(captured);
					}
				}
				else
				{
					// Non-pooled particles are one-shot instances — destroy on stop.
					Destroy(captured.gameObject);
				}
			});

			return particleComponent;
		}

		private void InitializePools()
		{
			_pools = new Dictionary<UID, Queue<ParticleComponent>>();

			if (_particlesRegistry == null || _particlesRegistry.ParticleConfigs == null) return;

			foreach (var config in _particlesRegistry.ParticleConfigs)
			{
				if (config == null || config.Prefab == null || config.VariantId == null || config.InitialPoolSize == 0) continue;

				if (!_pools.ContainsKey(config.VariantId))
				{
					_pools[config.VariantId] = new Queue<ParticleComponent>();
				}

				var pool = _pools[config.VariantId];
				for (int i = 0; i < config.InitialPoolSize; i++)
				{
					ParticleComponent ps = CreateNewParticle(config.Prefab);
					pool.Enqueue(ps);
				}
			}
		}

		private ParticleComponent CreateNewParticle(ParticleComponent prefab)
		{
			var go = Instantiate(prefab, transform);
			go.name = prefab.name;
			go.gameObject.SetActive(false);
			return go;
		}

		private async UniTask<ParticleComponent> CreateNewParticleAsync(ParticleComponent prefab)
		{
			var go = await InstantiateAsync(prefab, transform).ToUniTask();
			go[0].name = prefab.name;
			go[0].gameObject.SetActive(false);
			return go[0];
		}

		private void OnDestroy()
		{
			if (_pools == null) return;

			foreach (var kvp in _pools)
			{
				var pool = kvp.Value;
				while (pool.Count > 0)
				{
					var component = pool.Dequeue();
					if (component != null) Destroy(component.gameObject);
				}
			}

			_pools.Clear();
		}

#if UNITY_EDITOR
		[ContextMenu("Log Pool Statistics")]
		private void LogPoolStatistics()
		{
			if (_pools == null)
			{
				Debug.Log("No pools initialized.");
				return;
			}

			int total = 0;
			foreach (var kvp in _pools) total += kvp.Value.Count;

			Debug.Log("=== Particle Pool Statistics ===");
			Debug.Log($"Pools: {_pools.Count} | Pooled instances available: {total} | Active effects: {_activeComponents.Count}");

			foreach (var kvp in _pools)
			{
				Debug.Log($"Pool '{kvp.Key}': {kvp.Value.Count} available");
			}
		}
#endif
	}
}
