using UnityEngine;

namespace AK.Core.Extensions
{
	public static class ObjectExt
	{
		/// <summary>
		/// Destroys <paramref name="target"/> the way the current mode allows: at the end of the
		/// frame in Play mode, immediately in Edit mode (where <c>Object.Destroy</c> is an error),
		/// so the same code serves runtime, editor tools and edit-mode tests. A null or already
		/// destroyed target is ignored.
		/// </summary>
		public static void DestroyInAnyMode(this Object target)
		{
			if (target == null)
			{
				return;
			}

			if (Application.isPlaying)
			{
				Object.Destroy(target);
			}
			else
			{
				Object.DestroyImmediate(target);
			}
		}
	}
}
