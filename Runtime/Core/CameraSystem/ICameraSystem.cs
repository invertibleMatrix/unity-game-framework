using System;
using System.Collections.Generic;
using AK.Core;

namespace AK.Systems
{
    /// <summary>
    /// Camera identities are OPTIONAL throughout: <see cref="Uid{T}.None"/> means "the first
    /// bound camera" (or first assignable to T). Pass an identity only to pick a specific
    /// variant when several cameras share a type.
    /// <para>Virtual cameras need Cinemachine 3, and ICameraSystem.Cinemachine.cs declares them.
    /// Overlays stack on their base cameras with URP only; without it, each camera renders on
    /// its own.</para>
    /// </summary>
    public partial interface ICameraSystem
    {
        T Get<T>() where T : class, IGameCamera;

        IGameCamera GetCamera(Uid<CameraType> cameraType = default);
        IGameCamera GetCamera(CameraType cameraType);

        T GetCamera<T>(Uid<CameraType> cameraType = default) where T : class, IGameCamera;
        T GetCamera<T>(CameraType cameraType) where T : class, IGameCamera;

        /// <summary>All bound cameras assignable to <typeparamref name="T"/>.</summary>
        IReadOnlyList<T> GetCameras<T>() where T : class, IGameCamera;

        void BindCamera(IGameCamera gameCamera);

        /// <summary>
        /// Unbind a camera from the system. Removes from internal dictionaries and URP stack.
        /// Called automatically when a BaseCamera is destroyed. The overlays on an unbound base
        /// camera's stack wait for the next base camera of its kind.
        /// </summary>
        void UnbindCamera(IGameCamera gameCamera);

        /// <summary>
        /// Spawn a camera from the CameraRegistry. With an identity, the matching
        /// CameraDefinition is used; with None, the first definition whose prefab has a
        /// <typeparamref name="T"/> component is used. The instance is injected, takes the
        /// definition's configuration when it is an <see cref="ISpawnableCamera"/>, and is bound
        /// before this returns.
        /// </summary>
        T SpawnCamera<T>(Uid<CameraType> cameraType = default) where T : class, IGameCamera;
        T SpawnCamera<T>(CameraType cameraType) where T : class, IGameCamera;

        void RemoveCamera(Uid<CameraType> cameraType, bool destroy = true);
        void RemoveCamera(CameraType cameraType, bool destroy = true);

        void EnableCamera<T>(bool enableGameObject = true) where T : class, IGameCamera;
        void DisableCamera<T>(bool disableGameObject = true) where T : class, IGameCamera;

        void EnableCamera(Type cameraType, bool enableGameObject = true);
        void DisableCamera(Type cameraType, bool disableGameObject = true);

        /// <summary>Virtual cameras are ACTIVATED (priority boost) instead of merely enabled.</summary>
        void EnableCamera(Uid<CameraType> cameraType, bool enableGameObject = true);
        void EnableCamera(CameraType cameraType, bool enableGameObject = true);

        /// <summary>Virtual cameras are demoted to standby (the default camera takes over with a smooth blend).</summary>
        void DisableCamera(Uid<CameraType> cameraType, bool disableGameObject = true);
        void DisableCamera(CameraType cameraType, bool disableGameObject = true);

        /// <summary>
        /// Enables <paramref name="camera"/> itself, not the first camera of its type: a virtual
        /// camera is made live, and an overlay goes on its base camera's stack, or waits for its
        /// base camera to bind.
        /// </summary>
        void EnableCamera(IGameCamera camera, bool enableGameObject = true);

        /// <summary>
        /// Disables <paramref name="camera"/> itself: a virtual camera is demoted to standby, and
        /// an overlay leaves its base camera's stack and stops waiting for one.
        /// </summary>
        void DisableCamera(IGameCamera camera, bool disableGameObject = true);

        /// <summary>Sorts every base camera's stack by layer order. Does nothing without URP.</summary>
        void ReorderCameraStack();

        void Dispose();
    }
}
