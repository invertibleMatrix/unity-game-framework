using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using AK.Core;
using UnityEngine;

namespace AK.Utilities.Particles
{
	/// <summary>
	/// Spawns pooled particle effects. A config with an <see cref="ParticleConfigBase.InitialPoolSize"/>
	/// above zero has a pool of its own, filled to that size for the registry's configs and grown
	/// when it runs dry; any other config's effects are made for one show and destroyed when
	/// they stop. An effect counts toward its config's cap from the moment it is handed out
	/// until it is recycled. Main thread only.
	/// </summary>
	public class ParticleSpawner : GameEntity, IParticleSpawner
	{
		[SerializeField]
		private ParticlesRegistry _particlesRegistry;

		private readonly Dictionary<ParticleConfigBase, Stack<ParticleComponent>> _pools   = new();
		private readonly List<ParticleComponent>                                  _effects = new();

		private bool _initialized;

		private void Awake()
		{
			EnsureInitialized();
		}

		public T Spawn<T>(Uid<ParticleConfigBase> variant = default, Action onStop = null) where T : ParticleComponent
		{
			ParticleConfigBase config = _particlesRegistry != null ? _particlesRegistry.GetConfig<T>(variant) : null;
			if (config == null)
			{
				Debug.LogError($"Spawn<{typeof(T).Name}>() failed: no config for type with variant {UidDebugNames.Describe(variant)}.");
				return null;
			}

			return Spawn<T>(config, onStop);
		}

		public async UniTask<T> SpawnAsync<T>(Uid<ParticleConfigBase> variant = default, Action onStop = null) where T : ParticleComponent
		{
			ParticleConfigBase config = _particlesRegistry != null ? _particlesRegistry.GetConfig<T>(variant) : null;
			if (config == null)
			{
				Debug.LogError($"SpawnAsync<{typeof(T).Name}>() failed: no config for type with variant {UidDebugNames.Describe(variant)}.");
				return null;
			}

			if (!CanSpawn(config))
			{
				return null;
			}

			EnsureInitialized();

			ParticleComponent particleComponent = TakePooled(config);
			if (particleComponent == null)
			{
				particleComponent = await CreateNewParticleAsync(config.Prefab);
			}

			return HandOut(particleComponent, config, onStop) as T;
		}

		public ParticleComponent Spawn(ParticleConfigBase config, Action onStop = null)
		{
			if (config == null)
			{
				Debug.LogError("Spawn() failed: config is null.");
				return null;
			}

			if (!CanSpawn(config))
			{
				return null;
			}

			EnsureInitialized();

			ParticleComponent particleComponent = TakePooled(config);
			if (particleComponent == null)
			{
				particleComponent = CreateNewParticle(config.Prefab);
			}

			return HandOut(particleComponent, config, onStop);
		}

		public T Spawn<T>(ParticleConfigBase config, Action onStop = null) where T : ParticleComponent
		{
			var component = Spawn(config, onStop);
			if (component == null) return null;

			if (component is T typed) return typed;

			Debug.LogError($"Spawn<{typeof(T).Name}>() failed: '{config.name}' spawned a {component.GetType().Name}.");
			component.Stop();
			return null;
		}

