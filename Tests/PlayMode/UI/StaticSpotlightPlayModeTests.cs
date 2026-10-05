using System;
using System.Collections;
using System.Collections.Generic;
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
	/// A spotlight placed in a view as a static child is shown again rather than twinned, and
	/// each show's intro plays with that show's settings. Re-shown while it is up, the intro
	/// starts over with the new duration; re-shown while it closes, the intro plays once it is
	/// back. The show's onInit gives it its targets before the show sets its context.
	/// </summary>
	public class StaticSpotlightPlayModeTests
	{
		private const float FirstIntroSeconds  = 0.1f;
		private const float SecondIntroSeconds = 5f;
		private const float WaitLimitSeconds   = 10f;

		private sealed class Host : UIView { }

		private LiveUISystem _ui;
		private Host         _host;

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

		private UIViewSpotlight ShowSpotlight(float introSeconds)
		{
			return _ui.System.Show<UIViewSpotlight>(
				ShowOptions.Under(_host, new UIViewSpotlightContext { IntroDuration = introSeconds }),
				spotlight => spotlight.SetTargets(Array.Empty<RectTransform>()));
		}

		/// <summary>The intro the spotlight is playing; there must be exactly one.</summary>
		private static Tween Intro(UIViewSpotlight spotlight)
		{
			List<Tween> tweens = DOTween.TweensByTarget(spotlight);
			Assert.That(tweens, Is.Not.Null.And.Count.EqualTo(1), "one intro plays");
			return tweens[0];
		}

		[UnityTest]
		public IEnumerator ReShownWhileShown_ItsIntroStartsOverWithTheNewShowsDuration()
		{
			_ui.AddStaticChild<UIViewSpotlight>(_ui.AddScreenPrefab<Host>(), showOnStart: false);
			_host = _ui.System.Show<Host>();

			UIViewSpotlight spotlight = ShowSpotlight(FirstIntroSeconds);
			Assert.That(spotlight.State, Is.EqualTo(ViewState.Shown));
			yield return null;

			Assert.That(ShowSpotlight(SecondIntroSeconds), Is.SameAs(spotlight), "a static view is shown again, never twinned");
			Assert.That(spotlight.IsIntroPlaying, Is.True);
			Assert.That(Intro(spotlight).Duration(), Is.EqualTo(SecondIntroSeconds));
		}

		[UnityTest]
		public IEnumerator ReShownWhileClosing_ItsIntroPlaysOnceItIsBack()
		{
			var animation = ScriptableObject.CreateInstance<ScaleAnimationStrategy>();
			_ui.Track(animation);

			Host prefab = _ui.AddScreenPrefab<Host>();
			_ui.SetAnimation(_ui.AddStaticChild<UIViewSpotlight>(prefab, showOnStart: false), animation);
			_host = _ui.System.Show<Host>();

			UIViewSpotlight spotlight = ShowSpotlight(FirstIntroSeconds);
			yield return WaitUntil(() => spotlight.State == ViewState.Shown);

			UniTask closing = _ui.System.CloseAsync(spotlight);
			yield return WaitUntil(() => spotlight.State != ViewState.Shown);
			Assert.That(spotlight.State, Is.EqualTo(ViewState.Hiding), "its exit plays");

			ShowSpotlight(SecondIntroSeconds);
			yield return WaitUntil(() => closing.Status.IsCompleted() && spotlight.State is ViewState.Showing or ViewState.Shown);

			Assert.That(spotlight.State is ViewState.Showing or ViewState.Shown, Is.True, "it is shown again once the close ends");
			Assert.That(spotlight.IsIntroPlaying, Is.True, "with an intro");
			Assert.That(Intro(spotlight).Duration(), Is.EqualTo(SecondIntroSeconds), "that plays with the new show's duration");
		}
	}
}
