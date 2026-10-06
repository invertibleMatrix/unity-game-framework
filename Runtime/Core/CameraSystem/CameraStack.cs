using System.Collections.Generic;
using UnityEngine;
#if UGFW_URP
using UnityEngine.Rendering.Universal;
#endif

namespace AK.Systems
{
	/// <summary>
	/// A base camera's stack: the overlay cameras that render on top of it, in order. This is
	/// URP's camera stacking. Without URP, cameras don't stack: no camera has a stack, and each
	/// renders on its own, in <see cref="Camera.depth"/> order.
	/// </summary>
	internal readonly struct CameraStack
	{
#if UGFW_URP
		private readonly UniversalAdditionalCameraData _data;

		private CameraStack(UniversalAdditionalCameraData data)
		{
			_data = data;
		}
#endif

		/// <summary>
		/// The cameras on the stack, in render order. Null when the stack can't be read: its
		/// camera was destroyed, or isn't a base camera of a renderer that stacks cameras.
		/// </summary>
		public List<Camera> Cameras
		{
			get
			{
#if UGFW_URP
				return _data != null ? _data.cameraStack : null;
#else
				return null;
#endif
			}
		}

		/// <summary>Gets the stack of <paramref name="baseCamera"/>. False without a camera, or without URP.</summary>
		public static bool TryGet(Camera baseCamera, out CameraStack stack)
		{
#if UGFW_URP
			if (baseCamera != null)
			{
				UniversalAdditionalCameraData data = baseCamera.GetUniversalAdditionalCameraData();
				if (data != null)
				{
					stack = new CameraStack(data);
					return true;
				}
			}
#endif
			stack = default;
			return false;
		}

		/// <summary>
		/// Sets how <paramref name="camera"/> renders for <paramref name="role"/>: a base camera
		/// first, and an overlay on top of the base camera whose stack it is on. Does nothing for
		/// other roles, or without URP.
		/// </summary>
		public static void ApplyRenderType(Camera camera, CameraRole role)
		{
#if UGFW_URP
			if (camera == null) return;

			UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
			if (data == null) return;

			switch (role)
			{
				case CameraRole.Base:
					data.renderType = CameraRenderType.Base;
					break;
				case CameraRole.Overlay:
					data.renderType = CameraRenderType.Overlay;
					break;
			}
#endif
		}
	}
}
