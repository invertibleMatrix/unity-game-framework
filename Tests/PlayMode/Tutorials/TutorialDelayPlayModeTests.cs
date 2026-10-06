using System;
using System.Collections;
using System.Threading;
using AK.Tutorials;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AK.Tests.Tutorials
{
	/// <summary>
	/// Tutorial waits run on unscaled time, so a game paused at timeScale 0 can't freeze a step
	/// that is holding input. Every step wait goes through the same helper as the start delay.
	/// </summary>
	public class TutorialDelayPlayModeTests
	{
		private const float StartDelaySeconds = 0.25f;
		private const float WaitLimitSeconds  = 10f;

		/// <summary>A step that only waits out its start delay, as every step does first.</summary>
		private sealed class DelayOnlyStep : TutorialStep { }

		private DelayOnlyStep _step;
		private float         _timeScale;

		[SetUp]
		public void SetUp()
		{
			_timeScale = Time.timeScale;
			Time.timeScale = 0f;

			_step = ScriptableObject.CreateInstance<DelayOnlyStep>();
			_step.StartDelay = StartDelaySeconds;
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_step);
			Time.timeScale = _timeScale;
		}

		[UnityTest]
		public IEnumerator AtTimeScaleZero_AStepsStartDelay_Ends()
		{
			float startedAt = Time.realtimeSinceStartup;
			UniTask present = _step.PresentAsync(default, CancellationToken.None);

			float limit = startedAt + WaitLimitSeconds;
			while (!present.Status.IsCompleted() && Time.realtimeSinceStartup < limit)
			{
				yield return null;
			}

			float waited = Time.realtimeSinceStartup - startedAt;

			Assert.That(present.Status, Is.EqualTo(UniTaskStatus.Succeeded), "the delay ended although the game is paused");
			present.GetAwaiter().GetResult();
			Assert.That(waited, Is.GreaterThan(StartDelaySeconds / 2f), "and it was waited out, not skipped");
		}

		[UnityTest]
		public IEnumerator CancellingAStepsStartDelay_EndsItAtOnce()
		{
			using var cts = new CancellationTokenSource();
			UniTask present = _step.PresentAsync(default, cts.Token);

			cts.Cancel();

			Assert.That(present.Status, Is.EqualTo(UniTaskStatus.Canceled), "no frame has to pass");
			Assert.Catch<OperationCanceledException>(() => present.GetAwaiter().GetResult());
			yield break;
		}
	}
}
