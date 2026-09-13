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

        void Enable(bool enableGameObject = true);
        void Disable(bool disableGameObject = true);
        void Shake(float intensity, float duration);
    }
}
