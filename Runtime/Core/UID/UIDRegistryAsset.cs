using System.Collections.Generic;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// ScriptableObject wrapper around a <see cref="UidRegistry{T}"/>. One asset per domain
	/// (audio, facts, cameras...). The editor auto-tracker keeps its list in sync with the
	/// project; at runtime it is a pure resolver.
	/// </summary>
	public abstract class UidRegistryAsset<T> : UidRegistryAssetBase where T : UID
	{
		[SerializeField] protected UidRegistry<T> _registry = new();

		public UidRegistry<T>   Registry => _registry;
		public IReadOnlyList<T> Objects  => _registry.Objects;

		public override System.Type ElementType => typeof(T);
		public override int         ObjectCount => _registry.Count;

		public override IEnumerable<UID> GetTrackedObjects() => _registry.Objects;

		public bool TryResolve(Uid<T> id, out T obj) => _registry.TryResolve(id, out obj);
		public bool TryResolve(Uid id, out T obj)    => _registry.TryResolve(id, out obj);
		public T    Resolve(Uid<T> id)               => _registry.Resolve(id);
		public T    Resolve(Uid id)                  => _registry.Resolve(id);
		public bool Contains(Uid<T> id)              => _registry.Contains(id.Value);
		public bool Contains(Uid id)                 => _registry.Contains(id);

		public UidHandle<T> GetHandle(Uid<T> id)                       => _registry.GetHandle(id);
		public bool         TryGet(ref UidHandle<T> handle, out T obj) => _registry.TryGet(ref handle, out obj);

		public override bool TryResolveUntyped(Uid id, out UID obj)
		{
			bool found = _registry.TryResolve(id, out T typed);
			obj = typed;
			return found;
		}

		public override void SetRedirects(UidRedirectTable redirects) => _registry.SetRedirects(redirects);

		private void OnEnable() => _registry.Invalidate();

#if UNITY_EDITOR
		// Inspector edits and undo change the list under the registry's index.
		private void OnValidate() => _registry.Invalidate();

		public override bool Editor_TryTrack(UID asset)
		{
			if (asset is not T typed || !_registry.Add(typed)) return false;

			UnityEditor.EditorUtility.SetDirty(this);
			return true;
		}

		public override bool Editor_Untrack(UID asset)
		{
			if (asset is not T typed || !_registry.Remove(typed)) return false;

			UnityEditor.EditorUtility.SetDirty(this);
			return true;
		}

		public override int Editor_RemoveNullEntries()
		{
			int removed = _registry.RemoveNullEntries();
			if (removed > 0) UnityEditor.EditorUtility.SetDirty(this);
			return removed;
		}

		public override int Editor_RefreshFromProject()
		{
			var found = new List<T>();
			foreach (string guid in UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
			{
				var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
				if (asset != null) found.Add(asset);
			}

			_registry.ReplaceAll(found);
			UnityEditor.EditorUtility.SetDirty(this);
			return found.Count;
		}
#endif
	}
}
