using System.Collections.Generic;
using AK.Utilities.Previews;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace AK.Tests.Previews
{
	/// <summary>
	/// Pointer handling, driven by calling the event handlers directly, as the EventSystem would.
	/// Zoom factors multiply the camera distance, so below 1 brings the model closer.
	/// </summary>
	public class ModelPreviewInteractableTests
	{
		private GameObject _holder;
		private ModelPreviewInteractable _input;
		private readonly List<Vector2> _rotations = new();
		private readonly List<float> _zooms = new();

		[SetUp]
		public void SetUp()
		{
			_holder = new GameObject("Preview", typeof(RectTransform));
			_input = _holder.AddComponent<ModelPreviewInteractable>();
			_input.Init((yaw, pitch) => _rotations.Add(new Vector2(yaw, pitch)), factor => _zooms.Add(factor));
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_holder);
			_rotations.Clear();
			_zooms.Clear();
		}

		[Test]
		public void Drag_Rotates_YawAcross_PitchUpDown()
		{
			_input.OnPointerDown(Pointer(0, new Vector2(100f, 100f)));
			_input.OnDrag(Pointer(0, new Vector2(104f, 92f)));

			Assert.AreEqual(1, _rotations.Count);
			Assert.AreEqual(1f, _rotations[0].x, 1e-5f, "4 px right at 0.25 deg/px");
			Assert.AreEqual(2f, _rotations[0].y, 1e-5f, "8 px down pitches up by 2 deg");
			Assert.IsEmpty(_zooms);
		}

		[Test]
		public void SpreadingTwoFingers_ZoomsIn()
		{
			_input.OnPointerDown(Pointer(0, new Vector2(0f, 0f)));
			_input.OnPointerDown(Pointer(1, new Vector2(100f, 0f)));

			_input.OnDrag(Pointer(1, new Vector2(200f, 0f)));

			Assert.AreEqual(1, _zooms.Count);
			Assert.AreEqual(0.5f, _zooms[0], 1e-5f, "twice the spread halves the distance");
			Assert.IsEmpty(_rotations, "a pinch doesn't rotate");
		}

		[Test]
		public void PinchingTwoFingers_ZoomsOut()
		{
			_input.OnPointerDown(Pointer(0, new Vector2(0f, 0f)));
			_input.OnPointerDown(Pointer(1, new Vector2(100f, 0f)));

			_input.OnDrag(Pointer(1, new Vector2(50f, 0f)));

			Assert.AreEqual(2f, _zooms[0], 1e-5f);
		}

		[Test]
		public void LiftingOneFinger_GoesBackToRotating_WithoutAJump()
		{
			_input.OnPointerDown(Pointer(0, new Vector2(0f, 0f)));
			_input.OnPointerDown(Pointer(1, new Vector2(100f, 0f)));
			_input.OnPointerUp(Pointer(0, new Vector2(0f, 0f)));

			_input.OnDrag(Pointer(1, new Vector2(104f, 0f)));

			Assert.AreEqual(1, _rotations.Count);
			Assert.AreEqual(1f, _rotations[0].x, 1e-5f);
		}

		[Test]
		public void ScrollingUp_ZoomsIn()
		{
			_input.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0f, 2f) });
			_input.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0f, -2f) });

			Assert.AreEqual(2, _zooms.Count);
			Assert.Less(_zooms[0], 1f);
			Assert.AreEqual(1f, _zooms[0] * _zooms[1], 1e-5f, "equal scrolls up and down cancel out");
		}

		[Test]
		public void Drag_IsNeverAClick()
		{
			// The press and the drag both land on the preview, which the event system alone
			// would still let click a parent's click handler on release.
			PointerEventData pointer = Pointer(0, new Vector2(100f, 100f));
			pointer.eligibleForClick = true;

			_input.OnPointerDown(pointer);
			_input.OnBeginDrag(pointer);

			Assert.IsFalse(pointer.eligibleForClick);
		}

		[Test]
		public void OtherMouseButtons_AreIgnored()
		{
			_input.OnPointerDown(Pointer(-2, Vector2.zero, PointerEventData.InputButton.Right));
			_input.OnDrag(Pointer(-2, new Vector2(40f, 0f), PointerEventData.InputButton.Right));

			Assert.IsEmpty(_rotations);
		}

		[Test]
		public void Disconnected_DoesNothing()
		{
			_input.Init(null, null);

			_input.OnPointerDown(Pointer(0, Vector2.zero));
			_input.OnDrag(Pointer(0, new Vector2(40f, 0f)));
			_input.OnScroll(new PointerEventData(null) { scrollDelta = Vector2.up });

			Assert.IsEmpty(_rotations);
			Assert.IsEmpty(_zooms);
		}

		private static PointerEventData Pointer(int id, Vector2 position,
		                                        PointerEventData.InputButton button = PointerEventData.InputButton.Left)
		{
			return new PointerEventData(null) { pointerId = id, position = position, button = button };
		}
	}
}
