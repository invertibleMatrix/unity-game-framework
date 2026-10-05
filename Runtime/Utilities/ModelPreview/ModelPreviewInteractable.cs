using System;
using AK.Kernel.Gestures;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AK.Utilities.Previews
{
	/// <summary>
	/// UI-side input for an interactive model preview, attached next to the RawImage showing it.
	/// One-finger (or left-mouse) drag rotates the model; a two-finger pinch or the scroll wheel
	/// zooms, and spreading the fingers or scrolling up brings the model closer. Only pointers
	/// that go down on this graphic take part, so touches elsewhere never move the preview, and
	/// the 3D model needs no colliders.
	///
	/// The graphic keeps the presses it receives, so a parent's pointer-down handler doesn't see
	/// them. A tap still reaches a parent's click handler; a drag never does.
	/// </summary>
	public sealed class ModelPreviewInteractable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler,
	                                               IDragHandler, IScrollHandler
	{
		[Tooltip("Degrees of yaw or pitch per pixel dragged.")]
		[SerializeField] private float _rotateDegreesPerPixel = 0.25f;

		[Tooltip("How closely zoom follows a pinch: 1 = the camera distance scales inversely with the finger spread.")]
		[SerializeField] private float _pinchZoomGain = 1f;

		[Tooltip("Zoom per unit of scroll; each unit scales the camera distance by e^-x.")]
		[SerializeField] private float _scrollZoomSensitivity = 0.05f;

		private readonly PinchGesture _gesture = new();
		private Action<float, float> _rotateBy;
		private Action<float> _zoomBy;

		/// <summary>Routes rotation (yaw, pitch in degrees) and zoom (distance factor) to a preview; nulls disconnect it.</summary>
		public void Init(Action<float, float> rotateBy, Action<float> zoomBy)
		{
			_rotateBy = rotateBy;
			_zoomBy   = zoomBy;
			_gesture.Reset();
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			if (eventData.button == PointerEventData.InputButton.Left)
			{
				_gesture.Press(eventData.pointerId, ToGesture(eventData.position));
			}
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			if (eventData.button == PointerEventData.InputButton.Left)
			{
				_gesture.Release(eventData.pointerId);
			}
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
			// Press and drag both land on this graphic, so the event system would still count
			// the release as a click on a parent's click handler. A drag is never a click.
			eventData.eligibleForClick = false;
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (eventData.button != PointerEventData.InputButton.Left)
			{
				return;
			}

			GestureStep step = _gesture.Move(eventData.pointerId, ToGesture(eventData.position));

			if (_rotateBy != null && (step.Drag.X != 0f || step.Drag.Y != 0f))
			{
				_rotateBy(step.Drag.X * _rotateDegreesPerPixel, -step.Drag.Y * _rotateDegreesPerPixel);
			}

			// Spread > 1 (fingers apart) gives a factor < 1: the camera closes in.
			if (_zoomBy != null && step.Spread != 1f)
			{
				_zoomBy(MathF.Pow(step.Spread, -_pinchZoomGain));
			}
		}

		public void OnScroll(PointerEventData eventData)
		{
			// Scrolling up (positive) gives a factor < 1: the camera closes in.
			float scroll = eventData.scrollDelta.y;
			if (_zoomBy != null && scroll != 0f)
			{
				_zoomBy(MathF.Exp(-scroll * _scrollZoomSensitivity));
			}
		}

		private void OnDisable()
		{
			// Pointer-ups that arrive while disabled are lost; start clean next time.
			_gesture.Reset();
		}

		private static System.Numerics.Vector2 ToGesture(Vector2 position) => new(position.x, position.y);
	}
}
