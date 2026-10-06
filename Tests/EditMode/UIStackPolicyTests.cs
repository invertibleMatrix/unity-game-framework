using AK.Systems;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests
{
	/// <summary>
	/// The contract for what happens to the view *below* when a view with a given
	/// <see cref="ViewStackBehaviour"/> is pushed on top, and what happens when it is closed
	/// again. Each test asserts the exact hook sequence on the covered view. Screens and
	/// fragments are checked separately because they run through different pipelines today;
	/// after the refactor both must still produce these traces.
	/// </summary>
	public class UIStackPolicyTests
	{
		private UISystemHarness _h;

		[SetUp]    public void SetUp()    => _h = new UISystemHarness();
		[TearDown] public void TearDown() => _h.Dispose();

		private const string FreshShow = "PrepareShow Register Show";

		// ---------------------------------------------------------------
		// Screens (channel stack)
		// ---------------------------------------------------------------

		[TestCase(ViewStackBehaviour.DoNothing,          "",                  true,  "")]
		[TestCase(ViewStackBehaviour.HideBelow,          "Pause",             false, "Resume")]
		[TestCase(ViewStackBehaviour.PauseOnlyBelow,     "Pause",             false, "Resume")]
		public void Screen_PushThenClose_AppliesPolicyToScreenBelow(ViewStackBehaviour behaviour, string expectedOnPush,
		                                                            bool interactableWhileCovered, string expectedOnClose)
		{
			_h.MakePrefab<RecordingScreen>(screen: true, allowMultiple: true);
			_h.MakePrefab<RecordingFragment>(screen: true, behaviour: behaviour);

			var below = _h.System.Show<RecordingScreen>();
			Assert.That(below.Trace, Is.EqualTo(FreshShow));
			below.Clear();

			var top = _h.System.Show<RecordingFragment>();
			Assert.That(below.Trace, Is.EqualTo(expectedOnPush), "on push");
			Assert.That(below.Interactable, Is.EqualTo(interactableWhileCovered), "interactable while covered");
			Assert.That(below.IsVisible, Is.EqualTo(behaviour is ViewStackBehaviour.DoNothing or ViewStackBehaviour.PauseOnlyBelow), "visible while covered");
			Assert.That(top.IsVisible, Is.True);
			below.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(top));
			Assert.That(below.Trace, Is.EqualTo(expectedOnClose), "on close of the top view");
			Assert.That(below.Interactable, Is.True, "interactable restored");
			Assert.That(below.IsVisible, Is.True, "visible restored");
			Assert.That(_h.System.GetView<RecordingFragment>(), Is.Null);
		}

		[Test]
		public void Screen_CloseBelow_ClosesTheScreenBelowOnPush()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>(screen: true, behaviour: ViewStackBehaviour.CloseBelow);

			var below = _h.System.Show<RecordingScreen>();
			below.Clear();

			var top = _h.System.Show<RecordingFragment>();

			Assert.That(below.Trace, Is.EqualTo("PrepareHide Hide Unregister"));
			Assert.That(_h.System.GetView<RecordingScreen>(), Is.Null, "closed screen is unregistered");
			Assert.That(top.IsVisible, Is.True);
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(1));
		}

		[TestCase(ViewStackBehaviour.HideBelow)]
		[TestCase(ViewStackBehaviour.PauseOnlyBelow)]
		public void Screen_Immediate_PushThenClose_MatchesAnimatedTrace(ViewStackBehaviour behaviour)
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>(screen: true, behaviour: behaviour);

			var below = _h.System.Show<RecordingScreen>();
			below.Clear();

			var top = _h.System.Show<RecordingFragment>(new ShowOptions(mode: ShowMode.Immediate));
			Assert.That(below.Trace, Is.EqualTo("Pause"));
			Assert.That(below.Interactable, Is.False);
			below.Clear();

			_h.System.Close(top, CloseOptions.Now);
			Assert.That(below.Trace, Is.EqualTo("Resume"));
			Assert.That(below.Interactable, Is.True);
			Assert.That(below.IsVisible, Is.True);
		}

		[Test]
		public void Screen_PausingAScreen_AlsoPausesItsFragments()
		{
			_h.MakePrefab<RecordingScreen>(screen: true, allowMultiple: true);
			_h.MakePrefab<RecordingFragment>();
			_h.MakePrefab<RecordingFragmentB>(screen: true, behaviour: ViewStackBehaviour.PauseOnlyBelow);

			var host = _h.System.Show<RecordingScreen>();
			var child = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			child.Clear();
			host.Clear();

			var top = _h.System.Show<RecordingFragmentB>();
			Assert.That(host.Trace, Is.EqualTo("Pause"));
			Assert.That(child.Trace, Is.EqualTo("Pause"), "fragments of a paused screen are paused too");
			Assert.That(child.State, Is.EqualTo(ViewState.Paused), "the fragment's state follows the lifecycle, not just the hook");
			Assert.That(child.Interactable, Is.False, "the fragment loses input with its parent");
			child.Clear();
			host.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(top));
			Assert.That(host.Trace, Is.EqualTo("Resume"));
			Assert.That(child.Trace, Is.EqualTo("Resume"));
			Assert.That(child.State, Is.EqualTo(ViewState.Shown), "the fragment's state is restored with its parent's");
			Assert.That(child.Interactable, Is.True, "the fragment's input is restored with its parent's");
		}

		[Test]
		public void Screen_ClosingANonTopScreen_DoesNotTouchTheTop()
		{
			_h.MakePrefab<RecordingScreen>(screen: true, allowMultiple: true);

			var first = _h.System.Show<RecordingScreen>();
			var second = _h.System.Show<RecordingScreen>();
			second.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(first));

			Assert.That(second.Trace, Is.EqualTo(""), "top screen sees nothing when one beneath it closes");
			Assert.That(second.IsVisible, Is.True);
			Assert.That(_h.System.GetView<RecordingScreen>(), Is.SameAs(second));
		}

		[Test]
		public void Screen_ClosingANonTopPooledScreen_ReturnsItToThePool()
		{
			_h.MakePrefab<RecordingScreen>(screen: true, allowMultiple: true, pooled: true);

			var first = _h.System.Show<RecordingScreen>();
			var second = _h.System.Show<RecordingScreen>();
			first.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(first));

			Assert.That(first != null && first.gameObject != null, Is.True, "a mid-stack close honours pooling like a top close");
			Assert.That(first.Trace, Is.EqualTo("PrepareHide Hide Unregister BeforePool Reset"));
			Assert.That(second.IsVisible, Is.True);

			var third = _h.System.Show<RecordingScreen>();
			Assert.That(third, Is.SameAs(first), "the pooled instance is reused");
		}

		[TestCase(ViewStackBehaviour.HideBelow)]
		[TestCase(ViewStackBehaviour.PauseOnlyBelow)]
		public void Screen_MidStackClose_ResumesTheScreenBelow_WhenNothingElseCoversIt(ViewStackBehaviour behaviour)
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>(screen: true, behaviour: behaviour);
			_h.MakePrefab<RecordingFragmentB>(screen: true);

			var bottom = _h.System.Show<RecordingScreen>();
			var middle = _h.System.Show<RecordingFragment>();
			bottom.Clear();

			// A DoNothing screen on top, so `middle` is not the top of the stack.
			var top = _h.System.Show<RecordingFragmentB>();
			top.Clear();
			Assert.That(bottom.Trace, Is.EqualTo(""), "DoNothing on top touches nothing");

			UISystemHarness.Complete(_h.System.CloseAsync(middle));

			Assert.That(bottom.Trace, Is.EqualTo("Resume"), "bottom was covered only by middle");
			Assert.That(bottom.Interactable, Is.True);
			Assert.That(bottom.IsVisible, Is.True);
			Assert.That(top.Trace, Is.EqualTo(""), "the top screen sees nothing");
			Assert.That(top.IsVisible, Is.True);
		}

		[Test]
		public void Screen_MidStackClose_LeavesTheScreenBelowCovered_WhenAnotherCoverRemains()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>(screen: true, behaviour: ViewStackBehaviour.HideBelow, allowMultiple: true);

			var bottom = _h.System.Show<RecordingScreen>();
			var middle = _h.System.Show<RecordingFragment>();
			var top = _h.System.Show<RecordingFragment>();
			bottom.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(middle));

			Assert.That(bottom.Trace, Is.EqualTo(""), "top still hides what is below, so bottom stays hidden");
			Assert.That(bottom.IsVisible, Is.False);
			Assert.That(top.IsVisible, Is.True);
		}

		// ---------------------------------------------------------------
		// Fragments (per-parent history)
		// ---------------------------------------------------------------

		[TestCase(ViewStackBehaviour.DoNothing,          "",      true,  "")]
		[TestCase(ViewStackBehaviour.HideBelow,          "Pause", false, "Resume")]
		[TestCase(ViewStackBehaviour.PauseOnlyBelow,     "Pause", false, "Resume")]
		public void Fragment_SerializedPushThenClose_AppliesPolicyToFragmentBelow(ViewStackBehaviour behaviour, string expectedOnPush,
		                                                                          bool interactableWhileCovered, string expectedOnClose)
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>();
			_h.MakePrefab<RecordingFragmentB>(behaviour: behaviour);
			var host = _h.System.Show<RecordingScreen>();

			var below = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host, mode: ShowMode.Serialized));
			Assert.That(below.Trace, Is.EqualTo(FreshShow));
			below.Clear();

			var top = _h.System.Show<RecordingFragmentB>(new ShowOptions(parent: host, mode: ShowMode.Serialized));
			Assert.That(below.Trace, Is.EqualTo(expectedOnPush), "on push");
			Assert.That(below.Interactable, Is.EqualTo(interactableWhileCovered), "interactable while covered");
			Assert.That(below.IsVisible, Is.EqualTo(behaviour is ViewStackBehaviour.DoNothing or ViewStackBehaviour.PauseOnlyBelow), "visible while covered");
			below.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(top));
			Assert.That(below.Trace, Is.EqualTo(expectedOnClose), "on close of the top fragment");
			Assert.That(below.Interactable, Is.True);
			Assert.That(below.IsVisible, Is.True);
		}

		[TestCase(ViewStackBehaviour.HideBelow)]
		[TestCase(ViewStackBehaviour.PauseOnlyBelow)]
		public void Fragment_ShownWithAStackBehaviourOverride_ResumesTheFragmentBelowOnClose(ViewStackBehaviour overrideBehaviour)
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>();
			_h.MakePrefab<RecordingFragmentB>(behaviour: ViewStackBehaviour.DoNothing);
			var host = _h.System.Show<RecordingScreen>();

			var below = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host, mode: ShowMode.Serialized));
			below.Clear();

			// The override, not the prefab's DoNothing, decides both what the push does and what the close undoes.
			var top = _h.System.Show<RecordingFragmentB>(new ShowOptions(parent: host, stackBehaviour: overrideBehaviour));
			Assert.That(below.Trace, Is.EqualTo("Pause"), "on push");
			Assert.That(below.Interactable, Is.False);
			below.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(top));
			Assert.That(below.Trace, Is.EqualTo("Resume"), "on close");
			Assert.That(below.Interactable, Is.True);
			Assert.That(below.IsVisible, Is.True);
		}

		[TestCase(ViewStackBehaviour.HideBelow)]
		[TestCase(ViewStackBehaviour.PauseOnlyBelow)]
		public void Screen_ShownWithAStackBehaviourOverride_ResumesTheScreenBelowOnClose(ViewStackBehaviour overrideBehaviour)
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>(screen: true, behaviour: ViewStackBehaviour.DoNothing);

			var below = _h.System.Show<RecordingScreen>();
			below.Clear();

			var top = _h.System.Show<RecordingFragment>(new ShowOptions(stackBehaviour: overrideBehaviour));
			Assert.That(below.Trace, Is.EqualTo("Pause"), "on push");
			below.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(top));
			Assert.That(below.Trace, Is.EqualTo("Resume"), "on close");
			Assert.That(below.Interactable, Is.True);
			Assert.That(below.IsVisible, Is.True);
		}

		[Test]
		public void Fragment_CloseBelow_ClosesTheFragmentBelowOnPush()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>();
			_h.MakePrefab<RecordingFragmentB>(behaviour: ViewStackBehaviour.CloseBelow);
			var host = _h.System.Show<RecordingScreen>();

			var below = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host, mode: ShowMode.Serialized));
			below.Clear();

			var top = _h.System.Show<RecordingFragmentB>(new ShowOptions(parent: host, mode: ShowMode.Serialized));

			Assert.That(below.Trace, Is.EqualTo("PrepareHide Hide Unregister"));
			Assert.That(_h.System.GetView<RecordingFragment>(), Is.Null);
			Assert.That(top.IsVisible, Is.True);
		}

		[Test]
		public void Fragment_ParallelShow_IgnoresStackBehaviourOfTheNewFragment()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>();
			_h.MakePrefab<RecordingFragmentB>(behaviour: ViewStackBehaviour.HideBelow);
			var host = _h.System.Show<RecordingScreen>();

			var below = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			below.Clear();

			// Default show path is parallel: siblings do not negotiate, so HideBelow has no effect.
			var top = _h.System.Show<RecordingFragmentB>(ShowOptions.Under(host));

			Assert.That(below.Trace, Is.EqualTo(""));
			Assert.That(below.IsVisible, Is.True);
			Assert.That(top.IsVisible, Is.True);
		}

		[TestCase(ViewStackBehaviour.HideBelow)]
		[TestCase(ViewStackBehaviour.PauseOnlyBelow)]
		public void Fragment_MidStackClose_ResumesTheFragmentBelow_WhenNothingElseCoversIt(ViewStackBehaviour behaviour)
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>();
			_h.MakePrefab<RecordingFragmentB>(behaviour: behaviour, allowMultiple: true);
			_h.MakePrefab<RecordingFragment>(viewId: "toast");
			var host = _h.System.Show<RecordingScreen>();

			var bottom = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host, mode: ShowMode.Serialized));
			var middle = _h.System.Show<RecordingFragmentB>(new ShowOptions(parent: host, mode: ShowMode.Serialized));
			bottom.Clear();

			// A DoNothing toast-like view on top so `middle` is not the top of the history.
			var toast = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host, viewId: "toast", mode: ShowMode.Serialized));
			Assert.That(toast, Is.Not.Null);
			Assert.That(bottom.Trace, Is.EqualTo(""), "DoNothing on top touches nothing");

			UISystemHarness.Complete(_h.System.CloseAsync(middle));

			Assert.That(bottom.Trace, Is.EqualTo("Resume"), "bottom was covered only by middle");
			Assert.That(bottom.Interactable, Is.True);
			Assert.That(bottom.IsVisible, Is.True);
			Assert.That(toast.IsVisible, Is.True);
		}

		[Test]
		public void Fragment_MidStackClose_LeavesTheFragmentBelowCovered_WhenAnotherCoverRemains()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>();
			_h.MakePrefab<RecordingFragmentB>(behaviour: ViewStackBehaviour.HideBelow, allowMultiple: true);
			var host = _h.System.Show<RecordingScreen>();

			var a = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host, mode: ShowMode.Serialized));
			var b = _h.System.Show<RecordingFragmentB>(new ShowOptions(parent: host, mode: ShowMode.Serialized));
			var c = _h.System.Show<RecordingFragmentB>(new ShowOptions(parent: host, mode: ShowMode.Serialized));
			a.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(b));

			Assert.That(a.Trace, Is.EqualTo(""), "c still hides below, so a must stay hidden");
			Assert.That(a.IsVisible, Is.False);
			Assert.That(c.IsVisible, Is.True);
		}

		[Test]
		public void Fragment_ReshowingAPausedFragment_BringsItToTop_WithoutReRegistering()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>();
			_h.MakePrefab<RecordingFragmentB>(behaviour: ViewStackBehaviour.PauseOnlyBelow);
			var host = _h.System.Show<RecordingScreen>();

			var a = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host, mode: ShowMode.Serialized));
			var b = _h.System.Show<RecordingFragmentB>(new ShowOptions(parent: host, mode: ShowMode.Serialized));
			Assert.That(a.Interactable, Is.False);
			a.Clear();
			b.Clear();

			// Showing the same type again resolves to a replace: the paused instance closes,
			// a fresh one spawns on top, and b (DoNothing) sees nothing.
			var a2 = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host, mode: ShowMode.Serialized));

			Assert.That(a2, Is.Not.SameAs(a));
			Assert.That(a.Trace, Is.EqualTo("PrepareHide Hide Unregister"), "old instance closed");
			Assert.That(a2.IsVisible, Is.True);
			Assert.That(b.Trace, Is.EqualTo(""));
			Assert.That(_h.System.CountOfKind(typeof(RecordingFragment)), Is.EqualTo(1));
		}
	}
}
