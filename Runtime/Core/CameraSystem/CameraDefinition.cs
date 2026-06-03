using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Systems
{
    /// <summary>
    /// Defines a camera prefab and its configuration for use with CameraRegistry and CameraSystem.
    /// Each definition maps a CameraType UID to a prefab with role, layer order, and base camera reference.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraDef_", menuName = "AK/Camera/Camera Definition")]
    public class CameraDefinition : MetaDataAsset
    {
        [Header("Camera Config")]
        [Tooltip("The type of this camera (e.g., Main, UI, Effects).")]
        [SerializeField] private CameraType _cameraType;

        [Tooltip("Whether this camera renders as Base or Overlay in the URP stack.")]
        [SerializeField] private CameraRole _role = CameraRole.Base;

        [Tooltip("Sort order within the URP camera stack.")]
        [SerializeField] private int _layerOrder;

        [Tooltip("For Overlay cameras: which Base CameraType this overlay stacks on top of.")]
        [ShowIf("_role", CameraRole.Overlay)]
        [SerializeField] private CameraType _baseCameraType;

        [Tooltip("The camera prefab to instantiate.")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("Whether this camera should be spawned automatically on startup.")]
        [SerializeField] private bool _spawnOnStart;

        public CameraType CameraType => _cameraType;
        public CameraRole Role => _role;
        public int LayerOrder => _layerOrder;
        public CameraType BaseCameraType => _baseCameraType;
        public GameObject Prefab => _prefab;
        public bool SpawnOnStart => _spawnOnStart;
    }
}
