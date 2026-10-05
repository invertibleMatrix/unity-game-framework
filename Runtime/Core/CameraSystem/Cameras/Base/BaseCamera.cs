using System.Collections;
using AK.Core;
using AK.Core.Extensions;
using AK.Kernel.Timing;
using AK.StateMachines;
using Reflex.Attributes;
using UnityEngine;

namespace AK.Systems
{
    [RequireComponent(typeof(Camera))]
    public abstract class BaseCamera<TEntity, TState> : StateEntity<TEntity, TState>, ISpawnableCamera
        where TEntity : GameEntity
        where TState : BaseState<TEntity>, new()
    {
        [Header("Camera Config")] [SerializeField]
        protected CameraRole _role = CameraRole.Base;

        [SerializeField] protected int _layerOrder;

        [Tooltip("The CameraType UID that identifies this camera (e.g., Main, UI, Effects).")]
        [SerializeField] protected CameraType _cameraType;

        [Tooltip("For Overlay cameras: the CameraType UID of the Base camera this overlay stacks on.")]
        [SerializeField] protected CameraType _baseCameraType;

        [SerializeField] protected Camera _camera;

        [Tooltip("The time a shake runs on. Scaled time stops when the game pauses at a time scale of zero, and the shake holds still.")]
        [SerializeField] protected TimeDomain _shakeTime = TimeDomain.Scaled;

        [Inject] private ICameraSystem _cameraSystem;
        private bool _isBound;

        // The shake in progress: what it moves, how strongly, for how much longer, and the offset
        // it has put on the camera.
        private Coroutine     _shakeRoutine;
        private Transform     _shaken;
        private float         _shakeIntensity;
        private float         _shakeRemaining;
        private Vector3       _shakeOffset;
        private System.Random _shakeRandom;

        public CameraRole Role       => _role;
        public int        LayerOrder => _layerOrder;
        public Camera     Camera     => _camera;
        public GameObject GameObject => gameObject;

        public Uid<CameraType> CameraTypeId        => _cameraType != null ? _cameraType.IdAs<CameraType>() : default;
        public Uid<CameraType> DefaultBaseCameraId => _baseCameraType != null ? _baseCameraType.IdAs<CameraType>() : default;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            UpdateCameraRenderType();
        }
#endif

        protected override void Awake()
        {
            base.Awake();

            if (_camera == null) _camera = GetComponent<Camera>();
            UpdateCameraRenderType();
        }

        protected virtual void Start()
        {
            // Auto-bind if ICameraSystem was injected by Reflex DI
            if (_cameraSystem != null && !_isBound)
            {
                _cameraSystem.BindCamera(this);
                _isBound = true;
            }
        }

        /// <summary>
        /// Manually binds this camera to a CameraSystem.
        /// Only needed for dynamically created cameras that weren't injected by Reflex.
        /// </summary>
        public void BindToSystem(ICameraSystem cameraSystem)
        {
            if (_isBound) return;
            _cameraSystem = cameraSystem;
            _cameraSystem.BindCamera(this);
            _isBound = true;
        }

        /// <summary>
        /// Applies config from a CameraDefinition to this camera instance.
        /// Used by CameraSystem when spawning cameras from the registry so that
        /// the definition is the single source of truth, not the prefab's serialized fields.
        /// </summary>
        public void ApplyDefinition(CameraDefinition definition)
        {
            if (definition == null) return;

            _cameraType = definition.CameraType;
            _role = definition.Role;
            _layerOrder = definition.LayerOrder;
            _baseCameraType = definition.BaseCameraType;

            UpdateCameraRenderType();
        }

        protected virtual void OnDisable()
        {
            // A deactivated object's coroutines stop: end the shake here, so its offset doesn't stay.
            EndShake();
        }

        protected override void OnDestroy()
        {
            if (_cameraSystem != null)
            {
                _cameraSystem.UnbindCamera(this);
                _cameraSystem = null;
            }

            base.OnDestroy();
        }

        private void UpdateCameraRenderType() => CameraStack.ApplyRenderType(_camera, _role);

        /// <summary>
        /// Enables this camera through its camera system: an overlay goes back on its base
        /// camera's stack. Without a system, it only activates the GameObject when asked to.
        /// </summary>
        public void Enable(bool enableGameObject = true)
        {
            if (_cameraSystem != null)
            {
                _cameraSystem.EnableCamera(this, enableGameObject);
            }
            else if (enableGameObject)
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Disables this camera through its camera system: an overlay leaves its base camera's
        /// stack. Without a system, it only deactivates the GameObject when asked to.
        /// </summary>
        public void Disable(bool disableGameObject = true)
        {
            if (_cameraSystem != null)
            {
                _cameraSystem.DisableCamera(this, disableGameObject);
            }
            else if (disableGameObject)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Shakes the camera for <paramref name="duration"/> seconds of its shake time, each frame
        /// moving it to a random offset of up to <paramref name="intensity"/> along its local x and
        /// y. A shake started during another joins it: the shake runs as strongly as the stronger
        /// and as long as the longer. When it ends, the camera is where it would be without it,
        /// wherever something else moved it meanwhile. Nothing happens on an inactive camera.
        /// </summary>
        public virtual void Shake(float intensity, float duration)
        {
            if (!(intensity > 0f) || !(duration > 0f) || !isActiveAndEnabled || _camera == null) return;

            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
            _shakeRemaining = Mathf.Max(_shakeRemaining, duration);

            if (_shakeRoutine == null)
            {
                _shaken = _camera.transform;
                _shakeRandom ??= new System.Random();
                _shakeRoutine = StartCoroutine(ShakeRoutine());
            }
        }

        private IEnumerator ShakeRoutine()
        {
            MoveShake();

            while (true)
            {
                yield return null;

                float step = _shakeTime.DeltaTime();
                if (!(step > 0f)) continue;

                _shakeRemaining -= step;
                if (_shakeRemaining <= 0f) break;

                MoveShake();
            }

            _shakeRoutine = null;
            EndShake();
        }

        // Moves the camera from the shake's last offset to a new one, so movement from elsewhere stays.
        private void MoveShake()
        {
            var offset = new Vector3(Spread() * _shakeIntensity, Spread() * _shakeIntensity, 0f);
            _shaken.localPosition += offset - _shakeOffset;
            _shakeOffset = offset;
        }

        // Draws from the camera's own generator, not UnityEngine.Random, so shaking can't change a
        // seeded game's random sequence.
        private float Spread() => (float)(_shakeRandom.NextDouble() * 2d - 1d);

        private void EndShake()
        {
            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                _shakeRoutine = null;
            }

            if (_shaken != null)
            {
                _shaken.localPosition -= _shakeOffset;
            }

            _shakeOffset    = Vector3.zero;
            _shakeIntensity = 0f;
            _shakeRemaining = 0f;
        }
    }

    // Non-generic wrapper for simple cameras
    public class BaseCamera : BaseCamera<BaseCamera, BaseCamera.VoidState>
    {
        public sealed class VoidState : BaseState<BaseCamera> { }
    }
}
