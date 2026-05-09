using Unity.Cinemachine;

namespace AK.CameraSystem
{
    public interface ICinemachineGameCamera : IGameCamera

    {
        public CinemachineCamera ActiveVirtualCam { get; }
    }
}