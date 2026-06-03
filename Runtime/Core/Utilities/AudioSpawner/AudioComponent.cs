using System;
using System.Collections;
using System.Linq;
using AK.Core;
using AK.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Utilities.AudioSpawner
{
	[RequireComponent(typeof(AudioSource))]
	public class AudioComponent : GameEntity
	{
		[SerializeField] protected AudioSource _audioSource;

		private Action          _onStop;
		private AudioConfig _config;
		private Coroutine       _fadeCoroutine;
		private Coroutine       _playCoroutine;

		public UID ConfigVariantId { get; private set; }

		protected virtual void OnValidate()
		{
			if (_audioSource == null)
			{
				_audioSource = GetComponent<AudioSource>();
			}
		}

		public virtual void Init(AudioConfig config, Action onStop)
		{
			ConfigVariantId = config.UniqueID;
			_config = config;
			_onStop = onStop;
			_audioSource.playOnAwake = false;
			_audioSource.outputAudioMixerGroup = _config.OutputGroup;
		}

		public virtual void Play(Vector3? position = null)
		{
			gameObject.SetActive(true);

			if (position.HasValue)
			{
				transform.position = position.Value;
			}

			if (_config.Clips == null || _config.Clips.Count == 0)
			{
				Debug.LogError($"AudioComponent '{name}' has a config with no clips. Cannot play.", gameObject);
				_onStop?.Invoke(); // Immediately return to pool
				return;
			}

			if (_playCoroutine != null)
			{
				StopCoroutine(_playCoroutine);
			}

			_playCoroutine = StartCoroutine(PlayCoroutine());
		}

		private IEnumerator PlayCoroutine()
		{
			var clips = _config.Clips.ToList();
			if (!_config.PlayAllSequentially)
			{
				clips.Shuffle();
			}

			_audioSource.volume = _config.Volume;
			_audioSource.spatialBlend = _config.IsSpatial ? 1.0f : 0.0f;
			_audioSource.loop = _config.Loop;

			if (_config.StartAfterSeconds > 0)
			{
				yield return new WaitForSeconds(_config.StartAfterSeconds);
			}

			int clipsCount = clips.Count;
			if (!_config.Loop && !_config.PlayAllSequentially)
			{
				clipsCount = 1;
			}

			float stopAfterStamp = Mathf.Max(0, _config.StopAfterSeconds - _config.FadeOutDuration) + Time.time;
			int clipIndex = 0;
			while (true)
			{
				_audioSource.pitch = Random.Range(_config.PitchRange.x, _config.PitchRange.y);
				_audioSource.clip = clips[clipIndex++];

				_audioSource.Play();
				yield return FadeIn();

				if (_config.StopAfterSeconds > 0 && Time.time > stopAfterStamp)
				{
					yield return FadeOutStop();
					break;
				}

				float waitTillNextLoop = Mathf.Min(_audioSource.clip.length, _audioSource.clip.length - _config.FadeOutDuration);

				if (_config.LoopInterval > 0)
				{
					waitTillNextLoop = Mathf.Min(waitTillNextLoop, _config.LoopInterval);
				}

				yield return new WaitForSeconds(waitTillNextLoop);
				yield return FadeOut();

				if (_config.Loop)
				{
					clipIndex %= clipsCount;
				}

				if (clipIndex >= clipsCount)
				{
					break;
				}
			}

			_audioSource.Stop();
			_onStop?.Invoke();
		}

		/// <summary>
		/// Stops a playing sound and returns the component to the pool.
		/// This is required for stopping looping sounds.
		/// </summary>
		public void Stop()
		{
			if (!gameObject.activeInHierarchy) return;

			if (_fadeCoroutine != null)
			{
				StopCoroutine(_fadeCoroutine);
			}

			_fadeCoroutine = StartCoroutine(FadeOutStop());
		}

		private IEnumerator FadeIn()
		{
			float targetVolume = _config.Volume;
			if (_config.FadeInDuration > 0)
			{
				_audioSource.volume = 0;
				float time = 0;
				while (time < _config.FadeInDuration)
				{
					_audioSource.volume = Mathf.Lerp(0, targetVolume, time / _config.FadeInDuration);
					time += Time.deltaTime;
					yield return null;
				}
			}

			_audioSource.volume = targetVolume;
		}

		private IEnumerator FadeOut()
		{
			float startVolume = _audioSource.volume;
			if (_config.FadeOutDuration > 0)
			{
				float time = 0;
				while (time < _config.FadeOutDuration)
				{
					_audioSource.volume = Mathf.Lerp(startVolume, 0, time / _config.FadeOutDuration);
					time += Time.deltaTime;
					yield return null;
				}
			}
		}

		private IEnumerator FadeOutStop()
		{
			yield return FadeOut();
			_audioSource.Stop();
			_onStop?.Invoke();
		}
	}
}