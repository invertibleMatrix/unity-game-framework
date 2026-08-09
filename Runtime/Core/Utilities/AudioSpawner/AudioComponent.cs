using System;
using System.Linq;
using System.Threading;
using AK.Core;
using AK.Utilities;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Utilities.AudioSpawner
{
	[RequireComponent(typeof(AudioSource))]
	public class AudioComponent : GameEntity
	{
		[SerializeField] protected AudioSource _audioSource;

		private Action                  _onStop;
		private AudioConfig             _config;
		private CancellationTokenSource _playCts;
		private CancellationTokenSource _stopCts;
		private Transform               _followTarget;

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
			CancelPlay();

			ConfigVariantId = config.UniqueID;
			_config = config;
			_onStop = onStop;
			_followTarget = null;
			_audioSource.playOnAwake = false;
			_audioSource.outputAudioMixerGroup = _config.OutputGroup;
		}

		public virtual void Play(Vector3? position = null, Transform followTarget = null)
		{
			gameObject.SetActive(true);

			if (position.HasValue)
			{
				transform.position = position.Value;
			}

			_followTarget = followTarget;

			if (_config.Clips == null || _config.Clips.Count == 0)
			{
				Debug.LogError($"AudioComponent '{name}' has a config with no clips. Cannot play.", gameObject);
				_onStop?.Invoke(); // Immediately return to pool
				return;
			}

			CancelPlay();
			_playCts = new CancellationTokenSource();
			PlayAsync(_playCts.Token).Forget();
		}

		/// <summary>
		/// Stops a playing sound (with fade-out) and returns the component to the pool.
		/// Required for stopping looping sounds.
		/// </summary>
		public void Stop()
		{
			if (!gameObject.activeInHierarchy) return;

			// Kill the play loop BEFORE fading — otherwise a mid-wait loop iteration
			// can restart the source while it is fading out.
			CancelPlay();

			_stopCts?.Cancel();
			_stopCts?.Dispose();
			_stopCts = new CancellationTokenSource();

			FadeOutStopAsync(_stopCts.Token).Forget();
		}

		private async UniTaskVoid PlayAsync(CancellationToken ct)
		{
			try
			{
				var clips = _config.Clips.Where(c => c != null).ToList();
				if (clips.Count == 0)
				{
					Debug.LogError($"AudioComponent '{name}': all clips in config '{_config.name}' are null.", gameObject);
					_onStop?.Invoke();
					return;
				}

				if (!_config.PlayAllSequentially)
				{
					clips.Shuffle();
				}

				_audioSource.volume = _config.Volume;
				_audioSource.spatialBlend = _config.IsSpatial ? 1.0f : 0.0f;
				_audioSource.loop = _config.Loop;

				if (_config.StartAfterSeconds > 0)
				{
					await DelaySeconds(_config.StartAfterSeconds, ct);
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
					await FadeInAsync(ct);

					if (_config.StopAfterSeconds > 0 && Time.time > stopAfterStamp)
					{
						await FadeOutStopAsync(ct);
						return;
					}

					// Floor at zero — a fade longer than the clip would otherwise
					// produce a negative wait and restart the clip every frame.
					float waitTillNextLoop = Mathf.Max(0f, _audioSource.clip.length - _config.FadeOutDuration);

					if (_config.LoopInterval > 0)
					{
						waitTillNextLoop = Mathf.Min(waitTillNextLoop, _config.LoopInterval);
					}

					await DelaySeconds(waitTillNextLoop, ct);
					await FadeOutAsync(ct);

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
			catch (OperationCanceledException)
			{
				// Cancelled (Stop, pool recycle, teardown) — nothing to settle here;
				// the cancelling path owns the _onStop callback.
			}
		}

		private async UniTask FadeInAsync(CancellationToken ct)
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
					await UniTask.Yield(ct);
				}
			}

			_audioSource.volume = targetVolume;
		}

		private async UniTask FadeOutAsync(CancellationToken ct)
		{
			float startVolume = _audioSource.volume;
			if (_config.FadeOutDuration > 0)
			{
				float time = 0;
				while (time < _config.FadeOutDuration)
				{
					_audioSource.volume = Mathf.Lerp(startVolume, 0, time / _config.FadeOutDuration);
					time += Time.deltaTime;
					await UniTask.Yield(ct);
				}
			}
		}

		private async UniTask FadeOutStopAsync(CancellationToken ct)
		{
			try
			{
				await FadeOutAsync(ct);
			}
			catch (OperationCanceledException)
			{
				// A newer stop/play took over — it owns the source now.
				return;
			}

			_audioSource.Stop();
			_onStop?.Invoke();
		}

		private static UniTask DelaySeconds(float seconds, CancellationToken ct)
		{
			// DeltaTime matches WaitForSeconds semantics (timescale-scaled).
			return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
		}

		private void CancelPlay()
		{
			_playCts?.Cancel();
			_playCts?.Dispose();
			_playCts = null;
		}

		private void LateUpdate()
		{
			if (_followTarget != null)
			{
				transform.position = _followTarget.position;
			}
		}

		private void OnDisable()
		{
			// Mirrors coroutine death on deactivation: pool recycle kills the play loop.
			CancelPlay();
			_stopCts?.Cancel();
		}

		private void OnDestroy()
		{
			CancelPlay();
			_playCts = null;

			_stopCts?.Cancel();
			_stopCts?.Dispose();
			_stopCts = null;
		}
	}
}
