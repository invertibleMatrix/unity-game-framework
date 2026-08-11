using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Utilities.ModelPreview
{
	/// <summary>
	/// UI-side input for an interactive model preview, attached next to the RawImage
	/// showing it. Single-finger drag rotates the model; pinch (touch) or scroll
	/// (editor) zooms. All input is captured at the UI graphic — the 3D model
	/// itself needs no colliders or raycasts.
	/// </summary>
	public sealed class ModelPreviewInteractable : MonoBehaviour, IDragHandler, IScrollHandler
	{
		[SerializeField] private float _rotateDegreesPerPixel = 0.25f;
		[SerializeField] private float _pinchZoomSensitivity = 0.005f;
		[SerializeField] private float _scrollZoomSensitivity = 0.05f;

		private Action<float, float> _rotateBy;
		private Action<float> _zoomBy;
		private float _previousPinchDistance = -1f;

		public void Init(Action<float, float> rotateBy, Action<float> zoomBy)
		{
			_rotateBy = rotateBy;
			_zoomBy = zoomBy;
		}

		public void OnDrag(PointerEventData eventData)
		{
			if (_rotateBy == null || Input.touchCount > 1)
			{
				return;
			}

			_rotateBy(eventData.delta.x * _rotateDegreesPerPixel, -eventData.delta.y * _rotateDegreesPerPixel);
		}

		public void OnScroll(PointerEventData eventData)
		{
			_zoomBy?.Invoke(1f + eventData.scrollDelta.y * _scrollZoomSensitivity);
		}

		private void Update()
		{
			if (_zoomBy == null || Input.touchCount != 2)
			{
				_previousPinchDistance = -1f;
				return;
			}

			float distance = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
			if (_previousPinchDistance > 0f)
			{
				float delta = distance - _previousPinchDistance;
				if (Mathf.Abs(delta) > 0.01f)
				{
					_zoomBy(1f + delta * _pinchZoomSensitivity);
				}
			}

			_previousPinchDistance = distance;
		}
	}
}
