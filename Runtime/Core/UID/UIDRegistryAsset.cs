using System.Collections.Generic;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// Non-generic base for typed UID registry assets. Unity's context-menu discovery skips
	/// methods declared on generic base classes, and CustomEditor inheritance matching
	/// requires a non-generic base type — this contract enables both the per-asset
	/// inspector buttons (one editor for all subclasses) and project-wide bulk maintenance
	/// via a single AssetDatabase type query.
	/// </summary>
	public abstract class UIDRegistryAsset : ScriptableObject
	{
		public abstract int ObjectCount { get; }

		/// <summary>Validation view over the tracked objects, regardless of element type.</summary>
		public abstract IEnumerable<UID> GetTrackedObjects();

#if UNITY_EDITOR
		public abstract void RefreshAllObjects();
		public abstract void ValidateObjects();
		public abstract void RegenerateUIDs();
		public abstract bool TryTrackAsset(UID asset);
		public abstract int  RemoveNullEntries();
#endif
	}
}
