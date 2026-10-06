using System;
using System.Collections;
using System.Collections.Generic;
using AK.Kernel.Timing;
using AK.Utilities.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AK.Tests.Utilities
{
	/// <summary>
	/// A sound in a running game paused at timeScale 0. On unscaled time, the audio default, it
	/// waits out its start delay, fades in, then fades out and stops. On scaled time its start
	/// delay holds with the game.
	/// </summary>
	public class AudioTimePlayModeTests
	{
		private const float StartDelaySeconds = 0.2f;
		private const float FadeSeconds       = 0.2f;
		private const float Volume            = 0.5f;
		private const float WaitLimitSeconds  = 10f;

		/// <summary>Takes its own source, as the Inspector wires it through OnValidate.</summary>
		private sealed class TestSound : AudioComponent
		{
			private void Awake() => _audioSource = GetComponent<AudioSource>();
		}

		private AudioClip   _clip;
		private AudioConfig _config;
		private TestSound   _sound;
		private AudioSource _source;
		private bool        _stopped;
		private float       _timeScale;

		[SetUp]
		public void SetUp()
		{
			_timeScale = Time.timeScale;
			Time.timeScale = 0f;

			_clip = AudioClip.Create("Silence", 44100, 1, 44100, false);

			_config = ScriptableObject.CreateInstance<AudioConfig>();
			_config.Clips             = new List<AudioClip> { _clip };
			_config.Volume            = Volume;
			_config.PitchRange        = Vector2.one;
			_config.StartAfterSeconds = StartDelaySeconds;
			_config.FadeInDuration    = FadeSeconds;
			_config.FadeOutDuration   = FadeSeconds;
			_config.Loop              = true;
			_config.IsSpatial         = false;

			_sound = new GameObject("Sound").AddComponent<TestSound>();
			_source = _sound.GetComponent<AudioSource>();
			_source.playOnAwake = false;
			_stopped = false;
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_sound.gameObject);
			Object.DestroyImmediate(_config);
			Object.DestroyImmediate(_clip);
			Time.timeScale = _timeScale;
		}

		/// <summary>Waits frame by frame until <paramref name="condition"/> holds, for at most <see cref="WaitLimitSeconds"/> of real time.</summary>
		private static IEnumerator WaitUntil(Func<bool> condition)
		{
			float limit = Time.realtimeSinceStartup + WaitLimitSeconds;
			while (!condition() && Time.realtimeSinceStartup < limit)
			{
				yield return null;
			}
		}

		[UnityTest]
		public IEnumerator OnUnscaledTime_ASound_StartsFadesInAndStops_WhileTheGameIsPaused()
		{
			_sound.Init(_config, TimeDomain.Unscaled, () => _stopped = true);
			_sound.Play();

			yield return WaitUntil(() => _source.clip != null);
			Assert.That(_source.clip, Is.SameAs(_clip), "the start delay ran out");

			yield return WaitUntil(() => _source.volume >= Volume);
			Assert.That(_source.volume, Is.EqualTo(Volume), "the fade-in played out");

			_sound.Stop();
			yield return WaitUntil(() => _stopped);
			Assert.That(_stopped, Is.True, "the fade-out played out and the sound stopped");
		}

		[UnityTest]
		public IEnumerator OnScaledTime_ASoundsStartDelay_HoldsWithTheGame()
		{
			_sound.Init(_config, TimeDomain.Scaled, () => _stopped = true);
			_sound.Play();

			float heldUntil = Time.realtimeSinceStartup + StartDelaySeconds * 3f;
			yield return WaitUntil(() => Time.realtimeSinceStartup >= heldUntil);
			Assert.That(_source.clip, Is.Null, "the start delay holds while the game is paused");

			Time.timeScale = 1f;
			yield return WaitUntil(() => _source.clip != null);
			Assert.That(_source.clip, Is.SameAs(_clip), "and runs out once the game runs");
		}
	}
}
