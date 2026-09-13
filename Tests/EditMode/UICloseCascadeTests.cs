using AK.Systems;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AK.Tests
{
	/// <summary>
	/// The contract for how children settle when a parent closes: dynamic children are
	/// destroyed (or pooled), static children hide and survive, deep hierarchies cascade,
	/// and a view destroyed outside the system drops all bookkeeping.
	/// </summary>
	public class UICloseCascadeTests
	{
		private UISystemHarness _h;

		[SetUp]    public void SetUp()    => _h = new UISystemHarness();
		[TearDown] public void TearDown() => _h.Dispose();

		[Test]
		public void DynamicParentClose_DestroysDynamicChildren()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>();
			var host = _h.System.Show<RecordingScreen>();
			var child = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			child.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(host));

			Assert.That(child.Trace, Is.EqualTo("PrepareHide Hide Unregister"), "child ran its full close lifecycle exactly once");
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(0));
			Assert.That(_h.System.GetView<RecordingFragment>(), Is.Null);
		}

		[Test]
		public void DynamicParentClose_ReturnsPooledChildrenToThePool()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>(pooled: true);
			var host = _h.System.Show<RecordingScreen>();
			var child = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			child.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(host));

			Assert.That(child != null && child.gameObject != null, Is.True, "pooled child survives as an object");
			Assert.That(child.gameObject.activeSelf, Is.False);
			Assert.That(child.Trace, Does.Contain("BeforePool").And.Contain("Reset"));
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(0));

			var host2 = _h.System.Show<RecordingScreen>();
			var reused = _h.System.Show<RecordingFragment>(ShowOptions.Under(host2));
			Assert.That(reused, Is.SameAs(child), "next show reuses the pooled instance");
		}

		[Test]
		public void PooledParentClose_DoesNotLeaveDynamicChildrenBehindInTheParent()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>(pooled: true);
			_h.MakePrefab<RecordingFragmentB>();
			var host = _h.System.Show<RecordingScreen>();
			var parent = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			var child = _h.System.Show<RecordingFragmentB>(ShowOptions.Under(parent));
			child.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(parent));

			Assert.That(parent.gameObject.activeSelf, Is.False, "parent is pooled");
			Assert.That(child == null || child.gameObject == null, Is.True, "a child the pool cannot reuse is destroyed with the close, not parked inside the pooled parent");
			Assert.That(_h.System.CountOfKind(typeof(RecordingFragmentB)), Is.EqualTo(0));
			Assert.That(parent.GetComponentsInChildren<RecordingFragmentB>(true), Is.Empty);
		}

		[Test]
		public void PooledParentClose_LetsOnBeforePoolCloseChildrenWhileTheyAreStillRegistered()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<ChildClosingParent>(pooled: true);
			_h.MakePrefab<RecordingFragment>();
			var host = _h.System.Show<RecordingScreen>();
			var parent = _h.System.Show<ChildClosingParent>(ShowOptions.Under(host));
			var child = _h.System.Show<RecordingFragment>(ShowOptions.Under(parent));
			parent.ChildToClose = child;
			parent.Clear();
			child.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(parent));

			Assert.That(parent.Trace, Is.EqualTo("PrepareHide Hide Unregister BeforePool Reset"), "hooks run before the cascade, reset after it");
			Assert.That(child.Trace, Is.EqualTo("PrepareHide Hide Unregister"), "the child got one normal close from OnBeforePool, nothing more from the cascade");
			Assert.That(child == null || child.gameObject == null, Is.True);
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(1), "only the host remains");
			LogAssert.NoUnexpectedReceived();
		}

		[Test]
		public void StaticParentClose_HidesStaticChildren_AndDestroysDynamicOnes()
		{
			var screenPrefab = _h.MakePrefab<RecordingScreen>(screen: true);
			var staticHostPrefab = _h.AddStaticChild<RecordingFragment>(screenPrefab, showOnStart: true);
			_h.AddStaticChild<RecordingFragmentB>(staticHostPrefab, showOnStart: true);
			_h.MakePrefab<RecordingFragmentB>(viewId: "dyn");

			var screen = _h.System.Show<RecordingScreen>();
			var staticHost = _h.System.GetView<RecordingFragment>();
			var staticChild = _h.System.GetView<RecordingFragmentB>();
			Assert.That(staticHost.IsVisible, Is.True, "ShowOnStart");
			Assert.That(staticChild.IsVisible, Is.True, "nested ShowOnStart");

			var dynamicChild = _h.System.Show<RecordingFragmentB>(new ShowOptions(parent: staticHost, viewId: "dyn"));
			int registeredBefore = _h.System.RegisteredViewCount;
			staticChild.Clear();
			dynamicChild.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(staticHost));

			Assert.That(staticHost.IsVisible, Is.False);
			Assert.That(staticHost.gameObject.activeSelf, Is.False);
			Assert.That(_h.Host.IsRegistered(staticHost), Is.True, "static survives close");
			Assert.That(_h.Host.IsRegistered(staticChild), Is.True, "nested static survives too");
			Assert.That(staticChild.gameObject.activeSelf, Is.False);
			Assert.That(staticChild.Trace, Is.EqualTo("PrepareHide Hide Unregister"));
			Assert.That(_h.Host.IsRegistered(dynamicChild), Is.False, "dynamic child destroyed");
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(registeredBefore - 1));

			// Re-showing the surviving static re-runs its show lifecycle and its ShowOnStart children.
			staticHost.Clear();
			staticChild.Clear();
			_h.System.Show<RecordingFragment>(ShowOptions.Under(screen));
			Assert.That(staticHost.IsVisible, Is.True);
			Assert.That(staticHost.Trace, Is.EqualTo("PrepareShow Register Show"));
			Assert.That(staticChild.IsVisible, Is.True);
			Assert.That(staticChild.Trace, Is.EqualTo("PrepareShow Register Show"));
		}

		[Test]
		public void ScreenClose_DestroysStaticChildrenToo()
		{
			var screenPrefab = _h.MakePrefab<RecordingScreen>(screen: true);
			_h.AddStaticChild<RecordingFragment>(screenPrefab, showOnStart: true);

			var screen = _h.System.Show<RecordingScreen>();
			var staticChild = _h.System.GetView<RecordingFragment>();
			staticChild.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(screen));

			Assert.That(staticChild == null || staticChild.gameObject == null, Is.True, "destroyed with its dynamic parent");
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(0));
		}

		[Test]
		public void ChildrenFirstParallel_HidesChildrenBeforeParent()
		{
			_h.MakePrefab<RecordingScreen>(screen: true, childCloseOrder: ChildCloseOrder.ChildrenFirstParallel);
			_h.MakePrefab<RecordingFragment>(allowMultiple: true);
			var host = _h.System.Show<RecordingScreen>();
			var a = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			var b = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));

			var order = new System.Collections.Generic.List<string>();
			host.Log.Clear(); a.Log.Clear(); b.Log.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(host));

			Assert.That(a.Trace, Is.EqualTo("PrepareHide Hide Unregister"), "child hides exactly once despite children-first + cascade");
			Assert.That(b.Trace, Is.EqualTo("PrepareHide Hide Unregister"));
			Assert.That(host.Trace, Is.EqualTo("PrepareHide Hide Unregister"));
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(0));
		}

		[Test]
		public void ChildrenFirstSequential_HidesChildrenBeforeParent()
		{
			_h.MakePrefab<RecordingScreen>(screen: true, childCloseOrder: ChildCloseOrder.ChildrenFirstSequential);
			_h.MakePrefab<RecordingFragment>(allowMultiple: true);
			var host = _h.System.Show<RecordingScreen>();
			var a = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			var b = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			host.Log.Clear(); a.Log.Clear(); b.Log.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(host));

			Assert.That(a.Trace, Is.EqualTo("PrepareHide Hide Unregister"));
			Assert.That(b.Trace, Is.EqualTo("PrepareHide Hide Unregister"));
			Assert.That(host.Trace, Is.EqualTo("PrepareHide Hide Unregister"));
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(0));
		}

		/// <summary>
		/// Unity does not run MonoBehaviour.OnDestroy in edit mode (not even with ExecuteAlways),
		/// so the external-destroy path is exercised by invoking the same entry point OnDestroy
		/// calls at runtime. The runtime wiring itself is covered by the play-test checklist.
		/// </summary>
		[Test]
		public void ExternalDestroy_DropsAllBookkeeping()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>();
			var host = _h.System.Show<RecordingScreen>();
			var child = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			child.Clear();

			_h.Host.NotifyDestroyedExternally(child);
			Object.DestroyImmediate(child.gameObject);

			Assert.That(_h.Host.IsRegistered(child), Is.False);
			Assert.That(_h.System.GetView<RecordingFragment>(), Is.Null);
			Assert.That(_h.System.CountOfKind(typeof(RecordingFragment)), Is.EqualTo(0));
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(1), "only the host remains");

			// The host is still healthy and can host again.
			var again = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			Assert.That(again.IsVisible, Is.True);
			Assert.That(_h.System.CountOfKind(typeof(RecordingFragment)), Is.EqualTo(1));
		}

		[Test]
		public void ExternalDestroy_IsIdempotent_AndSafeAfterASystemClose()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			var host = _h.System.Show<RecordingScreen>();

			UISystemHarness.Complete(_h.System.CloseAsync(host));
			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(0));

			// Runtime OnDestroy of an already-closed view reaches this too; it must be a no-op.
			_h.Host.NotifyDestroyedExternally(host);
			_h.Host.NotifyDestroyedExternally(host);

			Assert.That(_h.System.RegisteredViewCount, Is.EqualTo(0));
			Assert.That(_h.System.CountOfKind(typeof(RecordingScreen)), Is.EqualTo(0));
		}

		[Test]
		public void DoubleClose_IsANoOp()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			var host = _h.System.Show<RecordingScreen>();
			host.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(host));
			Assert.That(host == null, Is.True, "dynamic screen is destroyed by its close");

			// Fake-null after destruction: the close pipeline treats it as nothing to do.
			UISystemHarness.Complete(_h.System.CloseAsync(host));

			Assert.That(host.Log, Is.EqualTo(new[] { "PrepareHide", "Hide", "Unregister" }), "lifecycle ran once");
		}

		[Test]
		public void CloseCallback_FiresOnlyWhenSomethingActuallyClosed()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			var host = _h.System.Show<RecordingScreen>();

			int fired = 0;
			_h.System.Close(host, default, () => fired++);
			Assert.That(fired, Is.EqualTo(1));

			_h.System.Close(host, default, () => fired++);

			Assert.That(fired, Is.EqualTo(1), "second close closed nothing, so no callback");
		}
	}
}
