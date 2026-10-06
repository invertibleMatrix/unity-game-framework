using System;
using System.Collections;
using System.Collections.Generic;
using AK.Core.Extensions;
using AK.Kernel.Timing;
using AK.Systems;
using AK.Systems.Animations;
using AK.Tests.Support;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AK.Tests.UI
{
	/// <summary>
	/// What a view's entrance leaves running. Its exit stops the idle loops the entrance left on
	/// the content, without reaching another view that plays the same strategy asset; a
	/// composite plays its children as one, so cutting it short stops them all.
	/// </summary>
	public class AnimationRunPlayModeTests
	{
		private const float WaitLimitSeconds = 10f;

		private sealed class ScreenA : UIView { }

		private sealed class ScreenB : UIView { }

		/// <summary>
		/// A short entrance that leaves two idle loops on the content, as breathing and pulsing
		/// strategies do: a tweener made from the content, and a sequence that targets it.
		/// </summary>
		private sealed class IdleLoopStrategy : AnimationStrategy
		{
			public Tween Breathing { get; private set; }
			public Tween Ticking   { get; private set; }

			public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time, Vector2 entryPos = default)
			{
				return DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, 1f, 0.1f)
				              .SetTarget(canvasGroup)
				              .OnComplete(() =>
				              {
					              Breathing = target.DOScale(1.05f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetTimeDomain(time).Play();
					              Ticking = DOTween.Sequence().AppendInterval(0.5f).SetLoops(-1).SetTarget(target).SetTimeDomain(time).Play();
				              })
				              .SetTimeDomain(time)
				              .Play();
			}

			public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time)
			{
				return DOTween.To(() => canvasGroup.alpha, a => canvasGroup.alpha = a, 0f, 0.3f)
				              .SetTarget(canvasGroup)
				              .SetTimeDomain(time)
				              .Play();
			}
		}

		/// <summary>An entrance and an exit of a set length that touch nothing, so only a sequence they are nested in can stop them.</summary>
		private sealed class TimedStrategy : AnimationStrategy
		{
			public float Seconds;
			public float Progress;

			/// <summary>Every tween it has started, in order.</summary>
			public List<Tween> Played { get; } = new();

			public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time, Vector2 entryPos = default) => Play(time);

			public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time) => Play(time);

			private Tween Play(TimeDomain time)
			{
				Progress = 0f;
				Tween tween = DOTween.To(() => Progress, p => Progress = p, 1f, Seconds).SetTimeDomain(time).Play();
				Played.Add(tween);
				return tween;
			}
		}

		private LiveUISystem _ui;

		[SetUp]
		public void SetUp() => _ui = new LiveUISystem();

		[TearDown]
		public void TearDown() => _ui.Dispose();

		/// <summary>Waits frame by frame until <paramref name="condition"/> holds, for at most <see cref="WaitLimitSeconds"/> of real time.</summary>
		private static IEnumerator WaitUntil(Func<bool> condition)
		{
			float limit = Time.realtimeSinceStartup + WaitLimitSeconds;
			while (!condition() && Time.realtimeSinceStartup < limit)
			{
				yield return null;
			}
		}

		private T MakeStrategy<T>() where T : AnimationStrategy
		{
			var strategy = ScriptableObject.CreateInstance<T>();
			_ui.Track(strategy);
			return strategy;
		}

		private TimedStrategy MakeTimed(float seconds)
		{
			var timed = MakeStrategy<TimedStrategy>();
			timed.Seconds = seconds;
			return timed;
		}

		private CompositeAnimationStrategy MakeComposite(params AnimationStrategy[] children)
		{
			var composite = MakeStrategy<CompositeAnimationStrategy>();
			LiveUISystem.SetField(composite, "_strategies", new List<AnimationStrategy>(children));
			return composite;
		}

		// ---------------------------------------------------------------
		// Idle loops
		// ---------------------------------------------------------------

		[UnityTest]
		public IEnumerator AnExit_StopsTheIdleLoopsTheEntranceLeftRunning()
		{
			var idle = MakeStrategy<IdleLoopStrategy>();
			_ui.SetAnimation(_ui.AddScreenPrefab<ScreenA>(), idle);

			UniTask<ScreenA> showing = _ui.System.ShowAsync<ScreenA>();
			yield return WaitUntil(() => showing.Status.IsCompleted() && idle.Ticking != null);
			ScreenA screen = showing.GetAwaiter().GetResult();

			Assert.That(idle.Breathing.IsActive() && idle.Ticking.IsActive(), Is.True, "the loops play while the screen is shown");

			UniTask closing = _ui.System.CloseAsync(screen);
			yield return WaitUntil(() => screen.State != ViewState.Shown);

			Assert.That(screen.State, Is.EqualTo(ViewState.Hiding), "the exit plays");
			Assert.That(idle.Breathing.IsActive(), Is.False, "a loop made from the content stops as the exit starts");
			Assert.That(idle.Ticking.IsActive(), Is.False, "and so does a sequence that targets it");

			yield return WaitUntil(() => closing.Status.IsCompleted());
			closing.GetAwaiter().GetResult();
		}

		[UnityTest]
		public IEnumerator ClosingAView_LeavesTheIdleLoopOfAnotherViewOnTheSameAsset()
		{
			var pulse = MakeStrategy<PulseAnimationStrategy>();
			LiveUISystem.SetField(pulse, "_continuousPulse", true);
			LiveUISystem.SetField(pulse, "_showPulses", 0);
			LiveUISystem.SetField(pulse, "_hidePulses", 0);

			_ui.SetAnimation(_ui.AddScreenPrefab<ScreenA>(), pulse);
			_ui.SetAnimation(_ui.AddScreenPrefab<ScreenB>(), pulse);

			UniTask<ScreenA> showingA = _ui.System.ShowAsync<ScreenA>();
			UniTask<ScreenB> showingB = _ui.System.ShowAsync<ScreenB>();
			yield return WaitUntil(() => showingA.Status.IsCompleted() && showingB.Status.IsCompleted());
			ScreenA a = showingA.GetAwaiter().GetResult();
			ScreenB b = showingB.GetAwaiter().GetResult();

			_ui.System.Close(a);

			Vector3 before = b.RectTransform.localScale;
			float until = Time.realtimeSinceStartup + 0.2f;
			yield return WaitUntil(() => Time.realtimeSinceStartup >= until);

			Assert.That(b.RectTransform.localScale, Is.Not.EqualTo(before), "the other view still pulses");
		}

		// ---------------------------------------------------------------
		// Composite
		// ---------------------------------------------------------------

		[UnityTest]
		public IEnumerator ACompositeEntrance_EndsWithItsLongestChild()
		{
			TimedStrategy brief  = MakeTimed(0.1f);
			TimedStrategy longer = MakeTimed(0.4f);
			_ui.SetAnimation(_ui.AddScreenPrefab<ScreenA>(), MakeComposite(brief, longer));

			UniTask<ScreenA> showing = _ui.System.ShowAsync<ScreenA>();
			yield return WaitUntil(() => showing.Status.IsCompleted());

			Assert.That(showing.GetAwaiter().GetResult().State, Is.EqualTo(ViewState.Shown));
			Assert.That(brief.Progress, Is.EqualTo(1f), "the shorter child ran to its end");
			Assert.That(longer.Progress, Is.EqualTo(1f), "and the entrance waited for the longer one");
		}

		[UnityTest]
		public IEnumerator CuttingACompositeEntranceShort_StopsEveryChild()
		{
			TimedStrategy brief  = MakeTimed(5f);
			TimedStrategy longer = MakeTimed(10f);
			_ui.SetAnimation(_ui.AddScreenPrefab<ScreenA>(), MakeComposite(brief, longer));

			ScreenA screen = _ui.System.Show<ScreenA>();
			yield return null;

			Assert.That(screen.State, Is.EqualTo(ViewState.Showing));
			Tween briefEntrance  = brief.Played[0];
			Tween longerEntrance = longer.Played[0];
			Assert.That(briefEntrance.IsActive() && longerEntrance.IsActive(), Is.True, "both children play");

			_ui.System.Close(screen, CloseOptions.Now);
			yield return null;

			Assert.That(briefEntrance.IsActive(), Is.False, "the shorter child stops with the entrance");
			Assert.That(longerEntrance.IsActive(), Is.False, "and so does the longer one");
		}
	}
}
