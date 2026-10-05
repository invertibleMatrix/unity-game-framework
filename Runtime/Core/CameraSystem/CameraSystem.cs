using System;
using System.Collections.Generic;
using AK.Core;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;

namespace AK.Systems
{
	// Virtual cameras need Cinemachine, and live in CameraSystem.Cinemachine.cs. Overlay stacks
	// are URP's, which CameraStack wraps; without URP, nothing stacks.
	public partial class CameraSystem : GameEntity, ICameraSystem
	{
		[SerializeField]
		private CameraRegistry _cameraRegistry;

		// Injects the cameras this system spawns.
		[Inject] private Container _container;

		// Multiple cameras may share a concrete type (prefab variants) - keep them all, first-bound wins lookups.
		private readonly Dictionary<Type, List<IGameCamera>> _camerasByType = new();

		private readonly Dictionary<Uid, IGameCamera> _camerasById = new();

		// Insertion-ordered list backing the "first bound camera" fallback for None identities.
		private readonly List<IGameCamera> _bindOrder = new();

		// The bound base cameras by stack key, each with the camera data that holds its stack.
		private readonly Dictionary<Uid, BaseStack> _baseStacks = new();

		private readonly List<GameObject> _spawnedCameraObjects = new();

		// Enabled overlays waiting for their base camera to bind, by the base's key.
		private readonly Dictionary<Uid, List<IGameCamera>> _pendingOverlays = new();

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

			if (gameCamera.Role == CameraRole.Virtual)
			{
				BindVirtualCamera(gameCamera);
				return;
			}

			// Not for virtual cameras: their Camera is the brain's, which has an order of its own.
			if (gameCamera.Camera != null)
			{
				_layerOrderCache[gameCamera.Camera] = gameCamera.LayerOrder;
			}

