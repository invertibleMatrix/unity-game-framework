using System.Text.RegularExpressions;
using AK.Systems;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AK.Tests
{
	/// <summary>
	/// The view's lifecycle state machine: which hooks fire when an entrance or exit is
	/// interrupted, and that teardown is idempotent. A <see cref="HoldAnimation"/> keeps a
	/// show or hide in flight so the Showing and Hiding states can be observed.
	/// </summary>
	public class ViewStateTests
	{
		private UISystemHarness _h;

		[SetUp]    public void SetUp()    => _h = new UISystemHarness();
		[TearDown] public void TearDown() => _h.Dispose();

		private RecordingScreen ShowWithHold(out HoldAnimation hold)
		{
			var prefab = _h.MakePrefab<RecordingScreen>(screen: true);
			_h.AddHoldAnimation(prefab);

			var view = _h.System.Show<RecordingScreen>();
			hold = view.GetComponent<HoldAnimation>();
			return view;
		}

		[Test]
		public void State_FollowsTheLifecycle()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>(screen: true, behaviour: ViewStackBehaviour.HideBelow);

			var view = _h.System.Show<RecordingScreen>();
			Assert.That(view.State, Is.EqualTo(ViewState.Shown));
			Assert.That(view.IsVisible, Is.True);

			var top = _h.System.Show<RecordingFragment>();
			Assert.That(view.State, Is.EqualTo(ViewState.Paused));
			Assert.That(view.IsVisible, Is.False, "HideBelow hides the paused view");

			_h.System.Close(top);
			Assert.That(view.State, Is.EqualTo(ViewState.Shown));
			Assert.That(view.IsVisible, Is.True);
		}

		[Test]
		public void StaticChildren_AreHiddenAndStayRegistered_WhenTheirStaticHostCloses()
		{
			var screen = _h.MakePrefab<RecordingScreen>(screen: true);
			var host = _h.AddStaticChild<RecordingFragment>(screen, showOnStart: true);
			_h.AddStaticChild<RecordingFragmentB>(host, showOnStart: true);

			var shownScreen = _h.System.Show<RecordingScreen>();
			var shownHost = shownScreen.GetComponentInChildren<RecordingFragment>(true);
			var child = shownScreen.GetComponentInChildren<RecordingFragmentB>(true);
			Assert.That(child.State, Is.EqualTo(ViewState.Shown));

			_h.System.Close(shownHost);

			Assert.That(shownHost.State, Is.EqualTo(ViewState.Hidden));
			Assert.That(child.State, Is.EqualTo(ViewState.Hidden));
			Assert.That(_h.Host.IsRegistered(child), Is.True, "statics survive a normal close");
			Assert.That(child.Trace, Is.EqualTo("PrepareShow Register Show PrepareHide Hide Unregister"));
		}

		[Test]
		public void Entrance_IsShowingUntilTheAnimationFinishes()
		{
			var view = ShowWithHold(out var hold);

			Assert.That(view.State, Is.EqualTo(ViewState.Showing));
			Assert.That(view.IsVisible, Is.False);
			Assert.That(view.Trace, Is.EqualTo("PrepareShow Register"));

			hold.Release();

			Assert.That(view.State, Is.EqualTo(ViewState.Shown));
			Assert.That(view.IsVisible, Is.True);
			Assert.That(view.Trace, Is.EqualTo("PrepareShow Register Show"));
		}

		[Test]
		public void Close_DuringEntrance_ReleasesResourcesAndSkipsHideHooks()
		{
			var view = ShowWithHold(out _);
			var log = view.Log;

			_h.System.Close(view);

			Assert.That(string.Join(" ", log), Is.EqualTo("PrepareShow Register Unregister"),
				"OnShow never ran, so neither OnPrepareHide nor OnHide may");
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(0), "the close still settles the view");
		}

		[Test]
		public void Show_DuringEntrance_RestartsTheEntranceWithoutReRegistering()
		{
			var view = ShowWithHold(out var hold);

			_h.System.Show(view);

			Assert.That(hold.Starts, Is.EqualTo(2), "the first entrance was cancelled, a second started");
			Assert.That(view.State, Is.EqualTo(ViewState.Showing));
			Assert.That(view.Trace, Is.EqualTo("PrepareShow Register PrepareShow"));

			hold.Release();

			Assert.That(view.State, Is.EqualTo(ViewState.Shown));
			Assert.That(view.Trace, Is.EqualTo("PrepareShow Register PrepareShow Show"));
		}

		[Test]
		public void Teardown_DuringEntrance_ReleasesResourcesOnly()
		{
			var view = ShowWithHold(out _);

			view.Lifecycle().Teardown();
			view.Lifecycle().Teardown();

			Assert.That(view.State, Is.EqualTo(ViewState.Hidden));
			Assert.That(view.Trace, Is.EqualTo("PrepareShow Register Unregister"));
		}

		[Test]
		public void Teardown_OnShownView_RunsTheCloseHooksExactlyOnce()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			var view = _h.System.Show<RecordingScreen>();

			view.Lifecycle().Teardown();
			view.Lifecycle().Teardown();

			Assert.That(view.State, Is.EqualTo(ViewState.Hidden));
			Assert.That(view.IsVisible, Is.False);
			Assert.That(view.Trace, Is.EqualTo("PrepareShow Register Show PrepareHide Hide Unregister"));
		}

		[Test]
		public void Teardown_DuringExit_FinishesTheExitOnce()
		{
			var view = ShowWithHold(out var hold);
			hold.Release();
			var log = view.Log;

			_h.System.Close(view);
			Assert.That(view.State, Is.EqualTo(ViewState.Hiding));
			Assert.That(string.Join(" ", log), Is.EqualTo("PrepareShow Register Show PrepareHide"));

			view.Lifecycle().Teardown();

			Assert.That(string.Join(" ", log), Is.EqualTo("PrepareShow Register Show PrepareHide Hide Unregister"));
		}

		[Test]
		public void Reset_WhileShown_WarnsAndForcesHidden()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			var view = _h.System.Show<RecordingScreen>();

			LogAssert.Expect(LogType.Warning, new Regex("reset while Shown"));
			view.OnReset();

			Assert.That(view.State, Is.EqualTo(ViewState.Hidden));
		}
	}
}
