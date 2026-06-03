using System;
using System.Collections.Generic;
using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AK.Systems
{
	public class CameraSystem : GameEntity, ICameraSystem
	{
		[InlineEditor] [SerializeField]
		private CameraRegistry _cameraRegistry;

		private readonly Dictionary<Type, IGameCamera> _camerasByType = new();

		private readonly Dictionary<string, IGameCamera> _camerasByUID = new();

		private readonly Dictionary<string, UniversalAdditionalCameraData> _baseCameraData = new();

		private readonly List<GameObject> _spawnedCameraObjects = new();

		private readonly Dictionary<string, List<IGameCamera>> _pendingOverlays = new();

		private void Awake()
		{
			if (_cameraRegistry != null)
			{
				_cameraRegistry.Initialize();
			}
		}

		private void Start()
		{
			SpawnStartupCameras();
		}

		public T Get<T>() where T : class, IGameCamera
		{
			return _camerasByType.GetValueOrDefault(typeof(T)) as T;
		}

		public IGameCamera GetCamera(UID cameraTypeUID)
		{
			if (cameraTypeUID == null || cameraTypeUID.IsEmpty()) return null;
			return _camerasByUID.GetValueOrDefault(cameraTypeUID.Id);
		}

		public void BindCamera(IGameCamera gameCamera)
		{
			if (gameCamera == null) return;

			var type = gameCamera.GetType();
			_camerasByType[type] = gameCamera;

			if (gameCamera.CameraTypeUID != null && !gameCamera.CameraTypeUID.IsEmpty())
			{
				_camerasByUID[gameCamera.CameraTypeUID.Id] = gameCamera;
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
			// 2. Overlay Camera: Auto-stack if base exists, otherwise defer
			else if (gameCamera.Role == CameraRole.Overlay)
			{
				var baseUID = gameCamera.DefaultBaseCameraUID;
				if (baseUID != null && !baseUID.IsEmpty())
				{
					var baseKey = baseUID.Id;
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
			_camerasByType.Remove(type);

			if (gameCamera.CameraTypeUID != null && !gameCamera.CameraTypeUID.IsEmpty())
			{
				_camerasByUID.Remove(gameCamera.CameraTypeUID.Id);
			}

			if (gameCamera.Role == CameraRole.Overlay)
			{
				var baseUID = gameCamera.DefaultBaseCameraUID;
				if (baseUID != null && !baseUID.IsEmpty())
				{
					var baseKey = baseUID.Id;
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

		public T SpawnCamera<T>(UID cameraTypeUID) where T : class, IGameCamera
		{
			if (_cameraRegistry == null)
			{
				Debug.LogError("SpawnCamera failed: CameraRegistry is not assigned.");
				return null;
			}

			var definition = _cameraRegistry.GetDefinitionByCameraType(cameraTypeUID as CameraType);
			if (definition == null || definition.Prefab == null)
			{
				Debug.LogError($"SpawnCamera failed: No CameraDefinition found for CameraType '{cameraTypeUID}'.");
				return null;
			}

			var instance = Instantiate(definition.Prefab, transform);
			instance.name = definition.Prefab.name;
			_spawnedCameraObjects.Add(instance);

			var gameCamera = instance.GetComponent<IGameCamera>();
			if (gameCamera == null)
			{
				Debug.LogError($"SpawnCamera failed: Prefab '{definition.Prefab.name}' does not have an IGameCamera component.");
				Destroy(instance);
				return null;
			}

			if (gameCamera is BaseCamera baseCam)
			{
				baseCam.ApplyDefinition(definition);
				baseCam.BindToSystem(this);
			}
			else
			{
				BindCamera(gameCamera);
			}

			return gameCamera as T;
		}

		public void RemoveCamera(UID cameraTypeUID, bool destroy = true)
		{
			var camera = GetCamera(cameraTypeUID);
			if (camera == null) return;

			UnbindCamera(camera);

			if (destroy && camera.GameObject != null)
			{
				_spawnedCameraObjects.Remove(camera.GameObject);
				Destroy(camera.GameObject);
			}
		}

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
			var camera = _camerasByType.GetValueOrDefault(cameraType);
			if (camera == null) return;

			if (camera.Role == CameraRole.Overlay && camera.DefaultBaseCameraUID != null)
			{
				AddToStack(camera.DefaultBaseCameraUID.Id, camera);
			}

			if (enableGameObject)
			{
				camera.GameObject.SetActive(true);
			}
		}

		public void DisableCamera(Type cameraType, bool disableGameObject = true)
		{
			var camera = _camerasByType.GetValueOrDefault(cameraType);
			if (camera == null) return;

			if (camera.Role == CameraRole.Overlay && camera.DefaultBaseCameraUID != null)
			{
				RemoveFromStack(camera.DefaultBaseCameraUID.Id, camera);
			}

			if (disableGameObject)
			{
				camera.GameObject.SetActive(false);
			}
		}

		public void EnableCamera(UID cameraTypeUID, bool enableGameObject = true)
		{
			var camera = GetCamera(cameraTypeUID);
			if (camera == null) return;

			if (camera.Role == CameraRole.Overlay && camera.DefaultBaseCameraUID != null)
			{
				AddToStack(camera.DefaultBaseCameraUID.Id, camera);
			}

			if (enableGameObject)
			{
				camera.GameObject.SetActive(true);
			}
		}

		public void DisableCamera(UID cameraTypeUID, bool disableGameObject = true)
		{
			var camera = GetCamera(cameraTypeUID);
			if (camera == null) return;

			if (camera.Role == CameraRole.Overlay && camera.DefaultBaseCameraUID != null)
			{
				RemoveFromStack(camera.DefaultBaseCameraUID.Id, camera);
			}

			if (disableGameObject)
			{
				camera.GameObject.SetActive(false);
			}
		}

		public void ReorderCameraStack()
		{
			foreach (var data in _baseCameraData.Values)
			{
				SortStack(data);
			}
		}

		private void AddToStack(string baseKey, IGameCamera overlay)
		{
			if (!_baseCameraData.TryGetValue(baseKey, out var baseData)) return;

			if (!baseData.cameraStack.Contains(overlay.Camera))
			{
				baseData.cameraStack.Add(overlay.Camera);
				SortStack(baseData);
			}
		}

		private void RemoveFromStack(string baseKey, IGameCamera overlay)
		{
			if (!_baseCameraData.TryGetValue(baseKey, out var baseData)) return;

			if (baseData.cameraStack.Contains(overlay.Camera))
			{
				baseData.cameraStack.Remove(overlay.Camera);
			}
		}

		private void SortStack(UniversalAdditionalCameraData data)
		{
			data.cameraStack.Sort(new CameraLayerOrderComparer());
		}

		private void SpawnStartupCameras()
		{
			if (_cameraRegistry == null) return;

			foreach (var def in _cameraRegistry.GetAllObjects())
			{
				if (def != null && def.SpawnOnStart && def.Prefab != null)
				{
					// Only spawn if not already bound (e.g., pre-placed in scene)
					if (def.CameraType != null && !def.CameraType.IsEmpty() && !_camerasByUID.ContainsKey(def.CameraType.Id))
					{
						SpawnCamera<IGameCamera>(def.CameraType);
					}
				}
			}
		}

		private string GetBaseCameraKey(IGameCamera camera)
		{
			if (camera.CameraTypeUID != null && !camera.CameraTypeUID.IsEmpty())
			{
				return camera.CameraTypeUID.Id;
			}

			return camera.GetType().Name;
		}

		public void Dispose()
		{
			foreach (var obj in _spawnedCameraObjects)
			{
				if (obj != null) Destroy(obj);
			}

			_spawnedCameraObjects.Clear();
			_pendingOverlays.Clear();

			Destroy(gameObject);
		}
	}
}