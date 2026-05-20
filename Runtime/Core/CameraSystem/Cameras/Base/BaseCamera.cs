using System;
using System.Collections;
using AK.Core;
using Reflex.Extensions;
using AK.StateMachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace AK.CameraSystem
{
	[RequireComponent(typeof(Camera))]
	public abstract class BaseCamera<TEntity, TState> : StateEntity<TEntity, TState>, IGameCamera
		where TEntity : GameEntity
		where TState : BaseState<TEntity>, new()
	{
		[Header("Camera Config")] [SerializeField]
		protected CameraRole _role = CameraRole.Base;

		[SerializeField] protected int _layerOrder;

		[Tooltip("Type name of the Base Camera this overlay belongs to (Optional)")] [SerializeField]
		protected Camera _camera;

		private ICameraSystem _cameraSystem;

		public CameraRole Role       => _role;
		public int        LayerOrder => _layerOrder;
		public Camera     Camera     => _camera;
		public GameObject GameObject => gameObject;

		public virtual Type DefaultBaseCameraType => typeof(BaseCamera);

#if UNITY_EDITOR
		protected virtual void OnValidate()
		{
			if (_camera == null) _camera = GetComponent<Camera>();
			UpdateCameraRenderType();
		}
#endif

		protected override void Awake()
		{
			base.Awake();

			if (_camera == null) _camera = GetComponent<Camera>();
			UpdateCameraRenderType();

			var container = gameObject.scene.GetSceneContainer();
			_cameraSystem = container.Resolve<ICameraSystem>();
			_cameraSystem.BindCamera(this);
		}
		
		private void UpdateCameraRenderType()
		{
			if (_camera == null) return;

			var cameraData = _camera.GetUniversalAdditionalCameraData();
			if (cameraData == null) return;

			switch (_role)
			{
				case CameraRole.Base:
					cameraData.renderType = CameraRenderType.Base;
					break;
				case CameraRole.Overlay:
					cameraData.renderType = CameraRenderType.Overlay;
					break;
			}
		}

		public void Enable(bool enableGameObject = true)
		{
			_cameraSystem.EnableCamera(GetType(), enableGameObject);
		}

		public void Disable(bool disableGameObject = true)
		{
			_cameraSystem.DisableCamera(GetType(), disableGameObject);
		}

		public virtual void Shake(float intensity, float duration)
		{
			StartCoroutine(ShakeRoutine(Camera.transform, intensity, duration));
		}

		private IEnumerator ShakeRoutine(Transform camTransform, float intensity, float duration)
		{
			Vector3 originalPos = camTransform.localPosition;
			float elapsed = 0.0f;

			while (elapsed < duration)
			{
				float x = Random.Range(-1f, 1f) * intensity;
				float y = Random.Range(-1f, 1f) * intensity;

				camTransform.localPosition = originalPos + new Vector3(x, y, 0);

				elapsed += Time.deltaTime;
				yield return null;
			}

			camTransform.localPosition = originalPos;
		}
	}

	// Non-generic wrapper for simple cameras
	public class BaseCamera : BaseCamera<BaseCamera, BaseCamera.VoidState>
	{
		public sealed class VoidState : BaseState<BaseCamera> { }
	}
}