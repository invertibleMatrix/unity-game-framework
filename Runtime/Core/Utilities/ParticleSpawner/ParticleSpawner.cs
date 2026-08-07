using System;
using System.Collections.Generic;
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

			ParticleComponent particleComponent;
			if (config.InitialPoolSize == 0)
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

			particleComponent.Init(config, () =>
			{
				onStop?.Invoke();
				particleComponent.gameObject.SetActive(false);
				if (particleComponent.ConfigVariantId != null && _pools.TryGetValue(particleComponent.ConfigVariantId, out var queue))
				{
					queue.Enqueue(particleComponent);
				}
			});

			return particleComponent as T;
		}

		public async UniTask<T> SpawnAsync<T>(UID variantId = null, Action onStop = null) where T : ParticleComponent
		{
			ParticleConfigBase config = _particlesRegistry.GetConfig<T>(variantId);
			if (config == null)
			{
				return null; // Error is logged by the registry
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

			particleComponent.Init(config, () =>
			{
				onStop?.Invoke();
				particleComponent.gameObject.SetActive(false);
				if (config.InitialPoolSize == 0)
				{
					Destroy(particleComponent.gameObject);
				}
				else
				{
					if (particleComponent.ConfigVariantId != null && _pools.TryGetValue(particleComponent.ConfigVariantId, out var queue))
					{
						queue.Enqueue(particleComponent);
					}
				}
			});

			return particleComponent as T;
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
	}
}