using System;

namespace AK.CameraSystem
{
	public interface ICameraSystem
	{
		T Get<T>() where T : class, IGameCamera;
        
		void BindCamera(IGameCamera gameCamera);
        
		void EnableCamera<T>(bool enableGameObject = true) where T : class, IGameCamera;
		void DisableCamera<T>(bool disableGameObject = true) where T : class, IGameCamera;
        
		void EnableCamera(Type cameraType, bool enableGameObject = true);
		void DisableCamera(Type cameraType, bool disableGameObject = true);

		// Restored: Reorders stacks for ALL base cameras
		void ReorderCameraStack();
        
		void Dispose();
	}
}