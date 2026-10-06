#if UGFW_CINEMACHINE
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

namespace AK.Systems
{
	// Virtual cameras: Cinemachine's single-brain priority workflow. Every virtual camera waits at
	// its standby priority, the live one is boosted above the rest, and the brain blends to it.
	public partial class CameraSystem
	{
		[Tooltip("Added to a virtual camera's BasePriority while it is live. Higher than any standby priority wins the brain.")]
		[SerializeField] private int _virtualCameraActiveBoost = 100;

		private readonly List<IVirtualGameCamera> _virtualCameras = new();
		private IVirtualGameCamera _activeVirtualCamera;
		private IVirtualGameCamera _defaultVirtualCamera;
		private CinemachineBrain _brain;

		public IVirtualGameCamera ActiveVirtualCamera => _activeVirtualCamera;
		public IVirtualGameCamera DefaultVirtualCamera => _defaultVirtualCamera;

		partial void BindVirtualCamera(IGameCamera gameCamera)
		{
			if (gameCamera is not IVirtualGameCamera camera)
			{
				Debug.LogWarning($"[CameraSystem] '{gameCamera.GameObject.name}' has the Virtual role but isn't an IVirtualGameCamera. It is bound by type and identity only.");
				return;
			}

			if (_virtualCameras.Contains(camera)) return;

			_virtualCameras.Add(camera);

			if (camera.VirtualCamera != null)
			{
				// Park at standby priority; GameObjects stay enabled (priority decides, the brain blends).
				camera.VirtualCamera.Priority = camera.BasePriority;
			}

			if (camera.IsDefault && _defaultVirtualCamera == null)
			{
				_defaultVirtualCamera = camera;

				// A default with nothing live takes over immediately (this also covers cold boot).
				if (_activeVirtualCamera == null)
				{
					ActivateVirtualCamera(explicitCamera: camera);
				}
			}
		}

		partial void UnbindVirtualCamera(IGameCamera gameCamera)
		{
			if (gameCamera is not IVirtualGameCamera camera) return;

			_virtualCameras.Remove(camera);

			if (ReferenceEquals(_defaultVirtualCamera, camera))
			{
				_defaultVirtualCamera = null;
			}

			if (ReferenceEquals(_activeVirtualCamera, camera))
			{
				_activeVirtualCamera = null;

				// Fall back to the default so the brain has somewhere to land.
				if (_defaultVirtualCamera != null)
				{
					ActivateVirtualCamera(explicitCamera: _defaultVirtualCamera);
				}
			}
		}

		partial void EnableVirtualCamera(IGameCamera gameCamera)
		{
			if (gameCamera is IVirtualGameCamera camera)
			{
				ActivateVirtualCamera(explicitCamera: camera);
			}
		}

		partial void DisableVirtualCamera(IGameCamera gameCamera)
		{
			if (gameCamera is IVirtualGameCamera camera)
			{
				DeactivateVirtualCamera(camera);
			}
		}

		partial void ClearVirtualCameras()
		{
			_virtualCameras.Clear();
			_activeVirtualCamera = null;
			_defaultVirtualCamera = null;
			_brain = null;
		}

		public void ActivateVirtualCamera(Uid<CameraType> cameraType = default, IVirtualGameCamera explicitCamera = null)
		{
			var target = ResolveVirtualCamera(cameraType, explicitCamera);
			if (target == null) return;

			foreach (var cam in _virtualCameras)
			{
				if (cam.VirtualCamera == null) continue;
				cam.VirtualCamera.Priority = ReferenceEquals(cam, target)
					? cam.BasePriority + _virtualCameraActiveBoost
					: cam.BasePriority;
			}

			_activeVirtualCamera = target;
		}

		public void ActivateVirtualCamera(CameraType cameraType, IVirtualGameCamera explicitCamera = null)
		{
			ActivateVirtualCamera(ToId(cameraType), explicitCamera);
		}

		public async UniTask<IVirtualGameCamera> ActivateVirtualCameraAsync(Uid<CameraType> cameraType = default, IVirtualGameCamera explicitCamera = null,
		                                                                    CancellationToken ct = default)
		{
			ActivateVirtualCamera(cameraType, explicitCamera);
			await WaitForCameraBlendAsync(ct);
			return _activeVirtualCamera;
		}

		public void DeactivateVirtualCamera(IVirtualGameCamera camera)
		{
			if (camera?.VirtualCamera == null) return;

			camera.VirtualCamera.Priority = camera.BasePriority;

			if (ReferenceEquals(_activeVirtualCamera, camera))
			{
				_activeVirtualCamera = null;

				// Smooth hand-over to the default camera, if one exists.
				if (_defaultVirtualCamera != null && !ReferenceEquals(_defaultVirtualCamera, camera))
				{
					ActivateVirtualCamera(explicitCamera: _defaultVirtualCamera);
				}
			}
		}

		public bool ActivateDefaultVirtualCamera()
		{
			if (_defaultVirtualCamera == null)
			{
				Debug.LogWarning("[CameraSystem] No default virtual camera set.");
				return false;
			}

			ActivateVirtualCamera(explicitCamera: _defaultVirtualCamera);
			return true;
		}

		public async UniTask WaitForCameraBlendAsync(CancellationToken ct = default)
		{
			var brain = GetBrain();
			if (brain == null)
			{
				return;
			}

			// Let Cinemachine process the priority change first.
			await UniTask.Yield(ct);

			while (brain != null && brain.IsBlending)
			{
				await UniTask.Yield(ct);
			}
		}

		private IVirtualGameCamera ResolveVirtualCamera(Uid<CameraType> cameraType, IVirtualGameCamera explicitCamera)
		{
			if (explicitCamera != null) return explicitCamera;

			if (cameraType.IsSet)
			{
				if (_camerasById.TryGetValue(cameraType.Value, out var camera))
				{
					if (camera is IVirtualGameCamera virtualCamera) return virtualCamera;

					Debug.LogWarning($"[CameraSystem] Camera {UidDebugNames.Describe(cameraType)} is bound but is not a virtual camera.");
					return null;
				}

				Debug.LogWarning($"[CameraSystem] No camera bound for CameraType {UidDebugNames.Describe(cameraType)}.");
				return null;
			}

			// None: default first, otherwise the first bound virtual camera.
			return _defaultVirtualCamera ?? (_virtualCameras.Count > 0 ? _virtualCameras[0] : null);
		}

		private CinemachineBrain GetBrain()
		{
			if (_brain != null) return _brain;

			// Prefer the brain of a bound Cinemachine base camera; fall back to any active brain.
			var brainCamera = GetCamera<ICinemachineGameCamera>();
			_brain = (brainCamera != null && brainCamera.Brain != null)
				? brainCamera.Brain
				: CinemachineBrain.GetActiveBrain(0);

			return _brain;
		}
	}
}
#else
using UnityEngine;

namespace AK.Systems
{
	// Virtual cameras need Cinemachine 3.
	public partial class CameraSystem
	{
		partial void BindVirtualCamera(IGameCamera gameCamera)
		{
			Debug.LogWarning($"[CameraSystem] '{gameCamera.GameObject.name}' is a virtual camera, and virtual cameras need Cinemachine 3. It is bound by type and identity only.");
		}
	}
}
#endif
