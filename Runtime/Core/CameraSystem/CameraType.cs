using AK.Core;
using UnityEngine;

namespace AK.Systems
{
    /// <summary>
    /// ScriptableObject identifier for camera types (e.g., Main, UI, Effects).
    /// Used by CameraRegistry and CameraSystem for UID-based camera lookups.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraType_", menuName = "AK/Camera/Camera Type")]
    public class CameraType : UID
    {
    }
}
