using System;
using System.Collections;
using System.Collections.Generic;
using AK.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AK.Systems
{
    public class CameraSystem : GameEntity, ICameraSystem
    {
        private readonly Dictionary<Type, IGameCamera> _cameras = new();
        
        // Cache URP Data for Base cameras
        private readonly Dictionary<Type, UniversalAdditionalCameraData> _baseCameraData = new();

        public T Get<T>() where T : class, IGameCamera
        {
            return _cameras.GetValueOrDefault(typeof(T)) as T;
        }

        public void BindCamera(IGameCamera gameCamera)
        {
            var type = gameCamera.GetType();
            _cameras[type] = gameCamera;

            // 1. Base Camera: Register data
            if (gameCamera.Role == CameraRole.Base)
            {
                var data = gameCamera.Camera.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    _baseCameraData[type] = data;
                }
            }
            // 2. Overlay Camera: Auto-stack if parent exists
            else if (gameCamera.Role == CameraRole.Overlay)
            {
                var parentType = gameCamera.DefaultBaseCameraType;
                if (parentType != null && _baseCameraData.ContainsKey(parentType))
                {
                    AddToStack(parentType, gameCamera);
                }
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
            var camera = _cameras.GetValueOrDefault(cameraType);
            if (camera == null) return;

            // Ensure it's in the stack if it's an overlay
            if (camera.Role == CameraRole.Overlay && camera.DefaultBaseCameraType != null)
            {
                AddToStack(camera.DefaultBaseCameraType, camera);
            }
            
            if (enableGameObject)
            {
                camera.GameObject.SetActive(true);
            }
        }

        public void DisableCamera(Type cameraType, bool disableGameObject = true)
        {
            var camera = _cameras.GetValueOrDefault(cameraType);
            if (camera == null) return;

            // Remove from stack to save performance
            if (camera.Role == CameraRole.Overlay && camera.DefaultBaseCameraType != null)
            {
                RemoveFromStack(camera.DefaultBaseCameraType, camera);
            }
            
            if (disableGameObject)
            {
                camera.GameObject.SetActive(false);
            }
        }

        // --- Stack Management ---

        public void ReorderCameraStack()
        {
            // Iterate over ALL base cameras and sort their stacks
            foreach (var data in _baseCameraData.Values)
            {
                SortStack(data);
            }
        }

        private void AddToStack(Type baseType, IGameCamera overlay)
        {
            if (!_baseCameraData.TryGetValue(baseType, out var baseData)) return;

            if (!baseData.cameraStack.Contains(overlay.Camera))
            {
                baseData.cameraStack.Add(overlay.Camera);
                SortStack(baseData);
            }
        }

        private void RemoveFromStack(Type baseType, IGameCamera overlay)
        {
            if (!_baseCameraData.TryGetValue(baseType, out var baseData)) return;

            if (baseData.cameraStack.Contains(overlay.Camera))
            {
                baseData.cameraStack.Remove(overlay.Camera);
            }
        }

        private void SortStack(UniversalAdditionalCameraData data)
        {
            data.cameraStack.Sort(new CameraLayerOrderComparer());
        }

        public void Dispose()
        {
            Destroy(gameObject);
        }
    }
}