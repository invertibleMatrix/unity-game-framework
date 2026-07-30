using Unity.Cinemachine;

namespace AK.Systems
{
    public interface ICinemachineGameCamera : IGameCamera
    {
        public CinemachineCamera ActiveVirtualCam { get; }

        /// <summary>The brain on this physical camera. One brain per project is the intended setup.</summary>
        public CinemachineBrain Brain { get; }
    }
}