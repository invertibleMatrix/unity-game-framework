using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AK.Core;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

namespace Utilities.AudioSpawner
{
	public class AudioSpawner : GameEntity, IAudioSpawner
	{
		[SerializeField]
		private AudioRegistry _audioRegistry;

		[Header("Music")]
		[SerializeField, Tooltip("Music survives scene loads (music sources live on a DontDestroyOnLoad root).")]
		private bool _persistMusicAcrossScenes = true;

		[SerializeField, Tooltip("Used for ducking: configs with DuckMusicDb > 0 lower the music channel while playing.")]
		private AudioMixerController _mixerController;

		// Pool by type - one pool per AudioComponent type, using max pool size across all configs
		private Dictionary<Type, Queue<AudioComponent>> _pools;
		private Dictionary<Type, int> _poolSizes;

		// Active voice tracking — drives concurrency caps, StopAll, IsPlaying
		private readonly List<AudioComponent>   _activeComponents = new();
		private readonly Dictionary<Uid, float> _lastPlayTimeByConfig = new();

		// Music lane: two plain sources A/B-crossfading on a persistent root
		private GameObject              _musicRoot;
		private AudioSource             _musicA;
		private AudioSource             _musicB;
		private AudioSource             _musicActive;
		private AudioConfig             _currentMusic;
		private CancellationTokenSource _musicCts;

		public AudioConfig CurrentMusic => _currentMusic;

		private void Awake()
		{
			InitializePools();
			InitializeMusicLane();
		}

