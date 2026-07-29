using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using Cysharp.Threading.Tasks;

namespace AK.Systems
{
    public interface ICameraSystem
    {
        T Get<T>() where T : class, IGameCamera;

        /// <summary>
        /// Get a camera by its CameraType UID.
        /// The UID is OPTIONAL: pass null (or an empty UID) to get the first bound camera.
        /// Use a UID only to pick a specific variant when several cameras share a type.
        /// </summary>
        IGameCamera GetCamera(UID cameraTypeUID = null);

        /// <summary>
        /// Get the first bound camera assignable to <typeparamref name="T"/>.
        /// Pass a UID only to disambiguate between multiple variants of the same type.
        /// </summary>
        T GetCamera<T>(UID cameraTypeUID = null) where T : class, IGameCamera;

        /// <summary>All bound cameras assignable to <typeparamref name="T"/>.</summary>
        IReadOnlyList<T> GetCameras<T>() where T : class, IGameCamera;

        void BindCamera(IGameCamera gameCamera);

        /// <summary>
        /// Unbind a camera from the system. Removes from internal dictionaries and URP stack.
        /// Called automatically when a BaseCamera is destroyed.
        /// </summary>
        void UnbindCamera(IGameCamera gameCamera);

        /// <summary>
        /// Spawn a camera from the CameraRegistry.
        /// The UID is OPTIONAL: with a UID, the matching CameraDefinition is used; with null,
        /// the first definition whose prefab has a <typeparamref name="T"/> component is used.
        /// Instantiates the prefab, binds it to the system, and returns the IGameCamera.
        /// </summary>
        T SpawnCamera<T>(UID cameraTypeUID = null) where T : class, IGameCamera;

        /// <summary>
        /// Remove a camera by CameraType UID (null = first bound camera).
        /// Unbinds and optionally destroys the GameObject.
        /// </summary>
        void RemoveCamera(UID cameraTypeUID, bool destroy = true);

        void EnableCamera<T>(bool enableGameObject = true) where T : class, IGameCamera;
        void DisableCamera<T>(bool disableGameObject = true) where T : class, IGameCamera;

        void EnableCamera(Type cameraType, bool enableGameObject = true);
        void DisableCamera(Type cameraType, bool disableGameObject = true);

        /// <summary>
        /// Enable a camera by its CameraType UID (null = first bound camera).
        /// Virtual cameras are ACTIVATED (priority boost) instead of merely enabled.
        /// </summary>
        void EnableCamera(UID cameraTypeUID, bool enableGameObject = true);

        /// <summary>
        /// Disable a camera by its CameraType UID (null = first bound camera).
        /// Virtual cameras are demoted to standby (the default camera takes over with a smooth blend).
        /// </summary>
        void DisableCamera(UID cameraTypeUID, bool disableGameObject = true);

        // =================================================================
        // VIRTUAL CAMERAS (Cinemachine, single-brain priority workflow)
        // =================================================================

        /// <summary>The currently live virtual camera, or null if none is active.</summary>
        IVirtualGameCamera ActiveVirtualCamera { get; }

        /// <summary>The virtual camera marked as default (fallback), or null if none.</summary>
        IVirtualGameCamera DefaultVirtualCamera { get; }

        /// <summary>
        /// Makes a virtual camera live: its priority is boosted above all others and the (single)
        /// Cinemachine brain blends to it. Pass null to activate the default camera.
        /// </summary>
        /// <param name="cameraTypeUID">Optional UID to pick a variant. Null = default, else first bound virtual camera.</param>
        /// <param name="explicitCamera">Skip lookup entirely and activate this exact instance.</param>
        void ActivateVirtualCamera(UID cameraTypeUID = null, IVirtualGameCamera explicitCamera = null);

        /// <summary>
        /// Makes a virtual camera live and awaits until the brain's blend to it completes.
        /// </summary>
        UniTask<IVirtualGameCamera> ActivateVirtualCameraAsync(UID cameraTypeUID = null, IVirtualGameCamera explicitCamera = null,
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
