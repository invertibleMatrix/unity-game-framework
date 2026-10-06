using AK.Utilities.Previews;
using UnityEditor;
using UnityEngine;

namespace AK.Tests.Previews
{
	/// <summary>Builds the scene objects ModelPreview tests run against.</summary>
	internal static class PreviewRig
	{
		/// <summary>The layer the test stage renders models on.</summary>
		public const int ModelLayer = 4;

		/// <summary>Where the stage camera sits relative to its pivot, as a stage prefab would author it.</summary>
		public static readonly Vector3 CameraOffset = new(0f, 0f, -5f);

		/// <summary>
		/// A stage like the shipped prefab: a <see cref="ModelPreviewCamera"/> rig with a
		/// separate pivot (the turntable), a disabled camera looking at it, and a directional
		/// light on the model layer.
		/// </summary>
		public static ModelPreviewCamera CreateStage(string name = "TestStage")
		{
			var root = new GameObject(name);

			var pivot = new GameObject("Pivot");
			pivot.transform.SetParent(root.transform, false);

			var cameraObject = new GameObject("Camera");
			cameraObject.transform.SetParent(root.transform, false);
			cameraObject.transform.localPosition = CameraOffset;
			var camera = cameraObject.AddComponent<Camera>();
			camera.enabled = false;

			var sun = new GameObject("Sun");
			sun.transform.SetParent(root.transform, false);
			sun.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
			var light = sun.AddComponent<Light>();
			light.type        = LightType.Directional;
			light.cullingMask = 1 << ModelLayer;

			var rig = root.AddComponent<ModelPreviewCamera>();
			var serialized = new SerializedObject(rig);
			serialized.FindProperty("_camera").objectReferenceValue = camera;
			serialized.FindProperty("_pivot").objectReferenceValue = pivot.transform;
			serialized.FindProperty("_modelLayer").intValue = 1 << ModelLayer;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			return rig;
		}
	}
}
