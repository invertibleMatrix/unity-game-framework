using System;
using System.Collections;
using System.Reflection;
using AK.Utilities.Particles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AK.Tests.Utilities
{
	/// <summary>
	/// A particle effect in a running game paused at timeScale 0 waits out its start delay on
	/// the time its root ParticleSystem simulates on: unscaled for a UI effect, scaled for a
	/// gameplay one. A looping UI effect stops at its stop-after time and is recycled.
	/// </summary>
	public class ParticleTimePlayModeTests
	{
		private const float StartDelaySeconds = 0.2f;
		private const float WaitLimitSeconds  = 10f;

		private ParticleConfigBase _config;
		private ParticleComponent  _effect;
		private float              _timeScale;

		[SetUp]
		public void SetUp()
		{
			_timeScale = Time.timeScale;
			Time.timeScale = 0f;

			_config = ScriptableObject.CreateInstance<ParticleConfigBase>();
			_config.StartDelayInSeconds = StartDelaySeconds;
		}

		[TearDown]
		public void TearDown()
		{
			if (_effect != null) Object.DestroyImmediate(_effect.gameObject);
			Object.DestroyImmediate(_config);
			Time.timeScale = _timeScale;
		}

		/// <summary>An effect prefab-like and inactive, its root system simulating on unscaled time or not.</summary>
		private static ParticleComponent MakeEffect(bool unscaledTime)
		{
			var go = new GameObject("Effect");
			go.SetActive(false);

			var system = go.AddComponent<ParticleSystem>();
			ParticleSystem.MainModule main = system.main;
			main.playOnAwake = false;
			main.useUnscaledTime = unscaledTime;

			var effect = go.AddComponent<ParticleComponent>();
			typeof(ParticleComponent).GetField("_rootParticle", BindingFlags.Instance | BindingFlags.NonPublic)
			                         .SetValue(effect, system);
			return effect;
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
		public IEnumerator AUIEffect_WaitsOutItsStartDelay_WhileTheGameIsPaused()
		{
			_effect = MakeEffect(unscaledTime: true);
			_effect.Init(_config, null);
			_effect.Show();

			yield return WaitUntil(() => _effect.gameObject.activeSelf);

			Assert.That(_effect.gameObject.activeSelf, Is.True, "the start delay ran out");
			Assert.That(_effect.RootParticle.isPlaying, Is.True);
		}

		[UnityTest]
		public IEnumerator ALoopingEffect_StopsAtItsStopAfterTime_AndIsRecycled()
		{
			_effect = MakeEffect(unscaledTime: true);
			ParticleSystem.MainModule main = _effect.RootParticle.main;
			main.loop          = true;
			main.duration      = 1f;
			main.startLifetime = 0.1f;

			_config.StartDelayInSeconds = 0f;
			_config.StopAfterSeconds    = 0.3f;

			bool recycled = false;
			_effect.Init(_config, () => recycled = true);
			_effect.Show();

			yield return WaitUntil(() => recycled);

			Assert.That(recycled, Is.True, "it stopped emitting at its stop-after time, and was recycled once its particles died");
		}

		[UnityTest]
		public IEnumerator AGameplayEffect_HoldsItsStartDelay_WithTheGame()
		{
			_effect = MakeEffect(unscaledTime: false);
			_effect.Init(_config, null);
			_effect.Show();

			float heldUntil = Time.realtimeSinceStartup + StartDelaySeconds * 3f;
			yield return WaitUntil(() => Time.realtimeSinceStartup >= heldUntil);
			Assert.That(_effect.gameObject.activeSelf, Is.False, "the start delay holds while the game is paused");

			Time.timeScale = 1f;
			yield return WaitUntil(() => _effect.gameObject.activeSelf);
			Assert.That(_effect.gameObject.activeSelf, Is.True, "and runs out once the game runs");
		}
	}
}
