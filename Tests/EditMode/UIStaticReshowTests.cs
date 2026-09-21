using System.Text.RegularExpressions;
using AK.Systems;
using AK.Tests.Support;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AK.Tests
{
	/// <summary>
	/// A static view is never twinned: a show that arrives while its close is still in
	/// flight queues behind that close and re-presents the same instance — or is dropped
	/// when the close ends in teardown. The dynamic case needs none of this: a closing
	/// dynamic view is skipped by every lookup, so the show simply spawns fresh.
	/// </summary>
	public class UIStaticReshowTests
	{
		private UISystemHarness _h;

		[SetUp]    public void SetUp()    => _h = new UISystemHarness();
		[TearDown] public void TearDown() => _h.Dispose();

		private static HoldAnimation HoldOf(UIView view) => view.GetComponent<HoldAnimation>();

		/// <summary>Shows a host carrying one held static child and returns the pair once the child is shown.</summary>
		private (RecordingScreen host, RecordingFragment child) ShowHostWithStaticChild()
		{
			var hostPrefab = _h.MakePrefab<RecordingScreen>(screen: true);
			_h.AddHoldAnimation(_h.AddStaticChild<RecordingFragment>(hostPrefab));
			var host = _h.System.Show<RecordingScreen>();

			// Showing resolves the spawned copy's static child — the prefab's child is only a template for it.
			var child = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host));
			Assert.That(child, Is.Not.Null);
			Assert.That(child.ParentView, Is.SameAs(host));
			HoldOf(child).Release();
			Assert.That(child.State, Is.EqualTo(ViewState.Shown));

			return (host, child);
		}

		[Test]
		public void Show_WhileStaticChildIsClosing_QueuesTheReshow_OnTheSameInstance()
		{
			var (host, child) = ShowHostWithStaticChild();

			UniTask closing = _h.System.CloseAsync(child);
			Assert.That(child.State, Is.EqualTo(ViewState.Hiding));
			Assert.That(HoldOf(child).Starts, Is.EqualTo(2), "the hide is in flight");

			var again = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host));

			Assert.That(again, Is.SameAs(child), "the show queues behind the close instead of spawning a twin");
			Assert.That(_h.System.CountOfKind(typeof(RecordingFragment)), Is.EqualTo(1));
			Assert.That(HoldOf(child).Starts, Is.EqualTo(2), "no entrance runs while the close is in flight");
			Assert.That(child.State, Is.EqualTo(ViewState.Hiding));

			HoldOf(child).Release();

			Assert.That(closing.Status.IsCompleted(), Is.True, "the close settled");
			Assert.That(child.State, Is.EqualTo(ViewState.Showing), "the queued re-show started when the close settled");
			Assert.That(child.gameObject.activeSelf, Is.True);
			Assert.That(HoldOf(child).Starts, Is.EqualTo(3));
			Assert.That(_h.System.CountOfKind(typeof(RecordingFragment)), Is.EqualTo(1));

			HoldOf(child).Release();

			Assert.That(child.State, Is.EqualTo(ViewState.Shown));
			Assert.That(_h.System.GetView<RecordingFragment>(), Is.SameAs(child));
			Assert.That(child.IsVisible, Is.True);
		}

		[Test]
		public void Show_WhileStaticChildIsClosing_IsDropped_WhenTheHostCascadesItAway()
		{
			var (host, child) = ShowHostWithStaticChild();

			UniTask closing = _h.System.CloseAsync(child);
			Assert.That(child.State, Is.EqualTo(ViewState.Hiding));

			var again = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host));
			Assert.That(again, Is.SameAs(child), "queued, not twinned");

			// Closing the host tears the static child down (a dynamic parent's close does
			// not leave statics registered) — the queued re-show must be dropped, not
			// resurrect a removed view.
			UISystemHarness.Complete(_h.System.CloseAsync(host, CloseOptions.Now));

			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(0));
			Assert.That(_h.System.CountOfKind(typeof(RecordingFragment)), Is.EqualTo(0));
			Assert.That(closing.Status.IsCompleted(), Is.True, "the child's close settled through the cascade");
			Assert.That(_h.System.GetView<RecordingFragment>(), Is.Null);
			LogAssert.NoUnexpectedReceived();
		}

		[Test]
		public void ShowAsync_ExistingView_WhileItIsClosing_IsRefused()
		{
			var (host, child) = ShowHostWithStaticChild();

			UniTask closing = _h.System.CloseAsync(child);
			Assert.That(child.State, Is.EqualTo(ViewState.Hiding));

			LogAssert.Expect(LogType.Warning, new Regex("it is closing"));
			UniTask reshown = _h.System.ShowAsync(child, new ShowOptions(parent: host));

			Assert.That(reshown.Status.IsCompleted(), Is.True, "refused synchronously");
			Assert.That(child.State, Is.EqualTo(ViewState.Hiding), "the close in flight was not disturbed");
			Assert.That(HoldOf(child).Starts, Is.EqualTo(2), "no extra run started");
			Assert.That(_h.System.CountOfKind(typeof(RecordingFragment)), Is.EqualTo(1));

			HoldOf(child).Release();

			Assert.That(closing.Status.IsCompleted(), Is.True);
			Assert.That(child.State, Is.EqualTo(ViewState.Hidden), "a static child stays registered and hidden after its close");
			Assert.That(_h.System.GetView<RecordingFragment>(), Is.SameAs(child), "still registered for a later show");
		}
	}
}
