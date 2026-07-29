using Unity.Cinemachine;

namespace AK.Systems
{
	/// <summary>
	/// A Cinemachine virtual camera managed by the <see cref="ICameraSystem"/>.
	/// Virtual cameras have no Unity <see cref="UnityEngine.Camera"/> of their own - a single
	/// CinemachineBrain (on one physical base camera) renders whichever enabled virtual camera
	/// has the highest priority, blending smoothly on priority changes.
	/// </summary>
	public interface IVirtualGameCamera : IGameCamera
	{
		/// <summary>The wrapped Cinemachine virtual camera (CinemachineCamera, FreeLook, StateDriven, ...).</summary>
		CinemachineVirtualCameraBase VirtualCamera { get; }

		/// <summary>Priority while on standby. The active camera gets BasePriority + the system's active boost.</summary>
		int BasePriority { get; }

		/// <summary>Fallback camera the system activates when the live one goes away.</summary>
		bool IsDefault { get; }

		/// <summary>Whether this camera is currently the live one (winning the brain).</summary>
		bool IsLive { get; }
	}
}
