using System;
using System.Collections.Generic;
using AK.Core;
using AK.Core.Extensions;
using AK.Kernel.Timing;
using UnityEngine;

namespace AK.Utilities.Audio
{
	/// <summary>
	/// Plays pooled voices, and one music track at a time.
	///
	/// <para>Voices are pooled per prefab. Each pool starts with the largest
	/// <see cref="AudioConfig.InitialPoolSize"/> among the registry's configs for its prefab, and
	/// grows when it runs dry. A voice is tracked from the moment it is handed out, by
	/// <see cref="PlayAudio(AudioConfig, Vector3?)"/> or <see cref="Spawn{T}"/>, until it ends
	/// and goes back to its pool.</para>
	///
	/// <para>A voice passes its config's gates as it starts to play: the replay gap turns it away
	/// when it comes too soon after the last play; the voice cap makes room by stopping the oldest
	/// voice of the config still sounding; then the voice ducks the music.</para>
	///
	/// <para>Music plays on two sources that crossfade. Its fades, and every voice's timing, run
	/// on the spawner's time.</para>
	///
	/// Main thread only.
	/// </summary>
	public class AudioSpawner : GameEntity, IAudioSpawner
	{
		[SerializeField]
		private AudioRegistry _audioRegistry;

		[SerializeField, Tooltip("The time sounds are timed on: start delays, fades, stop-after, clip holds, replay gaps and music crossfades. Clips play in real time at any time scale; Unscaled keeps their timing in step, and working in a game paused at timeScale 0.")]
		private TimeDomain _timeDomain = TimeDomain.Unscaled;

		[Header("Music")]
		[SerializeField, Tooltip("Music survives scene loads (music sources live on a DontDestroyOnLoad root). Otherwise they live under the spawner.")]
		private bool _persistMusicAcrossScenes = true;

		[SerializeField, Tooltip("Used for ducking: configs with DuckMusicDb > 0 lower the music channel while playing.")]
		private AudioMixerController _mixerController;

		private readonly Dictionary<AudioComponent, Stack<AudioComponent>> _pools         = new();
		private readonly List<AudioComponent>                              _voices        = new();
		private readonly Dictionary<AudioConfig, double>                   _lastPlayTimes = new();
		private readonly System.Random                                     _random        = new();

		private bool        _initialized;
		private GameObject  _musicRoot;
		private MusicSource _musicA;
		private MusicSource _musicB;
		private MusicSource _musicActive;
		private AudioConfig _currentMusic;

		/// <summary>The music playing: null after <see cref="StopMusic"/>, and once a track that doesn't loop has played out.</summary>
		public AudioConfig CurrentMusic =>
			_musicActive != null && (_musicActive.Source.loop || _musicActive.Source.isPlaying) ? _currentMusic : null;

		private void Awake()
		{
			EnsureInitialized();
		}

		private void Update()
		{
			StepMusic(_timeDomain.DeltaTime());
		}

		private void OnDestroy()
		{
			// Voices are children of the spawner and go with it; a persistent music root doesn't.
			if (_musicRoot != null)
			{
				Destroy(_musicRoot);
			}
		}

		// =================================================================
		// VOICES
		// =================================================================

		public T Spawn<T>(Uid<AudioConfig> variant = default) where T : AudioComponent
		{
			Type requestedType = typeof(T);

			AudioConfig config = _audioRegistry != null ? _audioRegistry.GetConfigStrict(requestedType, variant) : null;
			if (config == null)
			{
				Debug.LogError($"Spawn<{requestedType.Name}>() failed: no config for type '{requestedType.Name}' with variant {UidDebugNames.Describe(variant)}.");
				return null;
			}

			if (config.Prefab == null || config.Prefab.GetType() != requestedType)
			{
				Debug.LogError($"Spawn<{requestedType.Name}>() failed: config '{config.name}' has no prefab of type '{requestedType.Name}'.", config);
				return null;
			}

			return (T)Lease(config);
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

			AudioComponent voice = Lease(config);
			voice.Play(position);

			// A voice turned away by a gate, or with nothing to play, has gone back to its pool.
			return voice.IsPlaying ? voice : null;
		}

		public AudioComponent PlayAudio(Uid<AudioConfig> configId, Vector3? position = null)
		{
			if (configId.IsNone)
			{
				Debug.LogError("PlayAudio() failed: identity is None.");
				return null;
			}

			if (_audioRegistry == null || !_audioRegistry.TryResolve(configId, out AudioConfig config))
			{
				Debug.LogError($"PlayAudio() failed: no config for identity {UidDebugNames.Describe(configId)}. Check AudioRegistry.");
				return null;
			}

			return PlayAudio(config, position);
		}

		public bool IsPlaying(AudioConfig config)
		{
			if (config == null) return false;

			for (int i = 0; i < _voices.Count; i++)
			{
				AudioComponent voice = _voices[i];
				if (ReferenceEquals(voice.Config, config) && voice.IsPlaying) return true;
			}

			return false;
		}

