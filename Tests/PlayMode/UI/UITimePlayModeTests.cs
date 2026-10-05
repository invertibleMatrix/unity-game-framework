using System;
using System.Collections;
using AK.Kernel.Timing;
using AK.Systems;
using AK.Systems.Animations;
using AK.Tests.Support;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AK.Tests.UI
{
	/// <summary>
	/// The UI in a running game paused at timeScale 0. On the system's default unscaled time, a
	/// screen with a tweened entrance and exit, a background dim and a delayed static child
	/// shows and closes. A system set to scaled time holds its screens with the game.
	/// </summary>
	public class UITimePlayModeTests
	{
		private const float ChildDelaySeconds = 0.2f;
		private const float WaitLimitSeconds  = 10f;

		private sealed class DimmedScreen : UIView { }

		private sealed class DelayedChild : UIView { }

		private LiveUISystem           _ui;
		private ScaleAnimationStrategy _animation;
		private float                  _timeScale;

		[SetUp]
		public void SetUp()
		{
			_timeScale = Time.timeScale;
			Time.timeScale = 0f;

			_ui = new LiveUISystem();
			_animation = ScriptableObject.CreateInstance<ScaleAnimationStrategy>();
		}

		[TearDown]
		public void TearDown()
		{
			_ui.Dispose();
			Object.DestroyImmediate(_animation);
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
		public IEnumerator AtTimeScaleZero_AScreen_ShowsAndCloses()
		{
			var prefab = _ui.AddScreenPrefab<DimmedScreen>();
			prefab.gameObject.AddComponent<ViewBackgroundOverlay>();
			_ui.SetAnimation(prefab, _animation);
			_ui.AddStaticChild<DelayedChild>(prefab, showOnStart: true, showDelay: ChildDelaySeconds);

			UniTask<DimmedScreen> showing = _ui.System.ShowAsync<DimmedScreen>();
			yield return WaitUntil(() => showing.Status.IsCompleted());

			Assert.That(showing.Status, Is.EqualTo(UniTaskStatus.Succeeded), "the entrance played out");
			DimmedScreen screen = showing.GetAwaiter().GetResult();
			Assert.That(screen.State, Is.EqualTo(ViewState.Shown));

			var child = screen.GetComponentInChildren<DelayedChild>(true);
			yield return WaitUntil(() => child.State == ViewState.Shown);
			Assert.That(child.State, Is.EqualTo(ViewState.Shown), "the static child's delay ran out");

			var dim = screen.transform.Find("ViewBackground").GetComponent<Image>();
			yield return WaitUntil(() => dim.color.a >= ViewBackgroundOverlay.DefaultAlpha);
			Assert.That(dim.color.a, Is.EqualTo(ViewBackgroundOverlay.DefaultAlpha).Within(1e-3f), "the dim faded in");

			UniTask closing = _ui.System.CloseAsync(screen);
			yield return WaitUntil(() => closing.Status.IsCompleted());

			Assert.That(closing.Status, Is.EqualTo(UniTaskStatus.Succeeded), "the exit played out");
			closing.GetAwaiter().GetResult();
		}

		[UnityTest]
		public IEnumerator AtTimeScaleZero_ASystemOnScaledTime_HoldsItsScreens_UntilTheGameRuns()
		{
			_ui.SetTimeDomain(TimeDomain.Scaled);
			var prefab = _ui.AddScreenPrefab<DimmedScreen>();
			_ui.SetAnimation(prefab, _animation);

			UniTask<DimmedScreen> showing = _ui.System.ShowAsync<DimmedScreen>();

			float heldUntil = Time.realtimeSinceStartup + 0.5f;
			yield return WaitUntil(() => Time.realtimeSinceStartup >= heldUntil);
			Assert.That(showing.Status, Is.EqualTo(UniTaskStatus.Pending), "the entrance holds while the game is paused");

			Time.timeScale = 1f;
			yield return WaitUntil(() => showing.Status.IsCompleted());

			Assert.That(showing.Status, Is.EqualTo(UniTaskStatus.Succeeded), "and plays out once it runs");
			Assert.That(showing.GetAwaiter().GetResult().State, Is.EqualTo(ViewState.Shown));
		}
	}
}
