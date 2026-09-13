using System.Collections.Generic;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// Non-generic base for every registry asset. Editor tooling (auto-tracker, build
	/// validator, project-wide maintenance, the shared inspector) discovers registries
	/// through this type with one AssetDatabase query, and the aggregate resolver in the
	/// metadata repository enumerates tracked identities through it without knowing T.
	/// </summary>
	public abstract class UidRegistryAssetBase : ScriptableObject
	{
		public abstract System.Type ElementType { get; }
		public abstract int         ObjectCount { get; }

		/// <summary>Every tracked asset, untyped. Nulls (deleted assets) are yielded so validators can report them.</summary>
		public abstract IEnumerable<UID> GetTrackedObjects();

		/// <summary>Untyped resolve for aggregate lookups. Typed callers use the generic subclass.</summary>
		public abstract bool TryResolveUntyped(Uid id, out UID obj);

		public abstract void SetRedirects(UidRedirectTable redirects);

#if UNITY_EDITOR
		public abstract bool Editor_TryTrack(UID asset);
		public abstract bool Editor_Untrack(UID asset);
		public abstract int  Editor_RemoveNullEntries();
		public abstract int  Editor_RefreshFromProject();
#endif
	}
}