		public void StopAll(AudioConfig config = null)
		{
			// Back to front: a voice that ends at once leaves the list.
			for (int i = _voices.Count - 1; i >= 0; i--)
			{
				if (i >= _voices.Count) continue;

				AudioComponent voice = _voices[i];
				if (config == null || ReferenceEquals(voice.Config, config))
				{
					voice.Stop();
				}
			}
		}

		/// <summary>
		/// The gates a voice passes as it starts to play: false turns it away. A voice that
		/// passes is counted as played now, may stop another to make room, and ducks the music.
		/// </summary>
		internal bool OnVoiceStarting(AudioComponent voice)
		{
			AudioConfig config = voice.Config;
			double now = _timeDomain.Now();

			if (config.MinIntervalBetweenPlays > 0f &&
			    _lastPlayTimes.TryGetValue(config, out double lastPlay) &&
			    now - lastPlay < config.MinIntervalBetweenPlays)
			{
				return false;
			}

			if (config.MaxConcurrentVoices > 0)
			{
				// Voices fading out to their end don't count, and can't make room.
				int sounding = 0;
				AudioComponent oldest = null;

				for (int i = 0; i < _voices.Count; i++)
				{
					AudioComponent other = _voices[i];
					if (ReferenceEquals(other, voice) || !ReferenceEquals(other.Config, config) ||
					    !other.IsPlaying || other.IsStopping)
					{
						continue;
					}

					sounding++;
					oldest ??= other; // the list is in the order voices were handed out
				}

				if (sounding >= config.MaxConcurrentVoices)
				{
					oldest.Stop();
				}
			}

			_lastPlayTimes[config] = now;

			if (config.DuckMusicDb > 0f && _mixerController != null)
			{
				_mixerController.DuckMusic(config.DuckMusicDb, config.DuckDuration);
			}

			return true;
		}

		/// <summary>Takes back a voice that ended: untracked, deactivated and pooled.</summary>
		internal void Release(AudioComponent voice)
		{
			_voices.Remove(voice);
			voice.gameObject.SetActive(false);
			PoolOf(voice.Prefab).Push(voice);
		}

		/// <summary>Stops tracking a voice destroyed outside the spawner.</summary>
		internal void Forget(AudioComponent voice)
		{
			_voices.Remove(voice);
		}

		/// <summary>A voice for <paramref name="config"/> from its prefab's pool: bound to the config, and tracked.</summary>
		private AudioComponent Lease(AudioConfig config)
		{
			EnsureInitialized();

			Stack<AudioComponent> pool = PoolOf(config.Prefab);
			AudioComponent voice = null;

			// Skips voices destroyed while pooled.
			while (voice == null && pool.Count > 0)
			{
				voice = pool.Pop();
			}

			if (voice == null)
			{
				voice = Create(config.Prefab);
			}

			voice.Init(config, _timeDomain, null);
			_voices.Add(voice);
			return voice;
		}

		private Stack<AudioComponent> PoolOf(AudioComponent prefab)
		{
			if (!_pools.TryGetValue(prefab, out Stack<AudioComponent> pool))
			{
				pool = new Stack<AudioComponent>();
				_pools.Add(prefab, pool);
			}

			return pool;
		}

		private AudioComponent Create(AudioComponent prefab)
		{
			AudioComponent voice = Instantiate(prefab, transform);
			voice.name = prefab.name;
			voice.gameObject.SetActive(false);
			voice.Adopt(this, prefab, _random);
			return voice;
		}

		private void EnsureInitialized()
		{
			if (_initialized) return;
			_initialized = true;

			Prewarm();
			CreateMusicLane();
		}

