using System.Threading;
using AK.Systems;
using AK.Tests.Support;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AK.Tests
{
	/// <summary>
	/// The per-parent show gate that serialises <see cref="ShowMode.Serialized"/> fragment
	/// shows. Every other UI test settles synchronously, so the gate never has more than one
	/// show in it; these keep an entrance in flight with a <see cref="HoldAnimation"/> so a
	/// queue actually forms.
	/// </summary>
	public class UIShowGateTests
	{
		private UISystemHarness _h;

		[SetUp]    public void SetUp()    => _h = new UISystemHarness();
		[TearDown] public void TearDown() => _h.Dispose();

		private static ShowOptions Serialized(UIView parent, string viewId = "") =>
			new(parent: parent, viewId: viewId, mode: ShowMode.Serialized);

		private static HoldAnimation HoldOf(UIView view) => view.GetComponent<HoldAnimation>();

		// ---------------------------------------------------------------
		// Ordering: the gate is a queue, not a single slot
		// ---------------------------------------------------------------

		[Test]
		public void ThreeSerializedShows_RunOneAtATime_InCallOrder()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.AddHoldAnimation(_h.MakePrefab<RecordingFragment>(allowMultiple: true));
			var host = _h.System.Show<RecordingScreen>();

			var a = _h.System.Show<RecordingFragment>(Serialized(host));
			var b = _h.System.Show<RecordingFragment>(Serialized(host));
			var c = _h.System.Show<RecordingFragment>(Serialized(host));

			// Only the head of the queue has begun its entrance.
			Assert.That(HoldOf(a).Starts, Is.EqualTo(1), "a is in flight");
			Assert.That(HoldOf(b).Starts, Is.EqualTo(0), "b waits for a");
			Assert.That(HoldOf(c).Starts, Is.EqualTo(0), "c waits for a and b");
			Assert.That(a.State, Is.EqualTo(ViewState.Showing));
			Assert.That(b.State, Is.EqualTo(ViewState.Hidden));
			Assert.That(c.State, Is.EqualTo(ViewState.Hidden));

			HoldOf(a).Release();

			// a settled and exactly one more entered — b, not c.
			Assert.That(a.State, Is.EqualTo(ViewState.Shown));
			Assert.That(HoldOf(b).Starts, Is.EqualTo(1), "b entered after a");
			Assert.That(HoldOf(c).Starts, Is.EqualTo(0), "c still waits for b");

			HoldOf(b).Release();

			Assert.That(b.State, Is.EqualTo(ViewState.Shown));
			Assert.That(HoldOf(c).Starts, Is.EqualTo(1), "c entered after b");

			HoldOf(c).Release();

			Assert.That(c.State, Is.EqualTo(ViewState.Shown));
			Assert.That(a.Trace, Is.EqualTo("PrepareShow Register Show"));
			Assert.That(b.Trace, Is.EqualTo("PrepareShow Register Show"));
			Assert.That(c.Trace, Is.EqualTo("PrepareShow Register Show"));
		}

		[Test]
		public void SerializedShows_LandOnTheHistoryInCallOrder()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.AddHoldAnimation(_h.MakePrefab<RecordingFragment>(allowMultiple: true));
			var host = _h.System.Show<RecordingScreen>();

			var a = _h.System.Show<RecordingFragment>(Serialized(host));
			var b = _h.System.Show<RecordingFragment>(Serialized(host));
			var c = _h.System.Show<RecordingFragment>(Serialized(host));

			HoldOf(a).Release();
			HoldOf(b).Release();
			HoldOf(c).Release();

			Assert.That(_h.System.Histories.TryGet(host, out var history), Is.True);
			Assert.That(history.Count, Is.EqualTo(3));
			Assert.That(history[0], Is.SameAs(a), "bottom");
			Assert.That(history[1], Is.SameAs(b));
			Assert.That(history[2], Is.SameAs(c), "top");
		}

		[Test]
		public void StackPolicy_IsAppliedToAFinishedEntrance_NotOneStillInFlight()
		{
			// b hides what is beneath it. Queued properly, a has finished entering when b lands,
			// so a records a clean Pause after its Show. Run concurrently, b would pause a view
			// that is still Showing.
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.AddHoldAnimation(_h.MakePrefab<RecordingFragment>());
			_h.MakePrefab<RecordingFragmentB>(behaviour: ViewStackBehaviour.HideBelow);
			var host = _h.System.Show<RecordingScreen>();

			var a = _h.System.Show<RecordingFragment>(Serialized(host));
			var b = _h.System.Show<RecordingFragmentB>(Serialized(host));

			Assert.That(b.State, Is.EqualTo(ViewState.Hidden), "b waits for a's entrance");
			Assert.That(a.Trace, Is.EqualTo("PrepareShow Register"), "nothing has touched a yet");

			// Finishes a's entrance; b then lands and starts hiding a — a's hold now holds the exit.
			HoldOf(a).Release();

			Assert.That(a.Trace, Is.EqualTo("PrepareShow Register Show Pause"), "a finished, then b paused it");
			Assert.That(a.State, Is.EqualTo(ViewState.Paused));
			Assert.That(a.Interactable, Is.False);
			Assert.That(HoldOf(a).Starts, Is.EqualTo(2), "a's exit is in flight");

			HoldOf(a).Release();

			Assert.That(a.IsVisible, Is.False);
			Assert.That(b.State, Is.EqualTo(ViewState.Shown));
		}

		// ---------------------------------------------------------------
		// Release: nothing waiting at the gate may hang
		// ---------------------------------------------------------------

		[Test]
		public void QueuedShows_AreReleased_WhenTheParentCloses()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.AddHoldAnimation(_h.MakePrefab<RecordingFragment>(allowMultiple: true));
			var host = _h.System.Show<RecordingScreen>();

			_h.System.Show<RecordingFragment>(Serialized(host));
			UniTask<RecordingFragment> second = _h.System.ShowAsync<RecordingFragment>(Serialized(host));
			UniTask<RecordingFragment> third  = _h.System.ShowAsync<RecordingFragment>(Serialized(host));

			Assert.That(second.Status.IsCompleted(), Is.False, "second is parked behind the first");
			Assert.That(third.Status.IsCompleted(),  Is.False, "third is parked behind the second");

			// Closing the parent cascades to all three children and drops the history. Both
			// parked shows must come back — settled, not still waiting — and must not try to
			// present a view the cascade already tore down, so no error may be logged.
			UISystemHarness.Complete(_h.System.CloseAsync(host, CloseOptions.Now));

			Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Succeeded), "second was released");
			Assert.That(third.Status,  Is.EqualTo(UniTaskStatus.Succeeded), "third was released");
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(0), "nothing survives the parent's close");
			Assert.That(_h.System.Histories.TryGet(host, out _), Is.False, "the history is gone");
			LogAssert.NoUnexpectedReceived();
		}

		[Test]
		public void QueuedShow_StillGetsItsTurn_WhenTheShowAheadOfItIsCancelled()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.AddHoldAnimation(_h.MakePrefab<RecordingFragment>(allowMultiple: true));
			var host = _h.System.Show<RecordingScreen>();

			var a = _h.System.Show<RecordingFragment>(Serialized(host));
			var b = _h.System.Show<RecordingFragment>(Serialized(host));

			// Closing a mid-entrance cancels its run and settles its gate. A predecessor's
			// failure is not b's: its turn must still come.
			_h.System.Close(a, CloseOptions.Now);

			Assert.That(HoldOf(b).Starts, Is.EqualTo(1), "b entered once a's slot settled");

			HoldOf(b).Release();

			Assert.That(b.State, Is.EqualTo(ViewState.Shown));
			Assert.That(_h.System.Histories.TryGet(host, out var history), Is.True);
			Assert.That(history.Count, Is.EqualTo(1));
			Assert.That(history[0], Is.SameAs(b));
		}

		[Test]
		public void ParallelShows_DoNotQueue()
		{
			// The gate is for Serialized only; Parallel shows push history and animate at once.
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.AddHoldAnimation(_h.MakePrefab<RecordingFragment>(allowMultiple: true));
			var host = _h.System.Show<RecordingScreen>();

			var a = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host));
			var b = _h.System.Show<RecordingFragment>(new ShowOptions(parent: host));

			Assert.That(HoldOf(a).Starts, Is.EqualTo(1));
			Assert.That(HoldOf(b).Starts, Is.EqualTo(1), "parallel shows enter together");
		}

		// ---------------------------------------------------------------
		// Cancellation abandons the show
		// ---------------------------------------------------------------

		[Test]
		public void ShowAsync_Cancellation_MidEntrance_ClosesTheView_AndReportsCanceled()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.AddHoldAnimation(_h.MakePrefab<RecordingFragment>(allowMultiple: true));
			var host = _h.System.Show<RecordingScreen>();

			var cts = new CancellationTokenSource();
			UniTask<RecordingFragment> pending = _h.System.ShowAsync<RecordingFragment>(Serialized(host), null, cts.Token);
			var view = _h.System.GetView<RecordingFragment>();

			Assert.That(HoldOf(view).Starts, Is.EqualTo(1), "the entrance is in flight");

			cts.Cancel();

			Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Canceled), "the await reports cancellation, not the view");
			Assert.That(_h.System.GetView<RecordingFragment>(), Is.Null, "the abandoned show was closed, not left half-presented");
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(1), "only the host survives");
			LogAssert.NoUnexpectedReceived();
		}

		[Test]
		public void ShowAsync_Cancellation_WhileQueued_ClosesTheQueuedView_AndKeepsTheQueueMoving()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.AddHoldAnimation(_h.MakePrefab<RecordingFragment>(allowMultiple: true));
			var host = _h.System.Show<RecordingScreen>();

			var a = _h.System.Show<RecordingFragment>(Serialized(host));

			var cts = new CancellationTokenSource();
			UniTask<RecordingFragment> pending = _h.System.ShowAsync<RecordingFragment>(Serialized(host), null, cts.Token);

			Assert.That(_h.System.CountOfKind(typeof(RecordingFragment)), Is.EqualTo(2), "a plus the queued show");
			Assert.That(HoldOf(a).Starts, Is.EqualTo(1), "the queued show is parked, not presenting");

			cts.Cancel();

			Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Canceled));
			Assert.That(_h.System.CountOfKind(typeof(RecordingFragment)), Is.EqualTo(1), "the cancelled show closed; only a remains");
			Assert.That(a.State, Is.EqualTo(ViewState.Showing), "a's entrance was not disturbed");
			LogAssert.NoUnexpectedReceived();
		}
	}
}
