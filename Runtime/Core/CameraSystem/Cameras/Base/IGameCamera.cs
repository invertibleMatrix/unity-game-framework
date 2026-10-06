using AK.Core;
using UnityEngine;

namespace AK.Systems
{
    public interface IGameCamera
    {
        CameraRole Role       { get; }
        int        LayerOrder { get; }
        Camera     Camera     { get; }
        GameObject GameObject { get; }

        /// <summary>
        /// Identity of this camera's kind (Main, UI, Effects...). None when the camera is
        /// unaddressed and found only as "first of its type".
        /// </summary>
        Uid<CameraType> CameraTypeId { get; }

        /// <summary>
        /// For Overlay cameras: the kind of Base camera this overlay stacks on. None when it
        /// has no specific parent or is itself a Base.
        /// </summary>
        Uid<CameraType> DefaultBaseCameraId { get; }

        /// <summary>Enables this camera, through its camera system when it has one.</summary>
        void Enable(bool enableGameObject = true);

        /// <summary>Disables this camera, through its camera system when it has one.</summary>
        void Disable(bool disableGameObject = true);

        /// <summary>
        /// Shakes the camera, up to <paramref name="intensity"/>, for <paramref name="duration"/>
        /// seconds. How depends on the camera: a base camera moves its transform, a Cinemachine
        /// camera with an impulse source sends an impulse.
        /// </summary>
        void Shake(float intensity, float duration);
    }
}