			if (gameCamera.Role == CameraRole.Base)
			{
				if (CameraStack.TryGet(gameCamera.Camera, out CameraStack stack))
				{
					var baseKey = GetBaseCameraKey(gameCamera);
					_baseStacks[baseKey] = new BaseStack(gameCamera, stack);

					// Resolve any overlay cameras that were bound before this base camera
					if (_pendingOverlays.TryGetValue(baseKey, out var pending))
					{
						_pendingOverlays.Remove(baseKey);
						foreach (var overlay in pending)
						{
							AddToStack(baseKey, overlay);
						}
					}
				}
			}
			// Overlay Camera: Auto-stack if base exists, otherwise defer
			else if (IsStackedOverlay(gameCamera))
			{
				StackOrPend(gameCamera);
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

			if (gameCamera.Role == CameraRole.Virtual)
			{
				UnbindVirtualCamera(gameCamera);
				return;
			}

			// A reference check: the camera may be destroyed already, and its entry must still go.
			if (gameCamera.Camera is { } unboundCamera)
			{
				_layerOrderCache.Remove(unboundCamera);
			}

			if (IsStackedOverlay(gameCamera))
			{
				UnstackAndUnpend(gameCamera);
			}
			else if (gameCamera.Role == CameraRole.Base)
			{
				var baseKey = GetBaseCameraKey(gameCamera);

				// Only the base the key maps to: a second base of the same kind replaced it.
				if (_baseStacks.TryGetValue(baseKey, out BaseStack stack) && ReferenceEquals(stack.Owner, gameCamera))
				{
					_baseStacks.Remove(baseKey);
					PendOverlaysOf(baseKey, stack.Stack);
				}
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

			var gameCamera = instance.GetComponent<IGameCamera>();
			if (gameCamera == null)
			{
				Debug.LogError($"[CameraSystem] SpawnCamera failed: Prefab '{definition.Prefab.name}' does not have an IGameCamera component.");
				Destroy(instance);
				return null;
			}

			_spawnedCameraObjects.Add(instance);

			// Injected as a scene camera is: after its Awake, before its Start.
			if (_container != null)
			{
				GameObjectInjector.InjectRecursive(instance, _container);
			}

			if (gameCamera is ISpawnableCamera spawnable)
			{
				spawnable.ApplyDefinition(definition);
				spawnable.BindToSystem(this);
			}
			else
			{
				BindCamera(gameCamera);
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
			EnableCamera(GetCameraOfType(cameraType), enableGameObject);
		}

		public void DisableCamera(Type cameraType, bool disableGameObject = true)
		{
			DisableCamera(GetCameraOfType(cameraType), disableGameObject);
		}

		public void EnableCamera(Uid<CameraType> cameraType, bool enableGameObject = true)
		{
			EnableCamera(GetCamera(cameraType), enableGameObject);
		}

		public void EnableCamera(CameraType cameraType, bool enableGameObject = true) => EnableCamera(ToId(cameraType), enableGameObject);

		public void DisableCamera(Uid<CameraType> cameraType, bool disableGameObject = true)
		{
			DisableCamera(GetCamera(cameraType), disableGameObject);
		}

		public void DisableCamera(CameraType cameraType, bool disableGameObject = true) => DisableCamera(ToId(cameraType), disableGameObject);

		public void EnableCamera(IGameCamera camera, bool enableGameObject = true)
		{
			if (camera == null) return;

			if (camera.Role == CameraRole.Virtual)
			{
				EnableVirtualCamera(camera);
			}
			else if (IsStackedOverlay(camera))
			{
				StackOrPend(camera);
			}

			if (enableGameObject)
			{
				camera.GameObject.SetActive(true);
			}
		}

		public void DisableCamera(IGameCamera camera, bool disableGameObject = true)
		{
			if (camera == null) return;

			if (camera.Role == CameraRole.Virtual)
			{
				DisableVirtualCamera(camera);
			}
			else if (IsStackedOverlay(camera))
			{
				UnstackAndUnpend(camera);
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
			foreach (var stack in _baseStacks.Values)
			{
				SortStack(stack.Stack);
			}
		}

		// An overlay that names its base camera. One that doesn't is stacked by hand, if at all.
		private static bool IsStackedOverlay(IGameCamera camera)
		{
			return camera.Role == CameraRole.Overlay && camera.DefaultBaseCameraId.IsSet;
		}

		// An enabled overlay is on its base camera's stack, or waits for its base camera to bind.
		private void StackOrPend(IGameCamera overlay)
		{
			Uid baseKey = overlay.DefaultBaseCameraId.Value;
			if (_baseStacks.ContainsKey(baseKey))
			{
				AddToStack(baseKey, overlay);
				return;
			}

			if (!_pendingOverlays.TryGetValue(baseKey, out var pending))
			{
				pending = new List<IGameCamera>();
				_pendingOverlays[baseKey] = pending;
			}

			if (!pending.Contains(overlay))
			{
				pending.Add(overlay);
			}
		}

		private void UnstackAndUnpend(IGameCamera overlay)
		{
			Uid baseKey = overlay.DefaultBaseCameraId.Value;
			if (_pendingOverlays.TryGetValue(baseKey, out var pending) && pending.Remove(overlay) && pending.Count == 0)
			{
				_pendingOverlays.Remove(baseKey);
			}

			RemoveFromStack(baseKey, overlay);
		}

		// The overlays on the stack of a base camera that unbinds wait for the next base camera of
		// its kind. A destroyed camera's stack can't be read; then every overlay of its kind waits.
		private void PendOverlaysOf(Uid baseKey, CameraStack stack)
		{
			List<Camera> stacked = stack.Cameras;

			foreach (var camera in _bindOrder)
			{
				if (!IsStackedOverlay(camera) || camera.DefaultBaseCameraId.Value != baseKey) continue;

				// Not on the stack: it was disabled, and stays so.
				if (stacked != null && !stacked.Remove(camera.Camera)) continue;

				StackOrPend(camera);
			}
		}

		private void AddToStack(Uid baseKey, IGameCamera overlay)
		{
			if (!_baseStacks.TryGetValue(baseKey, out var baseStack)) return;

			var cameraStack = baseStack.Stack.Cameras;
			if (cameraStack != null && !cameraStack.Contains(overlay.Camera))
			{
				cameraStack.Add(overlay.Camera);
				cameraStack.Sort(_stackComparer);
			}
		}

		private void RemoveFromStack(Uid baseKey, IGameCamera overlay)
		{
			if (!_baseStacks.TryGetValue(baseKey, out var baseStack)) return;

			baseStack.Stack.Cameras?.Remove(overlay.Camera);
		}

		private void SortStack(CameraStack stack)
		{
			stack.Cameras?.Sort(_stackComparer);
		}

		// Implemented in CameraSystem.Cinemachine.cs. Without Cinemachine, a virtual camera is
		// bound by type and identity only.
		partial void BindVirtualCamera(IGameCamera gameCamera);

		partial void UnbindVirtualCamera(IGameCamera gameCamera);

		partial void EnableVirtualCamera(IGameCamera gameCamera);

		partial void DisableVirtualCamera(IGameCamera gameCamera);

		partial void ClearVirtualCameras();

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
			List<IGameCamera> sceneCameras = null;

			foreach (var def in _cameraRegistry.Objects)
			{
				if (def == null || !def.SpawnOnStart || def.Prefab == null) continue;

				var hasType = def.CameraType != null && def.CameraType.HasIdentity;

				// Already bound (e.g., a scene camera whose Start ran first)?
				if (hasType && _camerasById.ContainsKey(def.CameraType.Id)) continue;

				// Pre-placed in the scene but not yet bound (Start order not guaranteed)?
				if (hasType && SceneHasCameraWithType(def.CameraType, ref sceneCameras)) continue;

				// Spawn THIS definition (a type-less definition must not resolve to "first in registry").
				SpawnFromDefinition<IGameCamera>(def);
			}
		}

		// Every kind of camera, generic ones included: anything in the scene that is an IGameCamera.
		private static bool SceneHasCameraWithType(CameraType cameraType, ref List<IGameCamera> sceneCameras)
		{
			if (sceneCameras == null)
			{
				sceneCameras = new List<IGameCamera>();
				foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				{
					if (behaviour is IGameCamera camera)
					{
						sceneCameras.Add(camera);
					}
				}
			}

			Uid<CameraType> wanted = cameraType.IdAs<CameraType>();

			foreach (var cam in sceneCameras)
			{
				if (cam.CameraTypeId.IsSet && cam.CameraTypeId == wanted)
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
			ClearVirtualCameras();

			Destroy(gameObject);
		}

		private readonly struct BaseStack
		{
			public readonly IGameCamera Owner;
			public readonly CameraStack Stack;

			public BaseStack(IGameCamera owner, CameraStack stack)
			{
				Owner = owner;
				Stack = stack;
			}
		}
	}
}