		/// <summary>Fills each prefab's pool to the largest initial size among its configs.</summary>
		private void Prewarm()
		{
			if (_audioRegistry == null) return;

			IReadOnlyList<AudioConfig> configs = _audioRegistry.AudioConfigs;
			var sizes = new Dictionary<AudioComponent, int>();

			for (int i = 0; i < configs.Count; i++)
			{
				AudioConfig config = configs[i];
				if (config == null || config.Prefab == null) continue;

				sizes.TryGetValue(config.Prefab, out int size);
				sizes[config.Prefab] = Math.Max(size, config.InitialPoolSize);
			}

			foreach (KeyValuePair<AudioComponent, int> pair in sizes)
			{
				Stack<AudioComponent> pool = PoolOf(pair.Key);
				while (pool.Count < pair.Value)
				{
					pool.Push(Create(pair.Key));
				}
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

			EnsureInitialized();

			if (ReferenceEquals(CurrentMusic, config))
			{
				return;
			}

			AudioClip clip = FirstClip(config.Clips);
			if (clip == null)
			{
				Debug.LogError($"PlayMusic() failed: config '{config.name}' has no clips.", config);
				return;
			}

			// The new track takes the quieter source, and the other fades out.
			MusicSource to   = _musicB.Source.volume < _musicA.Source.volume ? _musicB : _musicA;
			MusicSource from = to == _musicA ? _musicB : _musicA;

			to.Begin(clip, config);
			to.FadeTo(config.Volume, crossfadeSeconds, stopAtEnd: false);
			from.FadeOut(crossfadeSeconds);

			_currentMusic = config;
			_musicActive  = to;
		}

		public void StopMusic(float fadeOutSeconds = 1f)
		{
			_currentMusic = null;
			_musicActive  = null;

			if (_musicA == null) return;

			_musicA.FadeOut(fadeOutSeconds);
			_musicB.FadeOut(fadeOutSeconds);
		}

		/// <summary>Moves the music's fades on by <paramref name="seconds"/> of the spawner's time, as a frame does.</summary>
		internal void StepMusic(double seconds)
		{
			if (_musicA == null) return;

			_musicA.Step(seconds);
			_musicB.Step(seconds);
		}

		private void CreateMusicLane()
		{
			_musicRoot = new GameObject("AudioSpawner_Music");

			if (_persistMusicAcrossScenes && Application.isPlaying)
			{
				DontDestroyOnLoad(_musicRoot);
			}
			else
			{
				_musicRoot.transform.SetParent(transform, false);
			}

			_musicA = new MusicSource(CreateMusicSource("Music_A"));
			_musicB = new MusicSource(CreateMusicSource("Music_B"));
		}

		private AudioSource CreateMusicSource(string childName)
		{
			var go = new GameObject(childName);
			go.transform.SetParent(_musicRoot.transform, false);

			var source = go.AddComponent<AudioSource>();
			source.playOnAwake = false;
			source.spatialBlend = 0f;
			source.volume = 0f;
			return source;
		}

		private static AudioClip FirstClip(List<AudioClip> clips)
		{
			if (clips == null) return null;

			for (int i = 0; i < clips.Count; i++)
			{
				if (clips[i] != null) return clips[i];
			}

			return null;
		}

		/// <summary>A music source and the fade it is in.</summary>
		private sealed class MusicSource
		{
			public readonly AudioSource Source;

			private float  _from;
			private float  _to;
			private double _duration;
			private double _elapsed;
			private bool   _fading;
			private bool   _stopAtEnd;

			public MusicSource(AudioSource source)
			{
				Source = source;
			}

			/// <summary>Starts <paramref name="clip"/> from silence, set up for <paramref name="config"/>.</summary>
			public void Begin(AudioClip clip, AudioConfig config)
			{
				_fading = false;

				Source.Stop();
				Source.clip                  = clip;
				Source.outputAudioMixerGroup = config.OutputGroup;
				Source.loop                  = config.Loop;
				Source.pitch                 = 1f;
				Source.spatialBlend          = 0f;
				Source.volume                = 0f;
				Source.Play();
			}

			/// <summary>
			/// Fades from the volume now to <paramref name="to"/> over <paramref name="seconds"/>,
			/// then stops the track when <paramref name="stopAtEnd"/>. No time applies it at once.
			/// </summary>
			public void FadeTo(float to, double seconds, bool stopAtEnd)
			{
				_from      = Source.volume;
				_to        = to;
				_duration  = seconds > 0d ? seconds : 0d;
				_elapsed   = 0d;
				_stopAtEnd = stopAtEnd;
				_fading    = true;

				Step(0d);
			}

			/// <summary>Fades out over <paramref name="seconds"/> and stops, unless a fade-out under way ends sooner.</summary>
			public void FadeOut(double seconds)
			{
				if (Source.clip == null) return;
				if (_fading && _stopAtEnd && _duration - _elapsed <= seconds) return;

				FadeTo(0f, seconds, stopAtEnd: true);
			}

			public void Step(double seconds)
			{
				if (!_fading) return;

				if (seconds > 0d) _elapsed += seconds;

				if (_elapsed < _duration)
				{
					Source.volume = Mathf.Lerp(_from, _to, (float)(_elapsed / _duration));
					return;
				}

				_fading = false;
				Source.volume = _to;

				if (_stopAtEnd)
				{
					Source.Stop();
					Source.clip = null;
				}
			}
		}

#if UNITY_EDITOR
		[ContextMenu("Log Pool Statistics")]
		private void LogPoolStatistics()
		{
			int pooled = 0;
			foreach (KeyValuePair<AudioComponent, Stack<AudioComponent>> pair in _pools)
			{
				pooled += pair.Value.Count;
			}

			Debug.Log($"=== Audio Pool Statistics ===\nPools: {_pools.Count} | Pooled voices: {pooled} | Voices out: {_voices.Count}");

			foreach (KeyValuePair<AudioComponent, Stack<AudioComponent>> pair in _pools)
			{
				Debug.Log($"Pool '{(pair.Key != null ? pair.Key.name : "(destroyed prefab)")}': {pair.Value.Count} available");
			}
		}
#endif
	}
}
