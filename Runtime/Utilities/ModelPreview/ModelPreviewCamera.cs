using UnityEngine;

namespace Utilities.ModelPreview
{
	/// <summary>
	/// Stage-side camera for a model preview booth. Frames models from their renderer
	/// bounds, damps interactive yaw/pitch/zoom toward targets, idles with optional
	/// auto-rotate, and can shut itself off after a warmup for static previews.
	/// </summary>
	public sealed class ModelPreviewCamera : MonoBehaviour
	{
		[SerializeField] private Camera _camera;
		[SerializeField] private Transform _pivot;
		[SerializeField] private LayerMask _modelLayer;
		[SerializeField] private float _damping = 10f;
		[SerializeField] private float _maxPitch = 60f;
		[SerializeField] private Vector2 _zoomLimits = new(0.5f, 2f);

		private Vector3 _center;
		private Vector3 _baseDirection;
		private float _baseDistance;
		private float _targetYaw;
		private float _targetPitch;
		private float _targetZoom = 1f;
		private float _yaw;
		private float _pitch;
		private float _zoom = 1f;
		private float _autoRotateSpeed;
		private int _framesUntilDisable = -1;

		public Camera Camera => _camera;
		public Transform Pivot => _pivot;
		public LayerMask ModelLayer => _modelLayer;

		private void Awake()
		{
			if (_camera == null)
			{
				_camera = GetComponentInChildren<Camera>(true);
			}

			if (_pivot == null)
			{
				_pivot = transform;
			}
		}

		public void SetTargetTexture(RenderTexture texture)
		{
			if (_camera == null) return;

			_camera.targetTexture = texture;
			if (texture != null)
			{
				// camera.aspect tracks the game screen until the RT renders at least once — force
				// it here so Frame()'s fit math (and the projection) use the RT's real aspect now.
				_camera.aspect = (float)texture.width / texture.height;
			}
		}

		public void SetBackground(Color? color)
		{
			if (_camera == null) return;
			_camera.clearFlags = CameraClearFlags.SolidColor;
			_camera.backgroundColor = color ?? Color.clear;
		}

		/// <summary>
		/// Positions the camera so the model's bounding SPHERE fits the view, preserving the
		/// stage's authored view direction. A sphere is rotation-invariant, so a model framed
		/// this way cannot clip at any yaw or pitch. The prefab camera's position only
		/// contributes the direction — distance is always recomputed here.
		/// margin: 1 = exact sphere fit (already clip-proof), &gt;1 = extra air.
		/// </summary>
		public void Frame(Bounds bounds, float margin)
		{
			if (_camera == null) return;

			// Half the box's space diagonal — the radius of the sphere containing the whole model.
			float radius = bounds.extents.magnitude;

			float halfVerticalFov = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
			float fitHeight = radius / Mathf.Tan(halfVerticalFov);
			float fitWidth = radius / (Mathf.Tan(halfVerticalFov) * _camera.aspect);
			float distance = Mathf.Max(fitHeight, fitWidth, 0.05f) * Mathf.Max(0.05f, margin);

			Vector3 direction = (_camera.transform.position - _pivot.position);
			direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : -_camera.transform.forward;

			_center = bounds.center;
			_baseDirection = direction;
			_baseDistance = distance;

			_camera.transform.position = _center + _baseDirection * _baseDistance;
			_camera.transform.LookAt(_center);
			_camera.nearClipPlane = Mathf.Max(0.01f, distance * 0.01f);
			_camera.farClipPlane = distance * 10f + bounds.size.magnitude;
		}

		public void RotateBy(float yawDelta, float pitchDelta)
		{
			_targetYaw += yawDelta;
			_targetPitch = Mathf.Clamp(_targetPitch + pitchDelta, -_maxPitch, _maxPitch);
		}

		public void ZoomBy(float factor)
		{
			_targetZoom = Mathf.Clamp(_targetZoom * factor, _zoomLimits.x, _zoomLimits.y);
		}

		public void ResetView()
		{
			_targetYaw = 0f;
			_targetPitch = 0f;
			_targetZoom = 1f;
		}

		public void SetAutoRotate(float degreesPerSecond)
		{
			_autoRotateSpeed = degreesPerSecond;
		}

		/// <summary>Renders a fixed number of frames, then disables the camera (Static render mode).</summary>
		public void RenderStatic(int warmupFrames)
		{
			if (_camera != null)
			{
				_camera.enabled = true;
			}

			_framesUntilDisable = Mathf.Max(1, warmupFrames);
		}

		private void Update()
		{
			if (_framesUntilDisable > 0)
			{
				_framesUntilDisable--;
				if (_framesUntilDisable == 0 && _camera != null)
				{
					_camera.enabled = false;
				}
			}

			if (_autoRotateSpeed != 0f)
			{
				_targetYaw += _autoRotateSpeed * Time.deltaTime;
			}

			float t = 1f - Mathf.Exp(-_damping * Time.deltaTime);
			_yaw = Mathf.Lerp(_yaw, _targetYaw, t);
			_pitch = Mathf.Lerp(_pitch, _targetPitch, t);
			_zoom = Mathf.Lerp(_zoom, _targetZoom, t);

			if (_pivot != null)
			{
				_pivot.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
			}

			if (_camera != null && _baseDistance > 0f)
			{
				_camera.transform.position = _center + _baseDirection * (_baseDistance * _zoom);
			}
		}
	}
}
