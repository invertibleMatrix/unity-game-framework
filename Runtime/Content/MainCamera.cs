using AK.CameraSystem;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gameplay.Cameras
{
	public class MainCamera : BaseCamera
	{
		[SerializeField] protected PhysicsRaycaster _physicsRaycaster;

		public void ToggleRaycaster(bool isEnabled)
		{
			if (_physicsRaycaster != null)
			{
				_physicsRaycaster.enabled = isEnabled;
			}
		}
	}
}