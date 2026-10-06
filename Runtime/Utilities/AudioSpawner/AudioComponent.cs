using System;
using System.Collections.Generic;
using AK.Core;
using AK.Core.Extensions;
using AK.Kernel.Audio;
using AK.Kernel.Timing;
using UnityEngine;

namespace AK.Utilities.Audio
{
	/// <summary>
	/// A voice: plays an <see cref="AudioConfig"/>'s clips on its AudioSource. A
	/// <see cref="VoiceTimeline"/> times them, stepped each frame on the voice's time: the start
	/// delay, each clip's fades and hold, and stop-after. A clip is held for its length at the
	/// pitch it plays at.
	///
	/// <para>A voice from an <see cref="AudioSpawner"/> passes the spawner's gates when it
	/// starts. When it ends it goes back to its pool and forgets its config, so keep no
	/// reference to it past its end. A voice on an object of your own keeps its config and can
	/// play again.</para>
	///
	/// <para>A deactivated voice pauses its timing, and Unity stops its sound. Once active again
	/// it sounds from its next clip.</para>
	/// </summary>
	[RequireComponent(typeof(AudioSource))]
	public class AudioComponent : GameEntity
	{
		/// <summary>Slower than this, in either direction, a clip is held for its length at normal speed.</summary>
		private const float MinSpeed = 0.01f;

		[SerializeField] protected AudioSource _audioSource;

		private readonly VoiceTimeline _timeline = new();

		private AudioClip[]   _order = Array.Empty<AudioClip>();
		private AudioConfig   _config;
		private TimeDomain    _time = TimeDomain.Unscaled;
		private Action        _onStop;
		private Transform     _followTarget;
		private System.Random _random;
		private float         _appliedVolume = -1f;
		private int           _startFrame    = -1;

		public Uid<AudioConfig> ConfigId { get; private set; }

		/// <summary>From <see cref="Play"/> until the voice ends: its start delay and fade-out included.</summary>
		public bool IsPlaying => _timeline.IsPlaying;

		/// <summary>The config the voice plays: null before <see cref="Init"/>, and once a pooled voice has ended.</summary>
		internal AudioConfig Config => _config;

		/// <summary>Fading out to its end, after <see cref="Stop"/> or at its stop-after time.</summary>
		internal bool IsStopping => _timeline.IsStopping;

		/// <summary>The spawner whose pool the voice belongs to; null for a voice of your own.</summary>
		internal AudioSpawner Owner { get; private set; }

		/// <summary>The prefab the voice was made from: the pool it goes back to.</summary>
		internal AudioComponent Prefab { get; private set; }

		protected virtual void OnValidate()
		{
			if (_audioSource == null)
			{
				_audioSource = GetComponent<AudioSource>();
			}
		}

		/// <summary>
		/// Makes the voice one of <paramref name="owner"/>'s, pooled with <paramref name="prefab"/>'s
		/// voices, drawing its pitches and clip orders from <paramref name="random"/>.
		/// </summary>
		internal void Adopt(AudioSpawner owner, AudioComponent prefab, System.Random random)
		{
			Owner   = owner;
			Prefab  = prefab;
			_random = random;
		}

		/// <summary>
		/// Binds the voice to <paramref name="config"/> for its next play, ending a play in
		/// progress silently. Its start delay, fades, stop-after and clip holds count
		/// <paramref name="time"/>. <paramref name="onStop"/> runs when the voice ends, after a
		/// pooled voice is back in its pool.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
		public virtual void Init(AudioConfig config, TimeDomain time, Action onStop)
		{
			if (config == null) throw new ArgumentNullException(nameof(config));

			Silence();

			ConfigId      = config.IdAs<AudioConfig>();
			_config       = config;
			_time         = time;
			_onStop       = onStop;
			_followTarget = null;

			_audioSource.playOnAwake           = false;
			_audioSource.outputAudioMixerGroup = config.OutputGroup;
		}

		/// <summary>
		/// Plays the voice from its start: at <paramref name="position"/> when given, following
		/// <paramref name="followTarget"/> when given. A pooled voice passes its spawner's gates
		/// first: its replay gap, its voice cap and the music duck. A voice that can't play, with
		/// no clips or turned away by a gate, ends at once, unless it was playing already.
		/// </summary>
		public virtual void Play(Vector3? position = null, Transform followTarget = null)
		{
			if (_config == null)
			{
				Debug.LogError($"AudioComponent '{name}' has no config to play: Init it first. A pooled voice forgets its config when it ends.", this);
				return;
			}

			if (!HasClip(_config.Clips))
			{
				Debug.LogError($"AudioComponent '{name}': config '{_config.name}' has no clips to play.", this);
				End();
				return;
			}

			if (Owner != null && !Owner.OnVoiceStarting(this))
			{
				if (!_timeline.IsPlaying) End();
				return;
			}

			Silence();
			gameObject.SetActive(true);

			if (position.HasValue)
			{
				transform.position = position.Value;
			}

			_followTarget = followTarget;
			_startFrame   = Time.frameCount;

			_audioSource.spatialBlend = _config.IsSpatial ? 1f : 0f;
			_audioSource.loop         = _config.Loop;

			var timing = new VoiceTiming(_config.StartAfterSeconds, _config.StopAfterSeconds, _config.FadeInDuration,
			                             _config.FadeOutDuration, _config.LoopInterval, _config.Loop,
			                             _config.PlayAllSequentially);

			Apply(_timeline.Start(timing, BuildPlayOrder()));
		}