		public ParticleComponent Play(ParticleConfigBase config, Vector3 position,
		                              Quaternion? rotation = null, Color? color = null, Action onStop = null)
		{
			if (config == null)
			{
				Debug.LogError("Play() failed: config is null.");
				return null;
			}

			var component = Spawn(config, onStop);
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

		public void StopAll(ParticleConfigBase config = null)
		{
			// Back to front: an effect recycled at once leaves the list.
			for (int i = _effects.Count - 1; i >= 0; i--)
			{
				if (i >= _effects.Count) continue;

				ParticleComponent effect = _effects[i];
				if (config == null || ReferenceEquals(effect.Config, config))
				{
					effect.Stop();
				}
			}
		}

		/// <summary>Takes back a recycled effect: untracked, then pooled, or destroyed when its config keeps no pool.</summary>
		internal void Release(ParticleComponent effect, ParticleConfigBase config)
		{
			_effects.Remove(effect);

			if (config != null && config.InitialPoolSize > 0)
			{
				effect.gameObject.SetActive(false);
				PoolOf(config).Push(effect);
			}
			else
			{
				Destroy(effect.gameObject);
			}
		}

		/// <summary>Stops tracking an effect destroyed outside the spawner.</summary>
		internal void Forget(ParticleComponent effect)
		{
			_effects.Remove(effect);
		}

		/// <summary>The config has a prefab, and room under its cap: extra spawns are refused (drop-newest).</summary>
		private bool CanSpawn(ParticleConfigBase config)
		{
			if (config.Prefab == null)
			{
				Debug.LogError($"Spawning '{config.name}' failed: the config has no prefab.", config);
				return false;
			}

			if (config.MaxActiveInstances <= 0)
			{
				return true;
			}

			int active = 0;
			for (int i = 0; i < _effects.Count; i++)
			{
				if (ReferenceEquals(_effects[i].Config, config))
				{
					active++;
				}
			}

			return active < config.MaxActiveInstances;
		}

		private ParticleComponent TakePooled(ParticleConfigBase config)
		{
			if (config.InitialPoolSize <= 0 || !_pools.TryGetValue(config, out Stack<ParticleComponent> pool))
			{
				return null;
			}

			// Skips effects destroyed while pooled.
			while (pool.Count > 0)
			{
				ParticleComponent effect = pool.Pop();
				if (effect != null) return effect;
			}

			return null;
		}

		private ParticleComponent HandOut(ParticleComponent effect, ParticleConfigBase config, Action onStop)
		{
			_effects.Add(effect);
			effect.Init(config, onStop);
			return effect;
		}

		private Stack<ParticleComponent> PoolOf(ParticleConfigBase config)
		{
			if (!_pools.TryGetValue(config, out Stack<ParticleComponent> pool))
			{
				pool = new Stack<ParticleComponent>();
				_pools.Add(config, pool);
			}

			return pool;
		}

		private void EnsureInitialized()
		{
			if (_initialized) return;
			_initialized = true;

			if (_particlesRegistry == null) return;

			IReadOnlyList<ParticleConfigBase> configs = _particlesRegistry.ParticleConfigs;
			for (int i = 0; i < configs.Count; i++)
			{
				ParticleConfigBase config = configs[i];
				if (config == null || config.Prefab == null || config.InitialPoolSize <= 0) continue;

				Stack<ParticleComponent> pool = PoolOf(config);
				while (pool.Count < config.InitialPoolSize)
				{
					pool.Push(CreateNewParticle(config.Prefab));
				}
			}
		}

		private ParticleComponent CreateNewParticle(ParticleComponent prefab)
		{
			var go = Instantiate(prefab, transform);
			go.name = prefab.name;
			go.gameObject.SetActive(false);
			go.Adopt(this);
			return go;
		}

		private async UniTask<ParticleComponent> CreateNewParticleAsync(ParticleComponent prefab)
		{
			var go = await InstantiateAsync(prefab, transform).ToUniTask();
			go[0].name = prefab.name;
			go[0].gameObject.SetActive(false);
			go[0].Adopt(this);
			return go[0];
		}

#if UNITY_EDITOR
		[ContextMenu("Log Pool Statistics")]
		private void LogPoolStatistics()
		{
			int total = 0;
			foreach (var kvp in _pools) total += kvp.Value.Count;

			Debug.Log("=== Particle Pool Statistics ===");
			Debug.Log($"Pools: {_pools.Count} | Pooled instances available: {total} | Active effects: {_effects.Count}");

			foreach (var kvp in _pools)
			{
				Debug.Log($"Pool '{(kvp.Key != null ? kvp.Key.name : "(destroyed config)")}': {kvp.Value.Count} available");
			}
		}
#endif
	}
}
