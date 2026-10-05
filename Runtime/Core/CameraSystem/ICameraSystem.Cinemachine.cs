#if UGFW_CINEMACHINE
using System.Threading;
using AK.Core;
using Cysharp.Threading.Tasks;

namespace AK.Systems
{
    // Virtual cameras: Cinemachine's single-brain priority workflow.
    public partial interface ICameraSystem
    {
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
    }
}
#endif
