using AK.Core.Extensions;
using AK.Kernel.Timing;
using UnityEngine;

namespace AK.Utilities.Previews
{
	/// <summary>
	/// Stage-side camera for a model preview booth. Frames models from their renderer
	/// bounds, damps interactive yaw/pitch/zoom toward targets, idles with optional
	/// auto-rotate, and in static mode renders only while something on screen changes.
	/// Runs on unscaled time by default, so previews keep animating while the game is paused.
	/// </summary>
	public sealed class ModelPreviewCamera : MonoBehaviour
	{
		// Below these the damped view is treated as settled and snapped to its target.
		private const float AngleEpsilon = 0.01f;
		private const float ZoomEpsilon  = 0.0005f;

		[SerializeField] private Camera _camera;
		[SerializeField] private Transform _pivot;
		[SerializeField] private LayerMask _modelLayer;
		[SerializeField] private float _damping = 10f;
		[SerializeField] private float _maxPitch = 60f;
		[SerializeField] private Vector2 _zoomLimits = new(0.5f, 2f);

		private Vector3 _center;
		private Vector3 _baseDirection;
		private bool _hasBaseDirection;
		private float _baseDistance;
		private float _targetYaw;
		private float _targetPitch;
		private float _targetZoom = 1f;
		private float _yaw;
		private float _pitch;
		private float _zoom = 1f;
		private float _autoRotateSpeed;
		private TimeDomain _timeDomain = TimeDomain.Unscaled;

		private bool _static;
		private int _staticWarmupFrames = 1;
		private int _framesLeft;
		private int _holds;

		/// <summary>The rendering camera; falls back to the first camera under the rig.</summary>
		public Camera Camera
		{
			get
			{
				if (_camera == null)
				{
					_camera = GetComponentInChildren<Camera>(true);
				}

				return _camera;
			}
		}

		/// <summary>The model's turntable; falls back to the rig's own transform.</summary>
		public Transform Pivot
		{
			get
			{
				if (_pivot == null)
				{
					_pivot = transform;
				}

				return _pivot;
			}
		}

		public LayerMask ModelLayer => _modelLayer;

		public TimeDomain TimeDomain => _timeDomain;

		public void SetTargetTexture(RenderTexture texture)
		{
			Camera cam = Camera;
			if (cam == null) return;

			cam.targetTexture = texture;
			if (texture != null)
			{
				// camera.aspect tracks the game screen until the RT renders at least once — force
				// it here so Frame()'s fit math (and the projection) use the RT's real aspect now.
				cam.aspect = (float)texture.width / texture.height;
			}
		}

		public void SetBackground(Color? color)
		{
			Camera cam = Camera;
			if (cam == null) return;

			cam.clearFlags      = CameraClearFlags.SolidColor;
			cam.backgroundColor = color ?? Color.clear;
		}

		/// <summary>Which clock drives damping and auto-rotate.</summary>
		public void SetTimeDomain(TimeDomain domain)
		{
			_timeDomain = domain;
		}

		/// <summary>
		/// Live mode renders every frame. Static mode renders only while something changes (an
		/// intro playing, the view damping toward a new rotation or zoom, auto-rotate) plus
		/// <paramref name="warmupFrames"/> more, then disables the camera and leaves the last
		/// frame in the texture.
		/// </summary>
		public void SetStatic(bool isStatic, int warmupFrames)
		{
			_static             = isStatic;
			_staticWarmupFrames = Mathf.Max(1, warmupFrames);
			_framesLeft         = _staticWarmupFrames;
			SetRendering(true);
		}

		/// <summary>
		/// Keeps a static camera rendering until the matching <see cref="EndHold"/>, e.g. for the
		/// length of an intro tween. Holds nest. No effect in live mode.
		/// </summary>
		public void BeginHold()
		{
			_holds++;
			if (_static)
			{
				SetRendering(true);
			}
		}

