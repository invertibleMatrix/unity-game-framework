using System;
using System.Threading;
using AK.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

namespace Utilities.AudioSpawner
{
	/// <summary>
	/// Owns the mixer: per-channel volumes (persisted via prefs, applied at boot),
	/// snapshot transitions as joint assets, and music ducking. Channel parameter names
	/// are serialized fields — no magic strings at call sites.
	/// </summary>
	public class AudioMixerController : GameEntity, IAudioMixerController
	{
		[Header("Mixer")]
		[SerializeField] private AudioMixer _mainMixer;

		[Header("Channel parameter names (must match the mixer's exposed parameters)")]
		[SerializeField] private string _masterVolumeParam = "MasterVolume";
		[SerializeField] private string _musicVolumeParam  = "MusicVolume";
		[SerializeField] private string _sfxVolumeParam    = "SfxVolume";

		[Header("Snapshots")]
		[SerializeField] private AudioSnapshot _bootSnapshot;

		private readonly PrefsProperty<float> _masterVolume = new("UGFW_AUDIO_VOL_MASTER", 1f);
		private readonly PrefsProperty<float> _musicVolume  = new("UGFW_AUDIO_VOL_MUSIC", 1f);
		private readonly PrefsProperty<float> _sfxVolume    = new("UGFW_AUDIO_VOL_SFX", 1f);

		private CancellationTokenSource _duckCts;

		private void Awake()
		{
			ApplyVolume(_masterVolumeParam, _masterVolume.Read());
			ApplyVolume(_musicVolumeParam, _musicVolume.Read());
			ApplyVolume(_sfxVolumeParam, _sfxVolume.Read());

			if (_bootSnapshot != null)
			{
				TransitionToSnapshot(_bootSnapshot, 0f);
			}
		}

		public void SetVolume(AudioChannel channel, float linearValue)
		{
			linearValue = Mathf.Clamp01(linearValue);

			switch (channel)
			{
				case AudioChannel.Master: _masterVolume.Save(linearValue); ApplyVolume(_masterVolumeParam, linearValue); break;
				case AudioChannel.Music:  _musicVolume.Save(linearValue);  ApplyVolume(_musicVolumeParam, linearValue);  break;
				case AudioChannel.Sfx:    _sfxVolume.Save(linearValue);    ApplyVolume(_sfxVolumeParam, linearValue);    break;
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
			// Latest duck wins: cancel any pending restore, duck now, schedule restore.
			_duckCts?.Cancel();
			_duckCts?.Dispose();
			_duckCts = new CancellationTokenSource();

			ApplyVolumeDb(_musicVolumeParam, ToDecibels(_musicVolume.Read()) - Mathf.Abs(duckDb));
			RestoreMusicAfterAsync(durationSeconds, _duckCts.Token).Forget();
		}

		private async UniTaskVoid RestoreMusicAfterAsync(float delaySeconds, CancellationToken ct)
		{
			bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), DelayType.DeltaTime, PlayerLoopTiming.Update, ct)
			                              .SuppressCancellationThrow();

			if (cancelled) return;

			ApplyVolumeDb(_musicVolumeParam, ToDecibels(_musicVolume.Read()));
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

		private void OnDestroy()
		{
			_duckCts?.Cancel();
			_duckCts?.Dispose();
			_masterVolume.Dispose();
			_musicVolume.Dispose();
			_sfxVolume.Dispose();
		}
	}
}
