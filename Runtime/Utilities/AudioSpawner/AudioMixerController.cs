using AK.Core;
using AK.Core.Extensions;
using AK.Kernel.Timing;
using UnityEngine;
using UnityEngine.Audio;

namespace AK.Utilities.Audio
{
	/// <summary>
	/// Owns the mixer: per-channel volumes (persisted via prefs, applied at boot),
	/// snapshot transitions as joint assets, and music ducking. Channel parameter names
	/// are serialized fields — no magic strings at call sites.
	/// A duck lasts its duration in the controller's time domain, counted down each frame, and a
	/// music volume set meanwhile keeps it. Snapshot transitions run on the mixer's own clock,
	/// its Update Mode: Normal stops at a time scale of zero, Unscaled Time doesn't.
	/// </summary>
	public class AudioMixerController : GameEntity, IAudioMixerController
	{
		[Header("Mixer")]
		[SerializeField] private AudioMixer _mainMixer;

		[Header("Channel parameter names (must match the mixer's exposed parameters)")]
		[SerializeField] private string _masterVolumeParam = "MasterVolume";
		[SerializeField] private string _musicVolumeParam  = "MusicVolume";
		[SerializeField] private string _sfxVolumeParam    = "SFXVolume";
		[SerializeField] private string _uiSfxVolumeParam  = "UISFXVolume";

		[Header("Snapshots")]
		[SerializeField] private AudioSnapshot _bootSnapshot;

		[Header("Time")]
		[SerializeField, Tooltip("The time a music duck lasts on. Snapshot transitions follow the mixer's own Update Mode instead.")]
		private TimeDomain _timeDomain = TimeDomain.Unscaled;

		private readonly PrefsProperty<float> _masterVolume = new("UGFW_AUDIO_VOL_MASTER", 1f);
		private readonly PrefsProperty<float> _musicVolume  = new("UGFW_AUDIO_VOL_MUSIC", 1f);
		private readonly PrefsProperty<float> _sfxVolume    = new("UGFW_AUDIO_VOL_SFX", 1f);

		private float  _duckDb;
		private double _duckSecondsLeft;

		/// <summary>How far the music is ducked now, in dB; 0 when it isn't.</summary>
		internal float DuckDb => _duckDb;

		private void Start()
		{
			if (_bootSnapshot != null)
			{
				TransitionToSnapshot(_bootSnapshot, 0f);
			}

			ApplyPersistedVolumes();
		}

		private void ApplyPersistedVolumes()
		{
			ApplyVolume(_masterVolumeParam, _masterVolume.Read());
			ApplyMusicVolume();
			ApplySfxVolumes(_sfxVolume.Read());
		}

		private void Update()
		{
			if (_duckDb > 0f)
			{
				StepDuck(_timeDomain.DeltaTime());
			}
		}

		public void SetVolume(AudioChannel channel, float linearValue)
		{
			linearValue = Mathf.Clamp01(linearValue);

			switch (channel)
			{
				case AudioChannel.Master: _masterVolume.Save(linearValue); ApplyVolume(_masterVolumeParam, linearValue); break;
				case AudioChannel.Music:  _musicVolume.Save(linearValue);  ApplyMusicVolume();                           break;
				case AudioChannel.Sfx:    _sfxVolume.Save(linearValue);    ApplySfxVolumes(linearValue);                 break;
			}
		}

		public float GetVolume(AudioChannel channel)
		{
			return channel switch
			{
				AudioChannel.Master => _masterVolume.Read(),
				AudioChannel.Music  => _musicVolume.Read(),
				AudioChannel.Sfx    => _sfxVolume.Read(),
				_                   => 1f
			};
		}

		public void TransitionToSnapshot(AudioSnapshot snapshot, float transitionTime = -1f)
		{
			if (snapshot == null || snapshot.Snapshot == null)
			{
				Debug.LogError("[AudioMixerController] TransitionToSnapshot failed: snapshot asset or its mixer snapshot is null.", this);
				return;
			}

			float time = transitionTime >= 0f ? transitionTime : snapshot.DefaultTransitionTime;
			snapshot.Snapshot.TransitionTo(time);
		}

		public void DuckMusic(float duckDb, float durationSeconds)
		{
			// The latest duck wins: its depth, for its duration from now.
			_duckDb          = Mathf.Abs(duckDb);
			_duckSecondsLeft = durationSeconds;
			ApplyMusicVolume();
		}

		/// <summary>Counts <paramref name="seconds"/> of the controller's time off the duck, and restores the music when it runs out.</summary>
		internal void StepDuck(double seconds)
		{
			if (_duckDb <= 0f) return;

			if (seconds > 0d) _duckSecondsLeft -= seconds;
			if (_duckSecondsLeft > 0d) return;

			_duckDb = 0f;
			ApplyMusicVolume();
		}

		private void ApplyMusicVolume()
		{
			ApplyVolumeDb(_musicVolumeParam, ToDecibels(_musicVolume.Read()) - _duckDb);
		}

		private void ApplySfxVolumes(float linearValue)
		{
			ApplyVolume(_sfxVolumeParam, linearValue);
			ApplyVolume(_uiSfxVolumeParam, linearValue);
		}

		private void ApplyVolume(string parameterName, float linearValue)
		{
			ApplyVolumeDb(parameterName, ToDecibels(linearValue));
		}

		private void ApplyVolumeDb(string parameterName, float dbValue)
		{
			if (_mainMixer != null)
			{
				_mainMixer.SetFloat(parameterName, dbValue);
			}
		}

		// Linear 0..1 → logarithmic dB (-80..0). Clamped to avoid log(0).
		private static float ToDecibels(float linearValue)
		{
			return Mathf.Log10(Mathf.Max(linearValue, 0.0001f)) * 20f;
		}
	}
}
