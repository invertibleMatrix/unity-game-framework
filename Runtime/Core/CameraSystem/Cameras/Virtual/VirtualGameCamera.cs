using AK.Core;
using Reflex.Attributes;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// A first-class Cinemachine virtual camera for the CameraSystem. Wraps any
	/// <see cref="CinemachineVirtualCameraBase"/> (CinemachineCamera, FreeLook, StateDriven...).
	///
	/// The intended setup is the classic Cinemachine one: ONE physical camera with a single
	/// CinemachineBrain (a <see cref="CinemachineBaseCamera{TEntity,TState}"/>), and any number
	/// of these virtual cameras placed in the scene or spawned from prefabs. Activation is
	/// priority-driven: the system boosts the live camera's priority above the rest and the
	/// brain blends to it - disabling/demoting one smoothly shifts to the next.
	///
	/// The CameraType UID is OPTIONAL. Leave it null and lookups fall back to
	/// "first camera of this type". Assign UIDs only when you have multiple variants of the
	/// same type (e.g. several prefab variants) that you need to address individually.
	/// </summary>
	[RequireComponent(typeof(CinemachineVirtualCameraBase))]
	public class VirtualGameCamera : MonoBehaviour, IVirtualGameCamera
	{
		[Header("Camera Config")]
		[Tooltip("OPTIONAL. The CameraType identifying this camera. Leave empty to be found as 'first of this type'. " +
		         "Assign only when multiple variants of the same type must be addressable by UID.")]
		[SerializeField] protected CameraType _cameraType;

		[Tooltip("OPTIONAL. The brain (base) camera this virtual camera conceptually feeds. " +
		         "Only needed to disambiguate between multiple brains - usually there is exactly one.")]
		[ShowIf("@_cameraType != null")]
		[SerializeField] protected CameraType _baseCameraType;

		[Tooltip("Priority while this camera is NOT live. The live camera gets BasePriority + the system's active boost.")]
		[SerializeField] protected int _basePriority = 10;

		[Tooltip("The fallback camera: activated automatically when the live camera is disabled or unbound.")]
		[SerializeField] protected bool _isDefault;

		[SerializeField] protected CinemachineVirtualCameraBase _virtualCamera;

		[Tooltip("Optional impulse source for Shake(). Without it, Shake() does nothing (virtual cameras have no transform shake).")]
		[SerializeField] protected CinemachineImpulseSource _impulseSource;

		[Inject] private ICameraSystem _cameraSystem;

		private bool _isBound;

		public CameraRole Role       => CameraRole.Virtual;
		public int        LayerOrder => 0;
		public GameObject GameObject => gameObject;

		/// <summary>
		/// Virtual cameras own no Unity Camera. Returns the camera of the brain this virtual
		/// camera feeds when resolvable, otherwise null.
		/// </summary>
		public Camera Camera => ResolveBrainCamera();

		public UID CameraTypeUID         => _cameraType;
		public UID DefaultBaseCameraUID  => _baseCameraType;

		public CinemachineVirtualCameraBase VirtualCamera => _virtualCamera;
		public int  BasePriority => _basePriority;
		public bool IsDefault    => _isDefault;

		/// <summary>True while this camera's priority is boosted above the base priority (i.e. it is the live one).</summary>
		public bool IsLive => _virtualCamera != null && _virtualCamera.Priority > _basePriority;

#if UNITY_EDITOR
		protected virtual void OnValidate()
		{
			if (_virtualCamera == null) _virtualCamera = GetComponent<CinemachineVirtualCameraBase>();
			if (_impulseSource == null) _impulseSource = GetComponent<CinemachineImpulseSource>();
		}
#endif

		protected virtual void Awake()
		{
			if (_virtualCamera == null) _virtualCamera = GetComponent<CinemachineVirtualCameraBase>();
			if (_impulseSource == null) _impulseSource = GetComponent<CinemachineImpulseSource>();
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
		/// Applies config from a CameraDefinition (used when spawned from the registry so the
		/// definition is the single source of truth, not the prefab's serialized fields).
		/// </summary>
		public void ApplyDefinition(CameraDefinition definition)
		{
			if (definition == null) return;

			_cameraType      = definition.CameraType;
			_baseCameraType  = definition.BaseCameraType;
			_basePriority    = definition.BasePriority;
			_isDefault       = definition.IsDefault;
		}

		protected virtual void OnDestroy()
		{
			if (_isBound && _cameraSystem != null)
			{
				_cameraSystem.UnbindCamera(this);
				_isBound = false;
			}

			_cameraSystem = null;
		}

		/// <summary>
		/// Makes this the live camera (priority boost via the system). When not bound and
		/// enableGameObject is true, simply activates the GameObject (standalone usage).
		/// </summary>
		public void Enable(bool enableGameObject = true)
		{
			if (_cameraSystem != null)
			{
				_cameraSystem.ActivateVirtualCamera(CameraTypeUID, this);
				return;
			}

			if (enableGameObject) gameObject.SetActive(true);
		}

		/// <summary>
		/// Demotes this camera back to standby priority. If it was live, the system falls back
		/// to the default camera and the brain blends to it.
		/// </summary>
		public void Disable(bool disableGameObject = true)
		{
			if (_cameraSystem != null)
			{
				_cameraSystem.DeactivateVirtualCamera(this);
				if (disableGameObject) gameObject.SetActive(false);
				return;
			}

			if (disableGameObject) gameObject.SetActive(false);
		}

		public virtual void Shake(float intensity, float duration)
		{
			if (_impulseSource != null)
			{
				_impulseSource.GenerateImpulseWithVelocity(Vector3.one * intensity);
			}
		}

		private Camera ResolveBrainCamera()
		{
			if (_cameraSystem == null) return null;

			// Explicit brain assignment wins; otherwise the first bound Cinemachine base camera.
			var brainCam = (_baseCameraType != null && !_baseCameraType.IsEmpty()
				                ? _cameraSystem.GetCamera<ICinemachineGameCamera>(_baseCameraType)
				                : null)
			               ?? _cameraSystem.GetCamera<ICinemachineGameCamera>();

			return brainCam?.Camera;
		}
	}
}
