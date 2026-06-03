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
        /// UID identifying what kind of camera this is (e.g., Main, UI, Effects).
        /// Used for UID-based lookups in CameraSystem.
        /// </summary>
        UID CameraTypeUID { get; }

        /// <summary>
        /// If this is an Overlay camera, which Base CameraType UID does it belong to?
        /// Returns null if it has no specific parent or is a Base itself.
        /// </summary>
        UID DefaultBaseCameraUID { get; }

        void Enable(bool enableGameObject = true);
        void Disable(bool disableGameObject = true);
        void Shake(float intensity, float duration);
    }
}