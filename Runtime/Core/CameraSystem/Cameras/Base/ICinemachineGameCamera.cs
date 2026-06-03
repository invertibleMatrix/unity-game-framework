using Unity.Cinemachine;

namespace AK.Systems
{
    public interface ICinemachineGameCamera : IGameCamera
    {
        public CinemachineCamera ActiveVirtualCam { get; }
    }
}