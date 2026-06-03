using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Utilities.AudioSpawner
{
	public class AudioSpawner : GameEntity, IAudioSpawner
	{
		[InlineEditor] [SerializeField]
		private AudioRegistry _audioRegistry;

		// Pool by type - one pool per AudioComponent type, using max pool size across all configs
		private Dictionary<Type, Queue<AudioComponent>> _pools;
		private Dictionary<Type, int> _poolSizes;

		private void Awake()
		{
			_audioRegistry.BuildCache();
			InitializePools();
		}

		/// <summary>
		/// Type-safe spawn: Returns exact type T if config exists for T.
		/// Returns null if no config found for type T (strict type matching).
		/// Use this when you need to manipulate the component or have custom logic.
		/// </summary>
		public T Spawn<T>(UID variantId = null) where T : AudioComponent
		{
			Type requestedType = typeof(T);
			
			// Normalize empty/null UID
			if (variantId == null || variantId.IsEmpty())
			{
				variantId = UID.EmptyUID();
			}

			// Get config with strict type checking
			AudioConfig config = _audioRegistry.GetConfigStrict(requestedType, variantId);
			if (config == null)
			{
				Debug.LogError($"Spawn<{requestedType.Name}>() failed: No config found for type '{requestedType.Name}' with variant ID '{variantId.Id}'. " +
				              $"Use PlayAudio(uid) if you just want to play audio by UID.");
				return null;
			}

			// Verify the config's prefab type matches the requested type
			Type prefabType = config.Prefab.GetType();
			if (prefabType != requestedType)
			{
				Debug.LogError($"Spawn<{requestedType.Name}>() failed: Config '{config.name}' has prefab type '{prefabType.Name}', " +
				              $"but requested type is '{requestedType.Name}'. Type mismatch!");
				return null;
			}

			// Get from pool or create new
			AudioComponent audioComponent = GetFromPool(prefabType, config);
			if (audioComponent == null)
			{
				return null;
			}

			// Init and return
			audioComponent.Init(config, () => ReturnToPool(prefabType, audioComponent));
			return audioComponent as T;
		}

		/// <summary>
		/// Simple audio playback: Plays audio by UID regardless of type.
		/// Uses whatever AudioComponent type the config specifies.
		/// Use this for 99% of cases when you just want to play audio.
		/// </summary>
		public AudioComponent PlayAudio(UID variantId, Vector3? position = null)
		{
			if (variantId == null || variantId.IsEmpty())
			{
				Debug.LogError("PlayAudio() failed: UID is null or empty. Please provide a valid audio UID");
				return null;
			}

			// Get config by UID (forgiving - doesn't care about type)
			AudioConfig config = _audioRegistry.GetConfigByUID(variantId);
			
			if (config == null)
			{
				Debug.LogError($"PlayAudio() failed: No config found for UID '{variantId.Id}'. Check AudioRegistry.");
				return null;
			}

			// Get from pool or create new
			Type prefabType = config.Prefab.GetType();
			AudioComponent audioComponent = GetFromPool(prefabType, config);
			if (audioComponent == null)
			{
				return null;
			}

			// Init and play
			audioComponent.Init(config, () => ReturnToPool(prefabType, audioComponent));
			audioComponent.Play(position);
			return audioComponent;
		}

		/// <summary>
		/// Legacy method: Spawns and plays audio.
		/// OBSOLETE: Use PlayAudio(uid, position) for simple playback.
		/// </summary>
		[Obsolete("Use PlayAudio(uid, position) for simple playback. Use Spawn<T>() only when you need the component reference.")]
		public AudioComponent SpawnAndPlay<T>(UID variantId = null, Vector3? position = null) where T : AudioComponent
		{
			// For backward compatibility, use PlayAudio for base AudioComponent type
			if (typeof(T) == typeof(AudioComponent))
			{
				PlayAudio(variantId, position);
				return null;
			}

			// For specific types, use strict Spawn<T>()
			var audio = Spawn<T>(variantId);
			if (audio != null)
			{
				audio.Play(position);
			}
			return audio;
		}

		/// <summary>
		/// Legacy method: Spawns audio by type.
		/// OBSOLETE: Use Spawn<T>() for type-safe spawning.
		/// </summary>
		[Obsolete("Use Spawn<T>() for type-safe spawning.")]
		public AudioComponent Spawn(Type type, UID variantId)
		{
			if (type == null)
			{
				Debug.LogError("Spawn() failed: Type is null.");
				return null;
			}

			if (!typeof(AudioComponent).IsAssignableFrom(type))
			{
				Debug.LogError($"Spawn() failed: Type '{type.Name}' is not an AudioComponent.");
				return null;
			}

			// Normalize empty/null UID
			if (variantId == null || variantId.IsEmpty())
			{
				variantId = UID.EmptyUID();
			}

			// Get config with strict type checking
			AudioConfig config = _audioRegistry.GetConfigStrict(type, variantId);
			
			if (config == null)
			{
				Debug.LogError($"Spawn({type.Name}) failed: No config found for type '{type.Name}' with variant ID '{variantId.Id}'. " +
				              $"Use PlayAudio(uid) if you just want to play audio by UID.");
				return null;
			}

			// Verify the config's prefab type matches the requested type
			Type prefabType = config.Prefab.GetType();
			if (prefabType != type)
			{
				Debug.LogError($"Spawn({type.Name}) failed: Config '{config.name}' has prefab type '{prefabType.Name}', " +
				              $"but requested type is '{type.Name}'. Type mismatch!");
				return null;
			}

			// Get from pool or create new
			AudioComponent audioComponent = GetFromPool(prefabType, config);
			if (audioComponent == null)
			{
				return null;
			}

			// Init and return
			audioComponent.Init(config, () => ReturnToPool(prefabType, audioComponent));
			return audioComponent;
		}

		private AudioComponent GetFromPool(Type type, AudioConfig config)
		{
			if (!_pools.TryGetValue(type, out var pool))
			{
				Debug.LogError($"No pool found for audio type '{type.Name}'. This shouldn't happen if InitializePools ran correctly.");
				return null;
			}

			AudioComponent audioComponent = pool.Count > 0
				? pool.Dequeue()
				: CreateNewAudioComponent(config.Prefab, config.name);

			return audioComponent;
		}

		private void InitializePools()
		{
			_pools = new Dictionary<Type, Queue<AudioComponent>>();
			_poolSizes = new Dictionary<Type, int>();

			// Calculate max pool size per concrete type
			var poolSizes = new Dictionary<Type, int>();
			foreach (AudioConfig audioConfig in _audioRegistry.AudioConfigs)
			{
				if (audioConfig == null || audioConfig.Prefab == null)
				{
					Debug.LogWarning($"Audio config '{audioConfig?.name}' has no prefab. Skipping pool initialization.");
					continue;
				}

				Type key = audioConfig.Prefab.GetType();
				if (!poolSizes.ContainsKey(key))
				{
					poolSizes[key] = audioConfig.InitialPoolSize;
				}
				else
				{
					// Use the largest requested pool size for this type
					poolSizes[key] = Mathf.Max(poolSizes[key], audioConfig.InitialPoolSize);
				}
			}

			// Create pools with pre-warmed instances
			foreach (var kvp in poolSizes)
			{
				Type poolType = kvp.Key;
				int poolSize = kvp.Value;

				var pool = new Queue<AudioComponent>(poolSize);

				// Find any config with this prefab type to use for instantiation
				AudioConfig sampleConfig = null;
				foreach (var cfg in _audioRegistry.AudioConfigs)
				{
					if (cfg != null && cfg.Prefab != null && cfg.Prefab.GetType() == poolType)
					{
						sampleConfig = cfg;
						break;
					}
				}

				if (sampleConfig != null)
				{
					for (int i = 0; i < poolSize; i++)
					{
						var component = CreateNewAudioComponent(sampleConfig.Prefab, $"{sampleConfig.Prefab.name}_Pooled_{i}");
						pool.Enqueue(component);
					}

					_pools.Add(poolType, pool);
					_poolSizes.Add(poolType, poolSize);
					Debug.Log($"Initialized pool for type '{poolType.Name}' with {poolSize} instances.");
				}
			}

			Debug.Log($"AudioSpawner initialized {_pools.Count} pools with {_poolSizes.Values.Sum()} total instances.");
		}

		private AudioComponent CreateNewAudioComponent(AudioComponent prefab, [CanBeNull] string name = null)
		{
			var go = Instantiate(prefab, transform);
			go.name = name ?? prefab.name;
			go.gameObject.SetActive(false);
			return go;
		}

		private void ReturnToPool(Type type, AudioComponent component)
		{
			component.gameObject.SetActive(false);
			
			if (_pools.TryGetValue(type, out var pool))
			{
				pool.Enqueue(component);
			}
			else
			{
				Debug.LogWarning($"Tried to return component of type '{type.Name}' to pool, but pool doesn't exist. Destroying instead.");
				Destroy(component.gameObject);
			}
		}

		private void OnDestroy()
		{
			if (_pools == null) return;

			foreach (var kvp in _pools)
			{
				Type type = kvp.Key;
				var pool = kvp.Value;
				
				while (pool.Count > 0)
				{
					var component = pool.Dequeue();
					if (component != null) Destroy(component.gameObject);
				}
			}

			_pools.Clear();
			_poolSizes?.Clear();
		}

#if UNITY_EDITOR
		[Button("Log Pool Statistics")]
		private void LogPoolStatistics()
		{
			if (_pools == null)
			{
				Debug.Log("No pools initialized.");
				return;
			}

			Debug.Log($"=== Audio Pool Statistics ===");
			Debug.Log($"Total Pools: {_pools.Count}");
			Debug.Log($"Total Instances: {_poolSizes.Values.Sum()}");
			
			foreach (var kvp in _pools)
			{
				Type type = kvp.Key;
				var pool = kvp.Value;
				int poolSize = _poolSizes.TryGetValue(type, out int size) ? size : 0;
				
				Debug.Log($"Pool '{type.Name}': {pool.Count}/{poolSize} available instances");
			}
		}
#endif
	}
}