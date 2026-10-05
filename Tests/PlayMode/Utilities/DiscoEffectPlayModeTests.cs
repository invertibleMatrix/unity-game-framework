using System;
using System.Collections;
using System.Reflection;
using AK.Kernel.Timing;
using AK.Utilities;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AK.Tests.Utilities
{
	/// <summary>The disco effect's tweens: one effect at a time per sprite, none left once it ends or its component goes.</summary>
	public class DiscoEffectPlayModeTests
	{
		private const float WaitLimitSeconds = 10f;

		private SpriteRenderer _sprite;
		private DiscoEffect    _effect;

		[SetUp]
		public void SetUp()
		{
			_sprite = new GameObject("Sprite").AddComponent<SpriteRenderer>();
			_sprite.color = Color.green;

			// On an object of its own, so the sprite outlives the component.
			_effect = new GameObject("Disco").AddComponent<DiscoEffect>();
			Set("_spriteRenderer", _sprite);
			Set("_effectDuration", 0.2f);
			Set("_endTransitionTime", 0.1f);
			Set("_timeDomain", TimeDomain.Unscaled);
		}

		[TearDown]
		public void TearDown()
		{
			if (_effect != null) Object.Destroy(_effect.gameObject);
			if (_sprite != null)
			{
				DOTween.Kill(_sprite);
				Object.Destroy(_sprite.gameObject);
			}
		}

		private void Set(string field, object value)
		{
			typeof(DiscoEffect).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_effect, value);
		}

		private int TweensOnTheSprite => DOTween.TweensByTarget(_sprite)?.Count ?? 0;

		private static IEnumerator WaitUntil(Func<bool> condition)
		{
			float limit = Time.realtimeSinceStartup + WaitLimitSeconds;
			while (!condition() && Time.realtimeSinceStartup < limit)
			{
				yield return null;
			}
		}

		[Test]
		public void PlayingAgain_RestartsTheEffect_InsteadOfAddingOne()
		{
			int before = DOTween.TotalActiveTweens();

			_effect.PlayDiscoEffect();
			_effect.PlayDiscoEffect();

			Assert.AreEqual(1, DOTween.TotalActiveTweens() - before, "no tween runs on beside the effect's own");
			Assert.AreEqual(1, TweensOnTheSprite);
		}

		[UnityTest]
		public IEnumerator TheEffect_FadesOut_ThenDisablesTheSprite_WithItsColorBack()
		{
			_effect.PlayDiscoEffect();
			yield return null;
			Assert.AreNotEqual(Color.green, _sprite.color, "cycling through the colors");

			yield return WaitUntil(() => !_sprite.enabled);

			Assert.IsFalse(_sprite.enabled);
			Assert.AreEqual(Color.green, _sprite.color);
			Assert.AreEqual(0, TweensOnTheSprite, "nothing left tweening the sprite");
		}

		[UnityTest]
		public IEnumerator DestroyingTheComponent_EndsItsEffect_AndLeavesTheSpritesOtherTweens()
		{
			// Started after the effect, which ends every tween on the sprite as it starts.
			_effect.PlayDiscoEffect();
			Tween other = _sprite.transform.DOMove(Vector3.one, 10f).SetTarget(_sprite).Play();

			Object.Destroy(_effect.gameObject);
			yield return null;

			Assert.AreEqual(1, TweensOnTheSprite);
			Assert.IsTrue(other.IsActive(), "the sprite's other tween runs on");
		}
	}
}
