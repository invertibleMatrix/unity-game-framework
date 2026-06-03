using System.Collections.Generic;
using UnityEngine;

namespace AK.Systems
{
	public class CameraLayerOrderComparer : IComparer<Camera>
	{
		public int Compare(Camera x, Camera y)
		{
			if (x == null) return -1;
			if (y == null) return 1;

			var xBaseCamera = x.GetComponent<IGameCamera>();
			var yBaseCamera = y.GetComponent<IGameCamera>();

			int xOrder = xBaseCamera?.LayerOrder ?? 0;
			int yOrder = yBaseCamera?.LayerOrder ?? 0;

			return xOrder.CompareTo(yOrder);
		}
	}
}