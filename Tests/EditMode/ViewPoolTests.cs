using AK.Systems;
using AK.Tests.Support;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

namespace AK.Tests
{
	/// <summary>
	/// The view pool: how many closed views it keeps, how it gives memory back, and that a
	/// reused view starts like a fresh instance of its prefab: posed like it, without the
	/// context of its last use, its static children reset, and none of its tweens running.
	/// </summary>
	public class ViewPoolTests
	{
		private sealed class PanelContext : UIContext { }
		private sealed class Panel : UIView<PanelContext> { }

		private UISystemHarness _h;

		[TearDown] public void TearDown() => _h?.Dispose();

		private RecordingScreen ShowHost()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			return _h.System.Show<RecordingScreen>();
		}

		[Test]
		public void Close_WhenItsKindIsFull_DestroysTheView_WithoutTellingItItWasPooled()
		{
			_h = new UISystemHarness(poolCapacityPerKind: 1);
			_h.MakePrefab<RecordingFragment>(pooled: true, allowMultiple: true);
			var host = ShowHost();
			var first = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			var second = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			first.Clear();
			second.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(first));
			UISystemHarness.Complete(_h.System.CloseAsync(second));

			Assert.That(first.Trace, Is.EqualTo("PrepareHide Hide Unregister BeforePool Reset"));
			Assert.That(second == null, Is.True, "no room: destroyed like a view that never pools");
			Assert.That(second.Trace, Is.EqualTo("PrepareHide Hide Unregister"), "and never told it was going to the pool");
			Assert.That(_h.System.Pool.Count, Is.EqualTo(1));
		}

		[Test]
		public void TrimPool_DestroysTheIdleViewsBeyondTheCountKept()
		{
			_h = new UISystemHarness();
			_h.MakePrefab<RecordingFragment>(pooled: true, allowMultiple: true);
			var host = ShowHost();
			var a = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			var b = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			var c = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			UISystemHarness.Complete(_h.System.CloseAsync(a));
			UISystemHarness.Complete(_h.System.CloseAsync(b));
			UISystemHarness.Complete(_h.System.CloseAsync(c));
			Assert.That(_h.System.Pool.Count, Is.EqualTo(3));

			_h.System.TrimPool(1);

			Assert.That(_h.System.Pool.Count, Is.EqualTo(1));
			Assert.That((a != null ? 1 : 0) + (b != null ? 1 : 0) + (c != null ? 1 : 0), Is.EqualTo(1), "the others are destroyed");

			_h.System.TrimPool();

			Assert.That(_h.System.Pool.Count, Is.EqualTo(0));
			Assert.That(a == null && b == null && c == null, Is.True);

			var fresh = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			Assert.That(fresh.IsVisible, Is.True, "an empty pool instantiates again");
		}

		[Test]
		public void ReusedView_IsPosedLikeAFreshInstanceOfItsPrefab()
		{
			_h = new UISystemHarness();
			var prefab = _h.MakePrefab<RecordingFragment>(pooled: true);
			var prefabRect = (RectTransform)prefab.transform;
			prefabRect.anchorMin = new Vector2(0.1f, 0.2f);
			prefabRect.anchorMax = new Vector2(0.9f, 0.8f);
			prefabRect.pivot = new Vector2(0.3f, 0.7f);
			prefabRect.sizeDelta = new Vector2(-20f, 40f);
			prefabRect.anchoredPosition3D = new Vector3(5f, -6f, 0f);
			var host = ShowHost();

			var view = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			var rect = (RectTransform)view.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.pivot = Vector2.one;
			rect.sizeDelta = new Vector2(300f, 300f);
			rect.anchoredPosition3D = new Vector3(40f, -30f, 7f);
			rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
			rect.localScale = new Vector3(2f, 2f, 2f);
			UISystemHarness.Complete(_h.System.CloseAsync(view));

			var reused = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));

			Assert.That(reused, Is.SameAs(view));
			Assert.That(rect.anchorMin, Is.EqualTo(prefabRect.anchorMin));
			Assert.That(rect.anchorMax, Is.EqualTo(prefabRect.anchorMax));
			Assert.That(rect.pivot, Is.EqualTo(prefabRect.pivot));
			Assert.That(rect.sizeDelta, Is.EqualTo(prefabRect.sizeDelta));
			Assert.That(rect.anchoredPosition3D, Is.EqualTo(prefabRect.anchoredPosition3D));
			Assert.That(rect.localRotation, Is.EqualTo(prefabRect.localRotation));
			Assert.That(rect.localScale, Is.EqualTo(prefabRect.localScale));
		}

		[Test]
		public void PooledView_DropsTheContextOfItsLastUse()
		{
			_h = new UISystemHarness();
			_h.MakePrefab<Panel>(pooled: true);
			var host = ShowHost();
			var context = new PanelContext();

			var view = _h.System.Show<Panel>(ShowOptions.Under(host, context));
			Assert.That(view.Context, Is.SameAs(context));

			UISystemHarness.Complete(_h.System.CloseAsync(view));
			Assert.That(view.Context, Is.Null, "dropped on its way to the pool");

			var reused = _h.System.Show<Panel>(ShowOptions.Under(host));

			Assert.That(reused, Is.SameAs(view));
			Assert.That(reused.Context, Is.Not.Null.And.Not.SameAs(context), "a show without a context gets a fresh one, not the last one");
		}

		[Test]
		public void PooledView_ResetsTheStaticChildrenItCarries()
		{
			_h = new UISystemHarness();
			var prefab = _h.MakePrefab<RecordingFragment>(pooled: true);
			_h.AddStaticChild<RecordingFragmentB>(prefab, showOnStart: true);
			var host = ShowHost();

			var view = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			var child = view.GetComponentInChildren<RecordingFragmentB>(true);
			Assert.That(child.IsVisible, Is.True);
			child.Clear();

			UISystemHarness.Complete(_h.System.CloseAsync(view));

			Assert.That(child.Trace, Is.EqualTo("PrepareHide Hide Unregister Reset"), "reset with the view that carries it");

			child.Clear();
			var reused = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));

			Assert.That(reused, Is.SameAs(view));
			Assert.That(child.Trace, Is.EqualTo("PrepareShow Register Show"), "and shown again with it");
		}

		[Test]
		public void PooledView_KillsTheTweensOfItsLastUse_OnlyInsideIt()
		{
			_h = new UISystemHarness();
			var prefab = _h.MakePrefab<RecordingFragment>(pooled: true);
			var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasGroup));
			label.transform.SetParent(prefab.transform, false);
			var host = ShowHost();

			var view = _h.System.Show<RecordingFragment>(ShowOptions.Under(host));
			var labelGroup = view.transform.Find("Label").GetComponent<CanvasGroup>();
			Tween onAChild = DOTween.To(() => labelGroup.alpha, a => labelGroup.alpha = a, 0f, 10f).SetTarget(labelGroup);
			Tween byAnId   = DOTween.To(() => 0f, _ => { }, 1f, 10f).SetId(labelGroup.gameObject);
			CanvasGroup hostGroup = host.CanvasGroup;
			Tween outside  = DOTween.To(() => hostGroup.alpha, a => hostGroup.alpha = a, 0f, 10f).SetTarget(hostGroup);

			try
			{
				UISystemHarness.Complete(_h.System.CloseAsync(view));

				Assert.That(onAChild.IsActive(), Is.False, "a tween on a component inside the view");
				Assert.That(byAnId.IsActive(), Is.False, "a tween kept by an id inside the view");
				Assert.That(outside.IsActive(), Is.True, "a tween outside the view plays on");
			}
			finally
			{
				outside.Kill();
			}
		}
	}
}
