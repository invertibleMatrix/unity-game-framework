using System.Collections.Generic;
using AK.Systems;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AK.Tests
{
	/// <summary>
	/// The scene side of the input gate: the blocker answers every pointer while the gate is
	/// held, outranks every other raycaster, and stays out of the way while it is free. The
	/// ranking against real canvases is pinned in play mode, where raycasters register.
	/// </summary>
	public class UIInputBlockerTests
	{
		private readonly List<Object> _created = new();
		private UIInputGate    _gate;
		private UIInputBlocker _blocker;

		[SetUp]
		public void SetUp()
		{
			_gate = new UIInputGate();

			var go = new GameObject("Blocker");
			_created.Add(go);
			_blocker = go.AddComponent<UIInputBlocker>();
			_blocker.Bind(_gate);
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var o in _created)
				if (o != null) Object.DestroyImmediate(o);
			_created.Clear();
		}

		private static PointerEventData Pointer(Vector2 position) => new(null) { position = position };

		[Test]
		public void WhileTheGateIsFree_ReportsNothing()
		{
			var results = new List<RaycastResult>();

			_blocker.Raycast(Pointer(new Vector2(10f, 20f)), results);

			Assert.That(results, Is.Empty);
		}

		[Test]
		public void WhileTheGateIsHeld_ReportsOneHit_OnItself()
		{
			InputHold hold = _gate.Hold("test");
			var results = new List<RaycastResult> { default }; // another raycaster's hit

			_blocker.Raycast(Pointer(new Vector2(10f, 20f)), results);

			Assert.That(results.Count, Is.EqualTo(2), "appends one hit");
			RaycastResult hit = results[1];
			Assert.That(hit.gameObject, Is.SameAs(_blocker.gameObject), "presses land on an object that handles nothing");
			Assert.That(hit.module, Is.SameAs(_blocker));
			Assert.That(hit.index, Is.EqualTo(1f));
			Assert.That(hit.screenPosition, Is.EqualTo(new Vector2(10f, 20f)));

			hold.Release();
			results.Clear();
			_blocker.Raycast(Pointer(new Vector2(10f, 20f)), results);

			Assert.That(results, Is.Empty, "input comes back with the release");
		}

		[Test]
		public void OutranksEveryRaycaster_CameraOrNot()
		{
			// The EventSystem ranks hits from different raycasters by camera depth when both
			// have a camera, then by these priorities: no camera and the highest priority put
			// the blocker's hit first.
			Assert.That(_blocker.eventCamera, Is.Null);
			Assert.That(_blocker.sortOrderPriority, Is.EqualTo(int.MaxValue));
			Assert.That(_blocker.renderOrderPriority, Is.EqualTo(int.MaxValue));
		}

		[Test]
		public void UISystem_OwnsOneGate_EnforcedByItsBlocker()
		{
			using var h = new UISystemHarness();

			Assert.That(h.System.InputGate, Is.Not.Null);

			UIInputBlocker[] blockers = h.System.GetComponentsInChildren<UIInputBlocker>(true);
			Assert.That(blockers.Length, Is.EqualTo(1));
			Assert.That(blockers[0].Gate, Is.SameAs(h.System.InputGate));

			h.System.EnsureInitialized();

			Assert.That(h.System.GetComponentsInChildren<UIInputBlocker>(true).Length, Is.EqualTo(1), "initializing again adds no second blocker");
		}
	}
}
