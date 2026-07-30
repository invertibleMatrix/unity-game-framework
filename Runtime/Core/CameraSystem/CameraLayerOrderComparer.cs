using System.Collections.Generic;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Sorts URP stack cameras by LayerOrder using a cached lookup maintained by CameraSystem.
	/// Never calls GetComponent inside Compare (that would be O(n log n) interface lookups per sort).
	/// </summary>
	public class CameraLayerOrderComparer : IComparer<Camera>
	{
		private readonly IReadOnlyDictionary<Camera, int> _layerOrderByCamera;

		public CameraLayerOrderComparer(IReadOnlyDictionary<Camera, int> layerOrderByCamera)
		{
			_layerOrderByCamera = layerOrderByCamera;
		}

		public int Compare(Camera x, Camera y)
		{
			if (x == null) return -1;
			if (y == null) return 1;

			int xOrder = _layerOrderByCamera.TryGetValue(x, out var xo) ? xo : 0;
			int yOrder = _layerOrderByCamera.TryGetValue(y, out var yo) ? yo : 0;

			return xOrder.CompareTo(yOrder);
		}
	}
}
