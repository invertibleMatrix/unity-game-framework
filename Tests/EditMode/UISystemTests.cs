using System.Collections.Generic;
using AK.Systems;
using Cysharp.Threading.Tasks;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace AK.Tests
{
	/// <summary>
	/// Edit-mode coverage of the UISystem's bookkeeping: registry index, static reuse,
	/// replace-on-show, GetView ordering and pooling. Views here carry no animation
	/// strategy, so every show/close settles synchronously inside the UniTask machinery
	/// and nothing needs a frame to pass.
	/// </summary>
	public class UISystemTests
	{
		private sealed class ScreenA : UIView { }
		private sealed class ScreenB : UIView { }
		private sealed class FragmentX : UIView { public int Inits; }
		private sealed class FragmentY : UIView { }

		private UISystemHarness _h;
		private UISystem _system => _h.System;

		[SetUp]    public void SetUp()    => _h = new UISystemHarness();
		[TearDown] public void TearDown() => _h.Dispose();

		private T MakePrefab<T>(string viewId = "", bool screen = false, bool pooled = false, bool allowMultiple = false,
		                        ViewStackBehaviour behaviour = ViewStackBehaviour.DoNothing) where T : UIView
			=> _h.MakePrefab<T>(viewId, screen, pooled, allowMultiple, behaviour);

		private T AddStaticChild<T>(UIView parent, string viewId = "", bool showOnStart = false, bool template = false) where T : UIView
			=> _h.AddStaticChild<T>(parent, viewId, showOnStart, template);

		private static void Complete(UniTask task) => UISystemHarness.Complete(task);

		// ---------------------------------------------------------------
		// Registry index
		// ---------------------------------------------------------------

		[Test]
		public void Show_Screen_RegistersAndIndexesByKind()
		{
			MakePrefab<ScreenA>(screen: true);

			var shown = _system.Show<ScreenA>();

			Assert.That(shown, Is.Not.Null);
			Assert.That(_system.RegisteredViewCount, Is.EqualTo(1));
			Assert.That(_system.CountOfKind(typeof(ScreenA)), Is.EqualTo(1));
			Assert.That(_system.GetView<ScreenA>(), Is.SameAs(shown));
			Assert.That(shown.IsVisible, Is.True);
		}

		[Test]
		public void Close_Screen_UnregistersAndClearsTheIndex()
		{
			MakePrefab<ScreenA>(screen: true);
			var shown = _system.Show<ScreenA>();

			Complete(_system.CloseAsync(shown));

			Assert.That(_system.RegisteredViewCount, Is.EqualTo(0));
			Assert.That(_system.CountOfKind(typeof(ScreenA)), Is.EqualTo(0));
			Assert.That(_system.GetView<ScreenA>(), Is.Null);
		}

		[Test]
		public void Index_ChainSurvivesRemovingHeadMiddleAndTail()
		{
			MakePrefab<ScreenA>(screen: true, allowMultiple: true);

			var first = _system.Show<ScreenA>();
			var second = _system.Show<ScreenA>();
			var third = _system.Show<ScreenA>();
			Assert.That(_system.CountOfKind(typeof(ScreenA)), Is.EqualTo(3));

			// Registration prepends, so the chain is third -> second -> first.
			Complete(_system.CloseAsync(second));
			Assert.That(_system.CountOfKind(typeof(ScreenA)), Is.EqualTo(2), "middle removed");

			Complete(_system.CloseAsync(third));
			Assert.That(_system.CountOfKind(typeof(ScreenA)), Is.EqualTo(1), "head removed");

			Complete(_system.CloseAsync(first));
			Assert.That(_system.CountOfKind(typeof(ScreenA)), Is.EqualTo(0), "tail removed");
			Assert.That(_system.RegisteredViewCount, Is.EqualTo(0));
		}

		[Test]
		public void VariantIds_AreDistinctKinds()
		{
			MakePrefab<ScreenA>(screen: true);
			MakePrefab<ScreenA>(viewId: "alt", screen: true);

			var plain = _system.Show<ScreenA>();
			var alt = _system.Show<ScreenA>(ShowOptions.Variant("alt"));

			Assert.That(plain, Is.Not.SameAs(alt));
			Assert.That(_system.CountOfKind(typeof(ScreenA)), Is.EqualTo(1));
			Assert.That(_system.CountOfKind(typeof(ScreenA), "alt"), Is.EqualTo(1));
			Assert.That(_system.GetView<ScreenA>("alt"), Is.SameAs(alt));
			Assert.That(_system.GetView<ScreenA>(), Is.SameAs(plain));
		}

		// ---------------------------------------------------------------
		// Fragments, replace, static reuse
		// ---------------------------------------------------------------

		[Test]
		public void Show_Fragment_ReplacesExistingDynamicInstance_WhenMultipleNotAllowed()
		{
			MakePrefab<ScreenA>(screen: true);
			MakePrefab<FragmentX>();
			var host = _system.Show<ScreenA>();

			var first = _system.Show<FragmentX>(ShowOptions.Under(host));
			var second = _system.Show<FragmentX>(ShowOptions.Under(host));

			Assert.That(second, Is.Not.SameAs(first));
			Assert.That(_system.CountOfKind(typeof(FragmentX)), Is.EqualTo(1), "old instance replaced, not stacked");
			Assert.That(_system.GetView<FragmentX>(), Is.SameAs(second));
			Assert.That(first == null || !first.IsVisible, Is.True, "replaced instance is gone or hidden");
		}

		[Test]
		public void Show_Fragment_AllowMultiple_KeepsBothInstances()
		{
			MakePrefab<ScreenA>(screen: true);
			MakePrefab<FragmentX>(allowMultiple: true);
			var host = _system.Show<ScreenA>();

			var first = _system.Show<FragmentX>(ShowOptions.Under(host));
			var second = _system.Show<FragmentX>(ShowOptions.Under(host));

			Assert.That(second, Is.Not.SameAs(first));
			Assert.That(_system.CountOfKind(typeof(FragmentX)), Is.EqualTo(2));
		}

		[Test]
		public void Show_Fragment_WithoutParent_HostsOnTopmostScreen()
		{
			MakePrefab<ScreenA>(screen: true);
			MakePrefab<FragmentX>();
			var host = _system.Show<ScreenA>();

			var fragment = _system.Show<FragmentX>();

			Assert.That(fragment, Is.Not.Null);
			Assert.That(fragment.ParentView, Is.SameAs(host));
			Assert.That(fragment.transform.parent, Is.SameAs(host.FragmentContainer));
		}

		[Test]
		public void Show_ReusesStaticChild_InsteadOfInstantiating()
		{
			var screenPrefab = MakePrefab<ScreenA>(screen: true);
			MakePrefab<FragmentX>();
			AddStaticChild<FragmentX>(screenPrefab);

			var host = _system.Show<ScreenA>();
			Assert.That(_system.CountOfKind(typeof(FragmentX)), Is.EqualTo(1), "static child registered with its host");

			int inits = 0;
			var shown = _system.Show<FragmentX>(ShowOptions.Under(host), v => inits++);

			Assert.That(shown.ParentView, Is.SameAs(host));
			Assert.That(shown.transform.IsChildOf(host.transform), Is.True);
			Assert.That(_system.CountOfKind(typeof(FragmentX)), Is.EqualTo(1), "no prefab instance was spawned");
			Assert.That(inits, Is.EqualTo(1));
			Assert.That(shown.IsVisible, Is.True);

			var again = _system.Show<FragmentX>();
			Assert.That(again, Is.SameAs(shown), "parent-less show re-routes onto the static's own host");
		}

		[Test]
		public void Show_StaticTemplate_SpawnsCloneAndNeverShowsTheTemplate()
		{
			var screenPrefab = MakePrefab<ScreenA>(screen: true);
			AddStaticChild<FragmentY>(screenPrefab, template: true);

			var host = _system.Show<ScreenA>();

			var a = _system.Show<FragmentY>(ShowOptions.Under(host));
			var b = _system.Show<FragmentY>(ShowOptions.Under(host));

			Assert.That(a, Is.Not.Null);
			Assert.That(b, Is.Not.Null);
			Assert.That(a, Is.Not.SameAs(b), "each show clones");
			Assert.That(a.IsTemplate, Is.True, "clone carries the serialized flag but is registered dynamic");
			Assert.That(_system.CountOfKind(typeof(FragmentY)), Is.EqualTo(3), "template + two clones");

			int hiddenTemplates = 0;
			for (int i = 0; i < host.transform.childCount; i++)
			{
				var child = host.transform.GetChild(i).GetComponent<FragmentY>();
				if (child != null && !child.IsVisible) hiddenTemplates++;
			}

			Assert.That(hiddenTemplates, Is.EqualTo(1), "only the template itself stays hidden");
		}

		// ---------------------------------------------------------------
		// GetView ordering
		// ---------------------------------------------------------------

		[Test]
		public void GetView_PrefersScreenOnChannelStack_OverFragment()
		{
			MakePrefab<ScreenA>(screen: true);
			MakePrefab<FragmentX>();
			var host = _system.Show<ScreenA>();
			var fragment = _system.Show<FragmentX>(ShowOptions.Under(host));

			Assert.That(_system.GetView<ScreenA>(), Is.SameAs(host));
			Assert.That(_system.GetView<FragmentX>(), Is.SameAs(fragment));
			Assert.That(_system.GetView<FragmentY>(), Is.Null);
		}

		[Test]
		public void GetView_SkipsViewsThatAreClosing()
		{
			MakePrefab<ScreenA>(screen: true);
			MakePrefab<FragmentX>();
			var host = _system.Show<ScreenA>();
			var fragment = _system.Show<FragmentX>(ShowOptions.Under(host));

			Complete(_system.CloseAsync(fragment));

			Assert.That(_system.GetView<FragmentX>(), Is.Null);
		}

		// ---------------------------------------------------------------
		// Pool
		// ---------------------------------------------------------------

		[Test]
		public void PooledView_IsReusedOnNextShow_AndKeepsItsVariantId()
		{
			MakePrefab<ScreenA>(screen: true);
			MakePrefab<FragmentX>(viewId: "small", pooled: true);
			var host = _system.Show<ScreenA>();

			var first = _system.Show<FragmentX>(new ShowOptions(parent: host, viewId: "small"));
			Complete(_system.CloseAsync(first));
			Assert.That(first != null && first.gameObject != null, Is.True, "pooled, not destroyed");
			Assert.That(first.gameObject.activeSelf, Is.False);

			var second = _system.Show<FragmentX>(new ShowOptions(parent: host, viewId: "small"));

			Assert.That(second, Is.SameAs(first), "instance came back from the pool");
			Assert.That(second.ViewId, Is.EqualTo("small"), "variant id restored on reuse");
			Assert.That(_system.CountOfKind(typeof(FragmentX), "small"), Is.EqualTo(1));
		}

		[Test]
		public void Close_UnregisteredView_WarnsAndDoesNothing()
		{
			var stray = new GameObject("Stray", typeof(RectTransform), typeof(CanvasGroup)).AddComponent<FragmentX>();
			_h.Track(stray.gameObject);

			using (ExpectedLog.Warning("not registered"))
			{
				Complete(_system.CloseAsync(stray));
			}
		}

		// ---------------------------------------------------------------
		// Allocation budget
		// ---------------------------------------------------------------

		[Test]
		public void GetView_DoesNotAllocate_OnceWarm()
		{
			MakePrefab<ScreenA>(screen: true);
			MakePrefab<FragmentX>();
			var host = _system.Show<ScreenA>();
			_system.Show<FragmentX>(ShowOptions.Under(host));

			for (int i = 0; i < 8; i++)
			{
				_system.GetView<ScreenA>();
				_system.GetView<FragmentX>();
				_system.GetView<FragmentY>();
			}

			int Window() => GcAllocations.Count(() =>
			{
				for (int i = 0; i < 200; i++)
				{
					_system.GetView<ScreenA>();
					_system.GetView<FragmentX>();
					_system.GetView<FragmentY>();
				}
			});

			Window();
			Assert.That(Window(), Is.EqualTo(0), "GetView must not allocate");
		}
	}
}
