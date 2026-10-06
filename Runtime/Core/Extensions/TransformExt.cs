using UnityEngine;

namespace AK.Core.Extensions
{
	public static class TransformExt
	{
		/// <summary>
		/// Gives <paramref name="target"/> the local pose <paramref name="source"/> has: position,
		/// rotation and scale, and for two RectTransforms the anchors, pivot, size and anchored
		/// position too. A pool calls it on a reused instance, with its prefab as the source, to
		/// place it under its new parent the way <c>Object.Instantiate(prefab, parent)</c> places
		/// a new one. Both transforms must be alive.
		/// </summary>
		public static void MatchLocalPose(this Transform target, Transform source)
		{
			if (target is RectTransform rect && source is RectTransform sourceRect)
			{
				rect.anchorMin = sourceRect.anchorMin;
				rect.anchorMax = sourceRect.anchorMax;
				rect.pivot = sourceRect.pivot;
				rect.sizeDelta = sourceRect.sizeDelta;
				rect.anchoredPosition3D = sourceRect.anchoredPosition3D;
				rect.localRotation = sourceRect.localRotation;
				rect.localScale = sourceRect.localScale;
				return;
			}

			source.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);
			target.SetLocalPositionAndRotation(position, rotation);
			target.localScale = source.localScale;
		}
	}
}
