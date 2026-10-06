using System;
using System.Collections;
using AK.Core.Extensions;
using AK.Kernel.Timing;
using AK.Services.Ads;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Timer = AK.Utilities.Timer;

namespace AK.Tests.Utilities
{
	/// <summary>
	/// The time domains in a running game paused at timeScale 0: unscaled waits, frame deltas,
	/// tweens and timers run on; scaled ones hold until the game runs.
	/// </summary>
	public class TimeDomainPlayModeTests
	{
		private const double WaitSeconds      = 0.2d;
		private const float  WaitLimitSeconds = 10f;

		private float _timeScale;
		private bool  _independentByDefault;

		[SetUp]
		public void SetUp()
		{
			_timeScale = Time.timeScale;
			_independentByDefault = DOTween.defaultTimeScaleIndependent;
			Time.timeScale = 0f;
		}

		[TearDown]
		public void TearDown()
		{
			DOTween.defaultTimeScaleIndependent = _independentByDefault;
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
		public IEnumerator AnUnscaledDelay_Ends_WhileAScaledOneHolds()
		{
			float startedAt = Time.realtimeSinceStartup;
			UniTask unscaled = TimeDomain.Unscaled.Delay(WaitSeconds);
			UniTask scaled = TimeDomain.Scaled.Delay(WaitSeconds);

			yield return WaitUntil(() => unscaled.Status.IsCompleted());

			Assert.That(unscaled.Status, Is.EqualTo(UniTaskStatus.Succeeded));
			unscaled.GetAwaiter().GetResult();
			Assert.That(Time.realtimeSinceStartup - startedAt, Is.GreaterThan(WaitSeconds * 0.9d), "it was waited out, not skipped");
			Assert.That(scaled.Status, Is.EqualTo(UniTaskStatus.Pending), "game time is paused");

			Time.timeScale = 1f;
			yield return WaitUntil(() => scaled.Status.IsCompleted());

			Assert.That(scaled.Status, Is.EqualTo(UniTaskStatus.Succeeded), "and runs once the game does");
			scaled.GetAwaiter().GetResult();
		}

		[UnityTest]
		public IEnumerator TheAdsClock_WaitsOnUnscaledTime()
		{
			UniTask delay = new ForegroundAdsClock().Delay(WaitSeconds);

			yield return WaitUntil(() => delay.Status.IsCompleted());

			Assert.That(delay.Status, Is.EqualTo(UniTaskStatus.Succeeded), "an ad watchdog runs out although the game is paused");
			delay.GetAwaiter().GetResult();
		}

		[UnityTest]
		public IEnumerator FrameDeltas_FollowTheirDomain()
		{
			yield return null;
			yield return null;

			Assert.That(TimeDomain.Scaled.DeltaTime(), Is.Zero);
			Assert.That(TimeDomain.Unscaled.DeltaTime(), Is.GreaterThan(0f).And.LessThanOrEqualTo((float)ForegroundTime.MaxFrameSeconds));
		}

		[UnityTest]
		public IEnumerator Tweens_FollowTheirDomain_WhenDOTweensDefaultIsScaled()
		{
			DOTween.defaultTimeScaleIndependent = false;
			yield return TweensFollowTheirDomain();
		}

		[UnityTest]
		public IEnumerator Tweens_FollowTheirDomain_WhenDOTweensDefaultIsUnscaled()
		{
			DOTween.defaultTimeScaleIndependent = true;
			yield return TweensFollowTheirDomain();
		}

		private static IEnumerator TweensFollowTheirDomain()
		{
			float unscaledValue = 0f;
			float scaledValue = 0f;
			Tween unscaled = DOTween.To(() => unscaledValue, v => unscaledValue = v, 1f, (float)WaitSeconds).SetTimeDomain(TimeDomain.Unscaled).Play();
			Tween scaled = DOTween.To(() => scaledValue, v => scaledValue = v, 1f, (float)WaitSeconds).SetTimeDomain(TimeDomain.Scaled).Play();

			try
			{
				yield return WaitUntil(() => !unscaled.IsActive());

				Assert.That(unscaledValue, Is.EqualTo(1f), "the unscaled tween played out");
				Assert.That(scaledValue, Is.Zero, "the scaled tween holds while the game is paused");
			}
			finally
			{
				unscaled.Kill();
				scaled.Kill();
			}
		}

		[UnityTest]
		public IEnumerator AnUnscaledTimer_Completes_WhileAScaledOneHolds()
		{
			using var unscaled = new Timer();
			using var scaled = new Timer();
			bool unscaledDone = false;
			bool scaledDone = false;
			unscaled.StartCountdown(TimeSpan.FromSeconds(WaitSeconds), onComplete: () => unscaledDone = true, timeBase: TimeBase.Unscaled);
			scaled.StartCountdown(TimeSpan.FromSeconds(WaitSeconds), onComplete: () => scaledDone = true);

			yield return WaitUntil(() => unscaledDone);

			Assert.That(unscaledDone, Is.True, "the unscaled countdown completed although the game is paused");
			Assert.That(scaledDone, Is.False, "game time is paused");
		}
	}
}
