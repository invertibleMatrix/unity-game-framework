using System;
using AK.Core;

namespace AK.Systems
{
    public interface ICameraSystem
    {
        T Get<T>() where T : class, IGameCamera;

        /// <summary>
        /// Get a camera by its CameraType UID.
        /// </summary>
        IGameCamera GetCamera(UID cameraTypeUID);

        void BindCamera(IGameCamera gameCamera);

        /// <summary>
        /// Unbind a camera from the system. Removes from internal dictionaries and URP stack.
        /// Called automatically when a BaseCamera is destroyed.
        /// </summary>
        void UnbindCamera(IGameCamera gameCamera);

        /// <summary>
        /// Spawn a camera from the CameraRegistry by CameraType UID.
        /// Instantiates the prefab, binds it to the system, and returns the IGameCamera.
        /// </summary>
        T SpawnCamera<T>(UID cameraTypeUID) where T : class, IGameCamera;

        /// <summary>
        /// Remove a camera by CameraType UID. Unbinds and optionally destroys the GameObject.
        /// </summary>
        void RemoveCamera(UID cameraTypeUID, bool destroy = true);

        void EnableCamera<T>(bool enableGameObject = true) where T : class, IGameCamera;
        void DisableCamera<T>(bool disableGameObject = true) where T : class, IGameCamera;

        void EnableCamera(Type cameraType, bool enableGameObject = true);
        void DisableCamera(Type cameraType, bool disableGameObject = true);

        /// <summary>
        /// Enable a camera by its CameraType UID.
        /// </summary>
        void EnableCamera(UID cameraTypeUID, bool enableGameObject = true);

        /// <summary>
        /// Disable a camera by its CameraType UID.
        /// </summary>
        void DisableCamera(UID cameraTypeUID, bool disableGameObject = true);

        void ReorderCameraStack();

        void Dispose();
    }
}