		public T Spawn<T>(Uid<AudioConfig> variant = default) where T : AudioComponent
		{
			Type requestedType = typeof(T);

			AudioConfig config = _audioRegistry.GetConfigStrict(requestedType, variant);
			if (config == null)
			{
				Debug.LogError($"Spawn<{requestedType.Name}>() failed: no config for type '{requestedType.Name}' with variant {UidDebugNames.Describe(variant)}.");
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

		/// <summary>Plays the given config's audio. The primary API — call sites hold the config, not an identity.</summary>
		public AudioComponent PlayAudio(AudioConfig config, Vector3? position = null)
		{
			if (config == null)
			{
				Debug.LogError("PlayAudio() failed: config is null.");
				return null;
			}

			if (config.Prefab == null)
			{
				Debug.LogError($"PlayAudio() failed: config '{config.name}' has no prefab.", config);
				return null;
			}

			if (!PassesPlayGates(config))
			{
				return null;
			}

			Type prefabType = config.Prefab.GetType();
			AudioComponent audioComponent = GetFromPool(prefabType, config);
			if (audioComponent == null)
			{
				return null;
			}

			TrackActive(audioComponent);

			if (config.DuckMusicDb > 0f && _mixerController != null)
			{
				_mixerController.DuckMusic(config.DuckMusicDb, config.DuckDuration);
			}

			audioComponent.Init(config, () => UntrackAndReturnToPool(prefabType, audioComponent));
			audioComponent.Play(position);
			return audioComponent;
		}

		public AudioComponent PlayAudio(Uid<AudioConfig> configId, Vector3? position = null)
		{
			if (configId.IsNone)
			{
				Debug.LogError("PlayAudio() failed: identity is None.");
				return null;
			}

			if (!_audioRegistry.TryResolve(configId, out AudioConfig config))
			{
				Debug.LogError($"PlayAudio() failed: no config for identity {UidDebugNames.Describe(configId)}. Check AudioRegistry.");
				return null;
			}

			return PlayAudio(config, position);
		}

		public bool IsPlaying(AudioConfig config)
		{
			if (config == null) return false;

			Uid<AudioConfig> id = config.IdAs<AudioConfig>();
			for (int i = 0; i < _activeComponents.Count; i++)
			{
				AudioComponent c = _activeComponents[i];
				if (c != null && c.ConfigId == id) return true;
			}

			return false;
		}

		public void StopAll(AudioConfig config = null)
		{
			bool filter = config != null;
			Uid<AudioConfig> id = filter ? config.IdAs<AudioConfig>() : default;

			// Snapshot — Stop() mutates the active list via the onStop callback
			foreach (var component in _activeComponents.ToList())
			{
				if (component == null) continue;
				if (filter && component.ConfigId != id) continue;

				component.Stop();
			}
		}

		// =================================================================
		// MUSIC LANE
		// =================================================================

		public void PlayMusic(AudioConfig config, float crossfadeSeconds = 1f)
		{
			if (config == null)
			{
				Debug.LogError("PlayMusic() failed: config is null.");
				return;
			}

			if (_currentMusic == config)
			{
				return;
			}

			var clip = config.Clips != null ? config.Clips.FirstOrDefault(c => c != null) : null;
			if (clip == null)
			{
				Debug.LogError($"PlayMusic() failed: config '{config.name}' has no usable clips.");
				return;
			}

			var from = _musicActive;
			var to = from == _musicA ? _musicB : _musicA;

			to.clip = clip;
			to.outputAudioMixerGroup = config.OutputGroup;
			to.loop = config.Loop;
			to.pitch = 1f;
			to.spatialBlend = 0f;
			to.volume = 0f;
			to.Play();

			_currentMusic = config;
			_musicActive = to;

			_musicCts?.Cancel();
			_musicCts?.Dispose();
			_musicCts = new CancellationTokenSource();

			CrossfadeAsync(from, to, config.Volume, crossfadeSeconds, _musicCts.Token).Forget();
		}

		public void StopMusic(float fadeOutSeconds = 1f)
		{
			if (_musicActive == null) return;

			var fading = _musicActive;
			_currentMusic = null;
			_musicActive = null;

			_musicCts?.Cancel();
			_musicCts?.Dispose();
			_musicCts = new CancellationTokenSource();

			FadeOutSourceAsync(fading, fadeOutSeconds, _musicCts.Token).Forget();
		}

		private void InitializeMusicLane()
		{
			_musicRoot = new GameObject("AudioSpawner_Music");
			_musicA = CreateMusicSource("Music_A");
			_musicB = CreateMusicSource("Music_B");

			if (_persistMusicAcrossScenes)
			{
				DontDestroyOnLoad(_musicRoot);
			}
		}

		private AudioSource CreateMusicSource(string childName)
		{
			var go = new GameObject(childName);
			go.transform.SetParent(_musicRoot.transform, false);

			var source = go.AddComponent<AudioSource>();
			source.playOnAwake = false;
			source.spatialBlend = 0f;
			return source;
		}

		private async UniTaskVoid CrossfadeAsync(AudioSource from, AudioSource to, float toVolume, float duration, CancellationToken ct)
		{
			float fromVolume = from != null ? from.volume : 0f;
			float time = 0f;

			while (time < duration)
			{
				time += Time.deltaTime;
				float t = duration <= 0f ? 1f : time / duration;

				if (from != null) from.volume = Mathf.Lerp(fromVolume, 0f, t);
				to.volume = Mathf.Lerp(0f, toVolume, t);

				await UniTask.Yield(ct);
			}

			if (from != null)
			{
				from.Stop();
				from.volume = fromVolume;
			}

			to.volume = toVolume;
		}

		private async UniTaskVoid FadeOutSourceAsync(AudioSource source, float duration, CancellationToken ct)
		{
			float startVolume = source.volume;
			float time = 0f;

			while (time < duration)
			{
				time += Time.deltaTime;
				source.volume = Mathf.Lerp(startVolume, 0f, duration <= 0f ? 1f : time / duration);
				await UniTask.Yield(ct);
			}

			source.Stop();
			source.volume = startVolume;
		}

		// =================================================================
		// CONCURRENCY + TRACKING
		// =================================================================

		private bool PassesPlayGates(AudioConfig config)
		{
			Uid id = config.Id;

			if (config.MinIntervalBetweenPlays > 0f &&
			    _lastPlayTimeByConfig.TryGetValue(id, out float lastPlay) &&
			    Time.time - lastPlay < config.MinIntervalBetweenPlays)
			{
				return false;
			}

			if (config.MaxConcurrentVoices > 0)
			{
				int active = 0;
				AudioComponent oldest = null;
				var typed = new Uid<AudioConfig>(id);

				foreach (var component in _activeComponents)
				{
					if (component == null || component.ConfigId != typed) continue;

					active++;
					oldest ??= component; // list is spawn-ordered — first match is oldest
				}

				if (active >= config.MaxConcurrentVoices)
				{
					// Steal-oldest: the oldest voice stops (fade-out) to make room
					oldest?.Stop();
				}
			}

			_lastPlayTimeByConfig[id] = Time.time;
			return true;
		}

		private void TrackActive(AudioComponent component)
		{
			_activeComponents.Add(component);
		}

		private void UntrackAndReturnToPool(Type prefabType, AudioComponent component)
		{
			_activeComponents.Remove(component);
			ReturnToPool(prefabType, component);
		}

		// =================================================================
		// POOLING
		// =================================================================

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
			if (_pools != null)
			{
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
				_poolSizes?.Clear();
			}

			_musicCts?.Cancel();
			_musicCts?.Dispose();

			if (_musicRoot != null)
			{
				Destroy(_musicRoot);
			}
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

			Debug.Log($"=== Audio Pool Statistics ===");
			Debug.Log($"Total Pools: {_pools.Count}");
			Debug.Log($"Total Instances: {_poolSizes.Values.Sum()}");
			Debug.Log($"Active Voices: {_activeComponents.Count}");

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
