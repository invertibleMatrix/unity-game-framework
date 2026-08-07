using System;
using System.Collections;
using AK.Core;
using AK.StateMachine;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;
using Reflex.Core;

namespace AK.Systems
{
    [RequireComponent(typeof(Camera))]
    public abstract class BaseCamera<TEntity, TState> : StateEntity<TEntity, TState>, IGameCamera
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

        [Inject] private ICameraSystem _cameraSystem;
        private bool _isBound;

        public CameraRole Role       => _role;
        public int        LayerOrder => _layerOrder;
        public Camera     Camera     => _camera;
        public GameObject GameObject => gameObject;

        /// <summary>
        /// UID identifying what kind of camera this is.
        /// </summary>
        public UID CameraTypeUID => _cameraType;

        /// <summary>
        /// If this is an Overlay camera, which Base CameraType UID does it belong to?
        /// </summary>
        public UID DefaultBaseCameraUID => _baseCameraType;

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

        protected override void OnDestroy()
        {
            if (_cameraSystem != null)
            {
                _cameraSystem.UnbindCamera(this);
                _cameraSystem = null;
            }
        }

        private void UpdateCameraRenderType()
        {
            if (_camera == null) return;

            var cameraData = _camera.GetUniversalAdditionalCameraData();
            if (cameraData == null) return;

            switch (_role)
            {
                case CameraRole.Base:
                    cameraData.renderType = CameraRenderType.Base;
                    break;
                case CameraRole.Overlay:
                    cameraData.renderType = CameraRenderType.Overlay;
                    break;
            }
        }

        public void Enable(bool enableGameObject = true)
        {
            if (_cameraSystem != null)
                _cameraSystem.EnableCamera(GetType(), enableGameObject);
        }

        public void Disable(bool disableGameObject = true)
        {
            if (_cameraSystem != null)
                _cameraSystem.DisableCamera(GetType(), disableGameObject);
        }

        public virtual void Shake(float intensity, float duration)
        {
            StartCoroutine(ShakeRoutine(Camera.transform, intensity, duration));
        }

        private IEnumerator ShakeRoutine(Transform camTransform, float intensity, float duration)
        {
            Vector3 originalPos = camTransform.localPosition;
            float elapsed = 0.0f;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * intensity;
                float y = Random.Range(-1f, 1f) * intensity;

                camTransform.localPosition = originalPos + new Vector3(x, y, 0);

                elapsed += Time.deltaTime;
                yield return null;
            }

            camTransform.localPosition = originalPos;
        }
    }

    // Non-generic wrapper for simple cameras
    public class BaseCamera : BaseCamera<BaseCamera, BaseCamera.VoidState>
    {
        public sealed class VoidState : BaseState<BaseCamera> { }
    }
}