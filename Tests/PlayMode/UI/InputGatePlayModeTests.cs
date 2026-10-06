using System;
using System.Collections;
using System.Collections.Generic;
using AK.Systems;
using AK.Tests.Support;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AK.Tests.UI
{
	/// <summary>
	/// The input gate in a running game paused at timeScale 0. The spotlight's intro plays out
	/// and gives input back; an intro that can't finish is ended by the gate; and while input is
	/// held, the blocker is the first thing every pointer hits.
	/// </summary>
	public class InputGatePlayModeTests
	{
		private const float IntroSeconds     = 0.3f;
		private const float WaitLimitSeconds = 10f;

		private LiveUISystem _ui;
		private LogRecorder  _log;
		private float        _timeScale;

		private UIInputGate Gate => _ui.System.InputGate;

		[SetUp]
		public void SetUp()
		{
			_timeScale = Time.timeScale;
			Time.timeScale = 0f;

			_log = new LogRecorder();
			_ui = new LiveUISystem();
			_ui.AddScreenPrefab<UIViewSpotlight>();
		}

		[TearDown]
		public void TearDown()
		{
			_ui.Dispose();
			_log.Dispose();
			Time.timeScale = _timeScale;
		}

		private UIViewSpotlight ShowSpotlight()
		{
			return _ui.System.Show<UIViewSpotlight>(
				ShowOptions.With(new UIViewSpotlightContext { IntroDuration = IntroSeconds }),
				spotlight => spotlight.SetTargets(Array.Empty<RectTransform>()));
		}

		/// <summary>Waits frame by frame while <paramref name="condition"/> holds, for at most <see cref="WaitLimitSeconds"/> of real time.</summary>
		private static IEnumerator WaitWhile(Func<bool> condition)
		{
			float limit = Time.realtimeSinceStartup + WaitLimitSeconds;
			while (condition() && Time.realtimeSinceStartup < limit)
			{
				yield return null;
			}
		}

		// ---------------------------------------------------------------
		// Spotlight
		// ---------------------------------------------------------------

		[UnityTest]
		public IEnumerator AtTimeScaleZero_TheSpotlightIntro_PlaysOut_AndGivesInputBack()
		{
			UIViewSpotlight spotlight = ShowSpotlight();
			float shownAt = Time.realtimeSinceStartup;

			Assert.That(spotlight.IsIntroPlaying, Is.True);
			Assert.That(Gate.IsHeld, Is.True, "input is held while the intro plays");

			yield return WaitWhile(() => Gate.IsHeld);
			float heldFor = Time.realtimeSinceStartup - shownAt;

			Assert.That(Gate.IsHeld, Is.False, "input comes back although the game is paused");
			Assert.That(spotlight.IsIntroPlaying, Is.False, "the holes are open");
			Assert.That(heldFor, Is.LessThan(IntroSeconds + UIViewSpotlight.IntroHoldMargin),
				"the intro's end gave input back, not the gate's backstop");
			Assert.That(_log.Count(LogType.Warning, "timed out"), Is.EqualTo(0));
		}

		[UnityTest]
		public IEnumerator AnIntroRequestedInTheShowsInit_PlaysWithThatShowsDuration()
		{
			// SetTargets runs in the show's onInit, before the show sets its context. Started
			// there, the intro would play with the view's serialized duration instead.
			UIViewSpotlight spotlight = ShowSpotlight();

			List<Tween> tweens = DOTween.TweensByTarget(spotlight);

			Assert.That(tweens, Is.Not.Null.And.Count.EqualTo(1), "the intro is the spotlight's one tween");
			Assert.That(tweens[0].Duration(), Is.EqualTo(IntroSeconds));
			yield break;
		}

		[UnityTest]
		public IEnumerator AnIntroThatCannotFinish_IsEndedByTheGate_AndTheHolesSnapOpen()
		{
			UIViewSpotlight spotlight = ShowSpotlight();
			DOTween.Pause(spotlight); // the intro tween targets its spotlight: paused, it never ends

			yield return WaitWhile(() => Gate.IsHeld);

			Assert.That(Gate.IsHeld, Is.False, "the gate gave input back");
			Assert.That(_log.Count(LogType.Warning, "'UIViewSpotlight intro' timed out"), Is.EqualTo(1));

			yield return null; // the spotlight's next Update sees that its hold is gone

			Assert.That(spotlight.IsIntroPlaying, Is.False, "the holes open with the input");
		}

		// ---------------------------------------------------------------
		// Blocker and EventSystem
		// ---------------------------------------------------------------

		[UnityTest]
		public IEnumerator WhileInputIsHeld_EveryPointerHitsTheBlockerFirst_EvenOverTheTopmostCanvas()
		{
			Image image = MakeFullScreenImage(short.MaxValue);
			yield return null; // the canvas lays out and its raycaster registers

			EventSystem events = EventSystem.current;
			Assert.That(events, Is.Not.Null, "the UISystem made the scene's EventSystem");

			var pointer = new PointerEventData(events) { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) };
			var hits = new List<RaycastResult>();

			events.RaycastAll(pointer, hits);
			Assert.That(hits.Count, Is.GreaterThan(0));
			Assert.That(hits[0].gameObject, Is.SameAs(image.gameObject), "while input is free, a press reaches the UI");

			InputHold hold = Gate.Hold("test");
			events.RaycastAll(pointer, hits);
			hold.Release();

			Assert.That(hits[0].module, Is.InstanceOf<UIInputBlocker>(), "while input is held, the blocker ranks first");
			Assert.That(hits.Exists(hit => hit.gameObject == image.gameObject), Is.True, "the UI is still found, ranked after it");

			events.RaycastAll(pointer, hits);
			Assert.That(hits[0].gameObject, Is.SameAs(image.gameObject), "once released, a press reaches the UI again");
		}

		[UnityTest]
		public IEnumerator WhileInputIsHeld_APointerOverTheWorld_ReadsAsOverUI()
		{
			// Game code keeps world input off the UI by asking the EventSystem for any hit under
			// the pointer, as Extra Life's camera pan and meeple taps do. Over the world there is none.
			yield return null;

			EventSystem events = EventSystem.current;
			var pointer = new PointerEventData(events) { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) };
			var hits = new List<RaycastResult>();

			events.RaycastAll(pointer, hits);
			Assert.That(hits, Is.Empty, "while input is free, the world is under the pointer");

			using (Gate.Hold("test"))
			{
				events.RaycastAll(pointer, hits);
				Assert.That(hits.Count, Is.EqualTo(1), "while input is held, the world reads as covered");
			}

			events.RaycastAll(pointer, hits);
			Assert.That(hits, Is.Empty);
		}

		[UnityTest]
		public IEnumerator TheEventSystemItMakes_ReadsInputThroughTheProjectsInputHandling()
		{
			yield return null; // a frame of input processing: the legacy module throws here when only the Input System is enabled

			BaseInputModule module = EventSystem.current.GetComponent<BaseInputModule>();

#if UGFW_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
			Assert.That(module, Is.InstanceOf<UnityEngine.InputSystem.UI.InputSystemUIInputModule>());
#else
			Assert.That(module, Is.InstanceOf<StandaloneInputModule>());
#endif
		}

		private Image MakeFullScreenImage(int sortingOrder)
		{
			var canvasGo = new GameObject("TopCanvas", typeof(RectTransform));
			_ui.Track(canvasGo);

			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = sortingOrder;
			canvasGo.AddComponent<GraphicRaycaster>();

			var imageGo = new GameObject("Image", typeof(RectTransform));
			var rect = (RectTransform)imageGo.transform;
			rect.SetParent(canvasGo.transform, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			return imageGo.AddComponent<Image>();
		}
	}
}