		public void EndHold()
		{
			if (_holds > 0)
			{
				_holds--;
			}
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
			Camera cam = Camera;
			if (cam == null) return;

			// Half the box's space diagonal — the radius of the sphere containing the whole model.
			float radius = bounds.extents.magnitude;

			float halfVerticalFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
			float fitHeight = radius / Mathf.Tan(halfVerticalFov);
			float fitWidth = radius / (Mathf.Tan(halfVerticalFov) * cam.aspect);
			float distance = Mathf.Max(fitHeight, fitWidth, 0.05f) * Mathf.Max(0.05f, margin);

			if (!_hasBaseDirection)
			{
				// Read once, from the camera as authored: once framing has moved it, its
				// position no longer says which way the stage meant it to look.
				Vector3 direction = cam.transform.position - Pivot.position;
				_baseDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : -cam.transform.forward;
				_hasBaseDirection = true;
			}

			_center = bounds.center;
			_baseDistance = distance;

			cam.transform.position = _center + _baseDirection * (_baseDistance * _zoom);
			cam.transform.LookAt(_center);
			cam.nearClipPlane = Mathf.Max(0.01f, distance * 0.01f);
			cam.farClipPlane = distance * 10f + bounds.size.magnitude;

			RequestFrames();
		}

		public void RotateBy(float yawDelta, float pitchDelta)
		{
			_targetYaw += yawDelta;
			_targetPitch = Mathf.Clamp(_targetPitch + pitchDelta, -_maxPitch, _maxPitch);
			RequestFrames();
		}

		/// <summary>Multiplies the camera distance: below 1 moves closer (the model grows), above 1 moves away.</summary>
		public void ZoomBy(float factor)
		{
			_targetZoom = Mathf.Clamp(_targetZoom * factor, _zoomLimits.x, _zoomLimits.y);
			RequestFrames();
		}

		/// <summary>Back to the authored view: front on, framed distance. Turns the short way round.</summary>
		public void ResetView()
		{
			_targetYaw = 360f * Mathf.Round(_yaw / 360f);
			_targetPitch = 0f;
			_targetZoom = 1f;
			RequestFrames();
		}

		public void SetAutoRotate(float degreesPerSecond)
		{
			_autoRotateSpeed = degreesPerSecond;
			RequestFrames();
		}

		/// <summary>Static mode: render at least <paramref name="frames"/> more frames before switching off.</summary>
		public void RenderStatic(int frames)
		{
			_framesLeft = Mathf.Max(_framesLeft, Mathf.Max(1, frames));
			SetRendering(true);
		}

		private void Update()
		{
			Tick(_timeDomain.DeltaTime());
		}

		/// <summary>Advances the view by <paramref name="deltaTime"/> seconds on the booth's clock.</summary>
		internal void Tick(float deltaTime)
		{
			if (_autoRotateSpeed != 0f)
			{
				_targetYaw += _autoRotateSpeed * deltaTime;
			}

			if (Mathf.Abs(_targetYaw) > 360f)
			{
				// Take whole turns off both angles, or a long auto-rotate grows them until float
				// precision swallows small drags. The pose and the damping only see the difference.
				float wrapped = _targetYaw % 360f;
				_yaw -= _targetYaw - wrapped;
				_targetYaw = wrapped;
			}

			bool moving = Damp(deltaTime);

			Pivot.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);

			Camera cam = Camera;
			if (cam != null && _baseDistance > 0f)
			{
				cam.transform.position = _center + _baseDirection * (_baseDistance * _zoom);
			}

			if (!_static)
			{
				return;
			}

			if (moving || _holds > 0 || _autoRotateSpeed != 0f)
			{
				RequestFrames();
			}
			else if (_framesLeft > 0 && --_framesLeft == 0)
			{
				SetRendering(false);
			}
		}

		/// <summary>Moves the view toward its targets, frame-rate independently. True while not yet settled.</summary>
		private bool Damp(float deltaTime)
		{
			float t = 1f - Mathf.Exp(-_damping * deltaTime);
			_yaw   = Mathf.Lerp(_yaw, _targetYaw, t);
			_pitch = Mathf.Lerp(_pitch, _targetPitch, t);
			_zoom  = Mathf.Lerp(_zoom, _targetZoom, t);

			bool settled = Mathf.Abs(_yaw - _targetYaw) < AngleEpsilon
			               && Mathf.Abs(_pitch - _targetPitch) < AngleEpsilon
			               && Mathf.Abs(_zoom - _targetZoom) < ZoomEpsilon;

			if (settled)
			{
				_yaw   = _targetYaw;
				_pitch = _targetPitch;
				_zoom  = _targetZoom;
			}

			return !settled;
		}

		private void RequestFrames()
		{
			if (!_static)
			{
				return;
			}

			_framesLeft = Mathf.Max(_framesLeft, _staticWarmupFrames);
			SetRendering(true);
		}

		private void SetRendering(bool rendering)
		{
			Camera cam = Camera;
			if (cam != null && cam.enabled != rendering)
			{
				cam.enabled = rendering;
			}
		}
	}
}
