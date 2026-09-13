using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using Cysharp.Threading.Tasks;

namespace AK.Systems
{
    /// <summary>
    /// Camera identities are OPTIONAL throughout: <see cref="Uid{T}.None"/> means "the first
    /// bound camera" (or first assignable to T). Pass an identity only to pick a specific
    /// variant when several cameras share a type.
    /// </summary>
    public interface ICameraSystem
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
        /// Called automatically when a BaseCamera is destroyed.
        /// </summary>
        void UnbindCamera(IGameCamera gameCamera);

        /// <summary>
        /// Spawn a camera from the CameraRegistry. With an identity, the matching
        /// CameraDefinition is used; with None, the first definition whose prefab has a
        /// <typeparamref name="T"/> component is used.
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

        // =================================================================
        // VIRTUAL CAMERAS (Cinemachine, single-brain priority workflow)
        // =================================================================

        /// <summary>The currently live virtual camera, or null if none is active.</summary>
        IVirtualGameCamera ActiveVirtualCamera { get; }

        /// <summary>The virtual camera marked as default (fallback), or null if none.</summary>
        IVirtualGameCamera DefaultVirtualCamera { get; }

        /// <summary>
        /// Makes a virtual camera live: its priority is boosted above all others and the (single)
        /// Cinemachine brain blends to it. None activates the default camera.
        /// </summary>
        /// <param name="explicitCamera">Skip lookup entirely and activate this exact instance.</param>
        void ActivateVirtualCamera(Uid<CameraType> cameraType = default, IVirtualGameCamera explicitCamera = null);
        void ActivateVirtualCamera(CameraType cameraType, IVirtualGameCamera explicitCamera = null);

        /// <summary>Makes a virtual camera live and awaits until the brain's blend to it completes.</summary>
        UniTask<IVirtualGameCamera> ActivateVirtualCameraAsync(Uid<CameraType> cameraType = default, IVirtualGameCamera explicitCamera = null,
                                                               CancellationToken ct = default);

        /// <summary>
        /// Demotes a virtual camera back to standby priority. If it was live, the default
        /// camera (if any) takes over with a smooth blend.
        /// </summary>
        void DeactivateVirtualCamera(IVirtualGameCamera camera);

        /// <summary>Activates the default virtual camera, if one is registered.</summary>
        bool ActivateDefaultVirtualCamera();

        /// <summary>Awaits until the active Cinemachine brain finishes its current blend (or returns immediately if none).</summary>
        UniTask WaitForCameraBlendAsync(CancellationToken ct = default);

        void ReorderCameraStack();

        void Dispose();
    }
}
