using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AK.Systems
{
	public class CameraSystem : GameEntity, ICameraSystem
	{
		[SerializeField]
		private CameraRegistry _cameraRegistry;

		[Tooltip("Added to a virtual camera's BasePriority while it is live. Higher than any standby priority wins the brain.")]
		[SerializeField] private int _virtualCameraActiveBoost = 100;

		// Multiple cameras may share a concrete type (prefab variants) - keep them all, first-bound wins lookups.
		private readonly Dictionary<Type, List<IGameCamera>> _camerasByType = new();

		private readonly Dictionary<Uid, IGameCamera> _camerasById = new();

		// Insertion-ordered list backing the "first bound camera" fallback for None identities.
		private readonly List<IGameCamera> _bindOrder = new();

		private readonly Dictionary<Uid, UniversalAdditionalCameraData> _baseCameraData = new();

		private readonly List<GameObject> _spawnedCameraObjects = new();

		private readonly Dictionary<Uid, List<IGameCamera>> _pendingOverlays = new();

		// Virtual (Cinemachine) cameras
		private readonly List<IVirtualGameCamera> _virtualCameras = new();
		private IVirtualGameCamera _activeVirtualCamera;
		private IVirtualGameCamera _defaultVirtualCamera;
		private CinemachineBrain _brain;

		// Cache of LayerOrder per camera so stack sorting never touches GetComponent in Compare.
		private readonly Dictionary<Camera, int> _layerOrderCache = new();
		private CameraLayerOrderComparer _stackComparer;

		private void Awake()
		{
			_stackComparer = new CameraLayerOrderComparer(_layerOrderCache);
		}

		private void Start()
		{
			SpawnStartupCameras();
		}

		public T Get<T>() where T : class, IGameCamera
		{
			if (_camerasByType.TryGetValue(typeof(T), out var list) && list.Count > 0)
			{
				return list[0] as T;
			}

			// Assignable-type fallback (interfaces, base classes).
			foreach (var camera in _bindOrder)
			{
				if (camera is T match)
				{
					return match;
				}
			}

			return null;
		}

		public IGameCamera GetCamera(Uid<CameraType> cameraType = default)
		{
			// None = "first bound camera". Identities only disambiguate variants.
			if (cameraType.IsNone)
			{
				return _bindOrder.Count > 0 ? _bindOrder[0] : null;
			}

			return _camerasById.GetValueOrDefault(cameraType.Value);
		}

		public IGameCamera GetCamera(CameraType cameraType) => GetCamera(ToId(cameraType));

		public T GetCamera<T>(Uid<CameraType> cameraType = default) where T : class, IGameCamera
		{
			if (cameraType.IsSet)
			{
				return _camerasById.GetValueOrDefault(cameraType.Value) as T;
			}

			// No identity: first bound camera assignable to T.
			foreach (var camera in _bindOrder)
			{
				if (camera is T match)
				{
					return match;
				}
			}

			return null;
		}

		public T GetCamera<T>(CameraType cameraType) where T : class, IGameCamera => GetCamera<T>(ToId(cameraType));

		private static Uid<CameraType> ToId(CameraType cameraType)
		{
			return cameraType != null ? cameraType.IdAs<CameraType>() : default;
		}

		public IReadOnlyList<T> GetCameras<T>() where T : class, IGameCamera
		{
			var result = new List<T>();
			foreach (var camera in _bindOrder)
			{
				if (camera is T match)
				{
					result.Add(match);
				}
			}

			return result;
		}

		public void BindCamera(IGameCamera gameCamera)
		{
			if (gameCamera == null) return;

			var type = gameCamera.GetType();

			if (_camerasByType.TryGetValue(type, out var typeList))
			{
				if (!typeList.Contains(gameCamera))
				{
					Debug.LogWarning($"[CameraSystem] Second camera of type '{type.Name}' bound ('{gameCamera.GameObject.name}'). " +
					                 "First-bound wins type lookups; assign CameraType UIDs to address variants individually.");
					typeList.Add(gameCamera);
				}
			}
			else
			{
				_camerasByType[type] = new List<IGameCamera> { gameCamera };
			}

			if (gameCamera.CameraTypeId.IsSet)
			{
				if (_camerasById.TryGetValue(gameCamera.CameraTypeId.Value, out var existing) && !ReferenceEquals(existing, gameCamera))
				{
					Debug.LogWarning($"[CameraSystem] Duplicate CameraType {UidDebugNames.Describe(gameCamera.CameraTypeId)} on '{gameCamera.GameObject.name}' - keeping the first bound ('{existing.GameObject.name}').");
				}
				else
				{
					_camerasById[gameCamera.CameraTypeId.Value] = gameCamera;
				}
			}

			if (!_bindOrder.Contains(gameCamera))
			{
				_bindOrder.Add(gameCamera);
			}

			if (gameCamera.Camera != null)
			{
				_layerOrderCache[gameCamera.Camera] = gameCamera.LayerOrder;
			}

			if (gameCamera.Role == CameraRole.Virtual)
			{
				BindVirtualCamera((IVirtualGameCamera)gameCamera);
				return;
			}

			if (gameCamera.Role == CameraRole.Base)
			{
				var data = gameCamera.Camera.GetUniversalAdditionalCameraData();
				if (data != null)
				{
					var baseKey = GetBaseCameraKey(gameCamera);
					_baseCameraData[baseKey] = data;

					// Resolve any overlay cameras that were bound before this base camera
					if (_pendingOverlays.TryGetValue(baseKey, out var pending))
					{
						foreach (var overlay in pending)
						{
							AddToStack(baseKey, overlay);
						}

						_pendingOverlays.Remove(baseKey);
					}
				}
			}
			// Overlay Camera: Auto-stack if base exists, otherwise defer
			else if (gameCamera.Role == CameraRole.Overlay)
			{
				Uid<CameraType> baseId = gameCamera.DefaultBaseCameraId;
				if (baseId.IsSet)
				{
					Uid baseKey = baseId.Value;
					if (_baseCameraData.ContainsKey(baseKey))
					{
						AddToStack(baseKey, gameCamera);
					}
					else
					{
						if (!_pendingOverlays.TryGetValue(baseKey, out var list))
						{
							list = new List<IGameCamera>();
							_pendingOverlays[baseKey] = list;
						}

						list.Add(gameCamera);
					}
				}
			}
		}

		public void UnbindCamera(IGameCamera gameCamera)
		{
			if (gameCamera == null) return;

			var type = gameCamera.GetType();
			if (_camerasByType.TryGetValue(type, out var typeList))
			{
				typeList.Remove(gameCamera);
				if (typeList.Count == 0)
				{
					_camerasByType.Remove(type);
				}
			}

			if (gameCamera.CameraTypeId.IsSet)
			{
				// Only remove the mapping if it still points at THIS camera (duplicate binds keep the first).
				if (_camerasById.TryGetValue(gameCamera.CameraTypeId.Value, out var mapped) && ReferenceEquals(mapped, gameCamera))
				{
					_camerasById.Remove(gameCamera.CameraTypeId.Value);
				}
			}

			_bindOrder.Remove(gameCamera);

			if (gameCamera.Camera != null)
			{
				_layerOrderCache.Remove(gameCamera.Camera);
			}

			if (gameCamera.Role == CameraRole.Virtual)
			{
				UnbindVirtualCamera((IVirtualGameCamera)gameCamera);
				return;
			}

			if (gameCamera.Role == CameraRole.Overlay)
			{
				Uid<CameraType> baseId = gameCamera.DefaultBaseCameraId;
				if (baseId.IsSet)
				{
					Uid baseKey = baseId.Value;
					if (_pendingOverlays.TryGetValue(baseKey, out var pending))
					{
						pending.Remove(gameCamera);
						if (pending.Count == 0) _pendingOverlays.Remove(baseKey);
					}

					RemoveFromStack(baseKey, gameCamera);
				}
			}
			else if (gameCamera.Role == CameraRole.Base)
			{
				var baseKey = GetBaseCameraKey(gameCamera);
				_baseCameraData.Remove(baseKey);
				_pendingOverlays.Remove(baseKey);
			}
		}

		public T SpawnCamera<T>(Uid<CameraType> cameraType = default) where T : class, IGameCamera
		{
			if (_cameraRegistry == null)
			{
				Debug.LogError("[CameraSystem] SpawnCamera failed: CameraRegistry is not assigned.");
				return null;
			}

			// None: first definition whose prefab actually carries a T component.
			CameraDefinition definition = cameraType.IsSet
				? _cameraRegistry.GetDefinitionByCameraType(cameraType)
				: FindFirstDefinitionFor<T>();

			if (definition == null || definition.Prefab == null)
			{
				Debug.LogError($"[CameraSystem] SpawnCamera failed: no CameraDefinition for CameraType {UidDebugNames.Describe(cameraType)}.");
				return null;
			}

			return SpawnFromDefinition<T>(definition);
		}

		public T SpawnCamera<T>(CameraType cameraType) where T : class, IGameCamera => SpawnCamera<T>(ToId(cameraType));

		private T SpawnFromDefinition<T>(CameraDefinition definition) where T : class, IGameCamera
		{
			var instance = Instantiate(definition.Prefab, transform);
			instance.name = definition.Prefab.name;
			_spawnedCameraObjects.Add(instance);

			var gameCamera = instance.GetComponent<IGameCamera>();
			if (gameCamera == null)
			{
				Debug.LogError($"[CameraSystem] SpawnCamera failed: Prefab '{definition.Prefab.name}' does not have an IGameCamera component.");
				_spawnedCameraObjects.Remove(instance);
				Destroy(instance);
				return null;
			}

			switch (gameCamera)
			{
				case BaseCamera baseCam:
					baseCam.ApplyDefinition(definition);
					baseCam.BindToSystem(this);
					break;
				case VirtualGameCamera virtualCam:
					virtualCam.ApplyDefinition(definition);
					virtualCam.BindToSystem(this);
					break;
				default:
					BindCamera(gameCamera);
					break;
			}

			return gameCamera as T;
		}

		public void RemoveCamera(Uid<CameraType> cameraType, bool destroy = true)
		{
			var camera = GetCamera(cameraType);
			if (camera == null) return;

			UnbindCamera(camera);

			if (destroy && camera.GameObject != null)
			{
				_spawnedCameraObjects.Remove(camera.GameObject);
				Destroy(camera.GameObject);
			}
		}

		public void RemoveCamera(CameraType cameraType, bool destroy = true) => RemoveCamera(ToId(cameraType), destroy);

		public void EnableCamera<T>(bool enableGameObject = true) where T : class, IGameCamera
		{
			EnableCamera(typeof(T), enableGameObject);
		}

		public void DisableCamera<T>(bool disableGameObject = true) where T : class, IGameCamera
		{
			DisableCamera(typeof(T), disableGameObject);
		}

		public void EnableCamera(Type cameraType, bool enableGameObject = true)
		{
			var camera = GetCameraOfType(cameraType);
			if (camera == null) return;

			if (camera is IVirtualGameCamera virtualCamera)
			{
				ActivateVirtualCamera(explicitCamera: virtualCamera);
				if (enableGameObject) camera.GameObject.SetActive(true);
				return;
			}

			EnablePhysicalCamera(camera, enableGameObject);
		}

		public void DisableCamera(Type cameraType, bool disableGameObject = true)
		{
			var camera = GetCameraOfType(cameraType);
			if (camera == null) return;

			DisablePhysicalOrVirtual(camera, disableGameObject);
		}

		public void EnableCamera(Uid<CameraType> cameraType, bool enableGameObject = true)
		{
			var camera = GetCamera(cameraType);
			if (camera == null) return;

			if (camera is IVirtualGameCamera virtualCamera)
			{
				ActivateVirtualCamera(explicitCamera: virtualCamera);
				if (enableGameObject) camera.GameObject.SetActive(true);
				return;
			}

			EnablePhysicalCamera(camera, enableGameObject);
		}

		public void EnableCamera(CameraType cameraType, bool enableGameObject = true) => EnableCamera(ToId(cameraType), enableGameObject);

		public void DisableCamera(Uid<CameraType> cameraType, bool disableGameObject = true)
		{
			var camera = GetCamera(cameraType);
			if (camera == null) return;

			DisablePhysicalOrVirtual(camera, disableGameObject);
		}

		public void DisableCamera(CameraType cameraType, bool disableGameObject = true) => DisableCamera(ToId(cameraType), disableGameObject);

		private void EnablePhysicalCamera(IGameCamera camera, bool enableGameObject)
		{
			if (camera.Role == CameraRole.Overlay && camera.DefaultBaseCameraId.IsSet)
			{
				AddToStack(camera.DefaultBaseCameraId.Value, camera);
			}

			if (enableGameObject)
			{
				camera.GameObject.SetActive(true);
			}
		}

		private void DisablePhysicalOrVirtual(IGameCamera camera, bool disableGameObject)
		{
			if (camera is IVirtualGameCamera virtualCamera)
			{
				DeactivateVirtualCamera(virtualCamera);
			}
			else if (camera.Role == CameraRole.Overlay && camera.DefaultBaseCameraId.IsSet)
			{
				RemoveFromStack(camera.DefaultBaseCameraId.Value, camera);
			}

			if (disableGameObject)
			{
				camera.GameObject.SetActive(false);
			}
		}

		private IGameCamera GetCameraOfType(Type cameraType)
		{
			if (_camerasByType.TryGetValue(cameraType, out var list) && list.Count > 0)
			{
				return list[0];
			}

			foreach (var camera in _bindOrder)
			{
				if (cameraType.IsInstanceOfType(camera))
				{
					return camera;
				}
			}

			return null;
		}

		public void ReorderCameraStack()
		{
			foreach (var data in _baseCameraData.Values)
			{
				SortStack(data);
			}
		}

		private void AddToStack(Uid baseKey, IGameCamera overlay)
		{
			if (!_baseCameraData.TryGetValue(baseKey, out var baseData)) return;

			if (!baseData.cameraStack.Contains(overlay.Camera))
			{
				baseData.cameraStack.Add(overlay.Camera);
				SortStack(baseData);
			}
		}

		private void RemoveFromStack(Uid baseKey, IGameCamera overlay)
		{
			if (!_baseCameraData.TryGetValue(baseKey, out var baseData)) return;

			if (baseData.cameraStack.Contains(overlay.Camera))
			{
				baseData.cameraStack.Remove(overlay.Camera);
			}
		}

		private void SortStack(UniversalAdditionalCameraData data)
		{
			data.cameraStack.Sort(_stackComparer);
		}

		// =================================================================
		// VIRTUAL CAMERAS (Cinemachine, single-brain priority workflow)
		// =================================================================

		public IVirtualGameCamera ActiveVirtualCamera => _activeVirtualCamera;
		public IVirtualGameCamera DefaultVirtualCamera => _defaultVirtualCamera;

		private void BindVirtualCamera(IVirtualGameCamera camera)
		{
			if (_virtualCameras.Contains(camera)) return;

			_virtualCameras.Add(camera);

			if (camera.VirtualCamera != null)
			{
				// Park at standby priority; GameObjects stay enabled (priority decides, the brain blends).
				camera.VirtualCamera.Priority = camera.BasePriority;
			}

			if (camera.IsDefault && _defaultVirtualCamera == null)
			{
				_defaultVirtualCamera = camera;

				// A default with nothing live takes over immediately (this also covers cold boot).
				if (_activeVirtualCamera == null)
				{
					ActivateVirtualCamera(explicitCamera: camera);
				}
			}
		}

		private void UnbindVirtualCamera(IVirtualGameCamera camera)
		{
			_virtualCameras.Remove(camera);

			if (ReferenceEquals(_defaultVirtualCamera, camera))
			{
				_defaultVirtualCamera = null;
			}

			if (ReferenceEquals(_activeVirtualCamera, camera))
			{
				_activeVirtualCamera = null;

				// Fall back to the default so the brain has somewhere to land.
				if (_defaultVirtualCamera != null)
				{
					ActivateVirtualCamera(explicitCamera: _defaultVirtualCamera);
				}
			}
		}

		public void ActivateVirtualCamera(Uid<CameraType> cameraType = default, IVirtualGameCamera explicitCamera = null)
		{
			var target = ResolveVirtualCamera(cameraType, explicitCamera);
			if (target == null) return;

			foreach (var cam in _virtualCameras)
			{
				if (cam.VirtualCamera == null) continue;
				cam.VirtualCamera.Priority = ReferenceEquals(cam, target)
					? cam.BasePriority + _virtualCameraActiveBoost
					: cam.BasePriority;
			}

			_activeVirtualCamera = target;
		}

		public void ActivateVirtualCamera(CameraType cameraType, IVirtualGameCamera explicitCamera = null)
		{
			ActivateVirtualCamera(ToId(cameraType), explicitCamera);
		}

		public async UniTask<IVirtualGameCamera> ActivateVirtualCameraAsync(Uid<CameraType> cameraType = default, IVirtualGameCamera explicitCamera = null,
		                                                                    CancellationToken ct = default)
		{
			ActivateVirtualCamera(cameraType, explicitCamera);
			await WaitForCameraBlendAsync(ct);
			return _activeVirtualCamera;
		}

		public void DeactivateVirtualCamera(IVirtualGameCamera camera)
		{
			if (camera?.VirtualCamera == null) return;

			camera.VirtualCamera.Priority = camera.BasePriority;

			if (ReferenceEquals(_activeVirtualCamera, camera))
			{
				_activeVirtualCamera = null;

				// Smooth hand-over to the default camera, if one exists.
				if (_defaultVirtualCamera != null && !ReferenceEquals(_defaultVirtualCamera, camera))
				{
					ActivateVirtualCamera(explicitCamera: _defaultVirtualCamera);
				}
			}
		}

		public bool ActivateDefaultVirtualCamera()
		{
			if (_defaultVirtualCamera == null)
			{
				Debug.LogWarning("[CameraSystem] No default virtual camera set.");
				return false;
			}

			ActivateVirtualCamera(explicitCamera: _defaultVirtualCamera);
			return true;
		}

		public async UniTask WaitForCameraBlendAsync(CancellationToken ct = default)
		{
			var brain = GetBrain();
			if (brain == null)
			{
				return;
			}

			// Let Cinemachine process the priority change first.
			await UniTask.Yield(ct);

			while (brain != null && brain.IsBlending)
			{
				await UniTask.Yield(ct);
			}
		}

		private IVirtualGameCamera ResolveVirtualCamera(Uid<CameraType> cameraType, IVirtualGameCamera explicitCamera)
		{
			if (explicitCamera != null) return explicitCamera;

			if (cameraType.IsSet)
			{
				if (_camerasById.TryGetValue(cameraType.Value, out var camera))
				{
					if (camera is IVirtualGameCamera virtualCamera) return virtualCamera;

					Debug.LogWarning($"[CameraSystem] Camera {UidDebugNames.Describe(cameraType)} is bound but is not a virtual camera.");
					return null;
				}

				Debug.LogWarning($"[CameraSystem] No camera bound for CameraType {UidDebugNames.Describe(cameraType)}.");
				return null;
			}

			// None: default first, otherwise the first bound virtual camera.
			return _defaultVirtualCamera ?? (_virtualCameras.Count > 0 ? _virtualCameras[0] : null);
		}

		private CinemachineBrain GetBrain()
		{
			if (_brain != null) return _brain;

			// Prefer the brain of a bound Cinemachine base camera; fall back to any active brain.
			var brainCamera = GetCamera<ICinemachineGameCamera>();
			_brain = (brainCamera != null && brainCamera.Brain != null)
				? brainCamera.Brain
				: CinemachineBrain.GetActiveBrain(0);

			return _brain;
		}

		private CameraDefinition FindFirstDefinitionFor<T>() where T : class, IGameCamera
		{
			foreach (var def in _cameraRegistry.Objects)
			{
				if (def == null || def.Prefab == null) continue;

				if (typeof(T) == typeof(IGameCamera) || def.Prefab.GetComponent<T>() != null)
				{
					return def;
				}
			}

			return null;
		}

		private void SpawnStartupCameras()
		{
			if (_cameraRegistry == null) return;

			// Pre-scan the scene once: a pre-placed scene camera with the same CameraType must
			// suppress the startup spawn. Both bind in Start, so dictionary checks alone race.
			BaseCamera[] sceneCameras = null;
			VirtualGameCamera[] sceneVirtualCameras = null;

			foreach (var def in _cameraRegistry.Objects)
			{
				if (def == null || !def.SpawnOnStart || def.Prefab == null) continue;

				var hasType = def.CameraType != null && def.CameraType.HasIdentity;

				// Already bound (e.g., a scene camera whose Start ran first)?
				if (hasType && _camerasById.ContainsKey(def.CameraType.Id)) continue;

				// Pre-placed in the scene but not yet bound (Start order not guaranteed)?
				if (hasType && SceneHasCameraWithType(def.CameraType, ref sceneCameras, ref sceneVirtualCameras)) continue;

				// Spawn THIS definition (a type-less definition must not resolve to "first in registry").
				SpawnFromDefinition<IGameCamera>(def);
			}
		}

		private static bool SceneHasCameraWithType(CameraType cameraType, ref BaseCamera[] sceneCameras,
			ref VirtualGameCamera[] sceneVirtualCameras)
		{
			sceneCameras ??= FindObjectsByType<BaseCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			sceneVirtualCameras ??= FindObjectsByType<VirtualGameCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);

			Uid<CameraType> wanted = cameraType.IdAs<CameraType>();

			foreach (var cam in sceneCameras)
			{
				if (cam != null && cam.CameraTypeId.IsSet && cam.CameraTypeId == wanted)
				{
					return true;
				}
			}

			foreach (var cam in sceneVirtualCameras)
			{
				if (cam != null && cam.CameraTypeId.IsSet && cam.CameraTypeId == wanted)
				{
					return true;
				}
			}

			return false;
		}

		// Base cameras without a CameraType still need a stable stack key. Deriving one from
		// the concrete type name keeps the map a pure Uid keyspace while preserving the old
		// "one base per type" behaviour for unaddressed cameras.
		private static readonly Uid TypeKeyNamespace = Uid.Parse("6f2a9c4e1b3d4a7f9e8c2b1d5a6f7e8c");

		private static Uid GetBaseCameraKey(IGameCamera camera)
		{
			return camera.CameraTypeId.IsSet
				? camera.CameraTypeId.Value
				: Uid.Deterministic(TypeKeyNamespace, camera.GetType().FullName);
		}

		public void Dispose()
		{
			foreach (var obj in _spawnedCameraObjects)
			{
				if (obj != null) Destroy(obj);
			}

			_spawnedCameraObjects.Clear();
			_pendingOverlays.Clear();
			_virtualCameras.Clear();
			_activeVirtualCamera = null;
			_defaultVirtualCamera = null;
			_brain = null;

			Destroy(gameObject);
		}
	}
}