		/// <summary>
		/// Stops the voice: it fades out from where it is over its fade-out, then ends. It ends at
		/// once when it is silent, as during its start delay, or has no fade-out. A pooled voice
		/// handed out but never played goes back to its pool.
		/// </summary>
		public void Stop()
		{
			if (_timeline.IsPlaying)
			{
				Apply(_timeline.Stop());
			}
			else if (Owner != null && _config != null)
			{
				End();
			}
		}

		/// <summary>Moves the voice on by <paramref name="seconds"/> of its time, as a frame does.</summary>
		internal void Step(double seconds)
		{
			if (_timeline.IsPlaying)
			{
				Apply(_timeline.Step(seconds));
			}
		}

		protected virtual void Update()
		{
			// The frame it starts in counts for nothing: the frame's time passed before it started.
			if (_timeline.IsPlaying && _startFrame != Time.frameCount)
			{
				Apply(_timeline.Step(_time.DeltaTime()));
			}
		}

		protected virtual void LateUpdate()
		{
			if (_followTarget != null)
			{
				transform.position = _followTarget.position;
			}
		}

		protected virtual void OnDestroy()
		{
			_timeline.Reset();
			_config = null;

			if (Owner != null)
			{
				Owner.Forget(this);
			}
		}

		private void Apply(VoiceCue cue)
		{
			if (cue == VoiceCue.End)
			{
				End();
				return;
			}

			float volume = _config.Volume * _timeline.Gain;
			if (volume != _appliedVolume)
			{
				_appliedVolume = volume;
				_audioSource.volume = volume;
			}

			if (cue == VoiceCue.StartClip)
			{
				StartClip(_order[_timeline.Clip]);
			}
		}

		private void StartClip(AudioClip clip)
		{
			float pitch = RollPitch();

			_audioSource.pitch = pitch;
			_audioSource.clip  = clip;
			_audioSource.Play();

			float speed = Mathf.Abs(pitch);
			_timeline.ClipStarted(speed >= MinSpeed ? clip.length / speed : clip.length);
		}

		/// <summary>Ends the voice: silences it, gives a pooled voice back to its pool, then runs its stop callback.</summary>
		private void End()
		{
			Silence();
			Action onStop = _onStop;

			if (Owner != null)
			{
				ConfigId      = default;
				_config       = null;
				_onStop       = null;
				_followTarget = null;
				Owner.Release(this);
			}

			onStop?.Invoke();
		}

		/// <summary>Ends the timing and the sound, without a callback.</summary>
		private void Silence()
		{
			_timeline.Reset();
			_appliedVolume = -1f;

			if (_audioSource == null)
			{
				_audioSource = GetComponent<AudioSource>();
			}

			_audioSource.Stop();
			_audioSource.clip = null;
		}

		/// <summary>
		/// Fills the play order with the config's clips that are set, and returns how many there
		/// are. Unless the clips play in their order, the ones the voice plays are drawn at random:
		/// all of them, shuffled, for a voice that loops, and one otherwise.
		/// </summary>
		private int BuildPlayOrder()
		{
			List<AudioClip> clips = _config.Clips;
			int listed = clips != null ? clips.Count : 0;

			if (_order.Length < listed)
			{
				_order = new AudioClip[listed];
			}

			int count = 0;
			for (int i = 0; i < listed; i++)
			{
				AudioClip clip = clips[i];
				if (clip != null) _order[count++] = clip;
			}

			Array.Clear(_order, count, _order.Length - count);

			if (!_config.PlayAllSequentially && count > 1)
			{
				// A partial Fisher-Yates shuffle: the first `drawn` places get a uniform random draw.
				System.Random random = Rng;
				int drawn = _config.Loop ? count - 1 : 1;
				for (int i = 0; i < drawn; i++)
				{
					int j = random.Next(i, count);
					(_order[i], _order[j]) = (_order[j], _order[i]);
				}
			}

			return count;
		}

		private float RollPitch()
		{
			Vector2 range = _config.PitchRange;
			if (range.x == range.y) return range.x;

			return range.x + (float)Rng.NextDouble() * (range.y - range.x);
		}

		/// <summary>The spawner's random numbers, or the voice's own when it has no spawner.</summary>
		private System.Random Rng => _random ??= new System.Random();

		private static bool HasClip(List<AudioClip> clips)
		{
			if (clips == null) return false;

			for (int i = 0; i < clips.Count; i++)
			{
				if (clips[i] != null) return true;
			}

			return false;
		}
	}
}
