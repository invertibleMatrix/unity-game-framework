using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AK.Core
{
	public abstract class TypedUIDRegistryAsset<T> : UIDRegistryAsset where T : UID
	{
		[SerializeField] protected TypedUIDRegistry<T> _registry = new();

		public TypedUIDRegistry<T> Registry => _registry;

		public override int ObjectCount => _registry.Objects.Count;

		public override IEnumerable<UID> GetTrackedObjects() => _registry.Objects;

		public void Initialize()
		{
			_registry.Initialize();
		}

		private void OnEnable()
		{
			Initialize();
		}
		
		public T                GetObjectByUID(string guid) => _registry.GetObjectByUID(guid);
		public T                GetObjectByUID(UID uid)     => _registry.GetObjectByUID(uid);
		public T                GetObjectByName(string objName) => _registry.GetObjectByName(objName);

		/// <summary>
		/// Resolves a UIDRef link: GUID first, asset-name fallback when the GUID went
		/// stale (identity regenerated). Name-resolved hits log a warning — re-link the
		/// field in the editor (the drawer heals and persists the new GUID on sight).
		/// </summary>
		public T GetObjectByUID(UIDRef link)
		{
			if (link == null || !link.IsSet) return null;

			T obj = _registry.GetObjectByUID(link.Guid);
			if (obj == null && !string.IsNullOrEmpty(link.AssetName))
			{
				obj = _registry.GetObjectByName(link.AssetName);
				if (obj != null)
				{
					Debug.LogWarning($"[{GetType().Name}] '{link.AssetName}' resolved by name — its stored GUID was stale. Open the referencing asset in the inspector to persist the healed link.", this);
				}
			}

			return obj;
		}

		public string           GetUID(T obj)               => _registry.GetUID(obj);
		public IReadOnlyList<T> GetAllObjects()             => _registry.GetAllObjects();

#if UNITY_EDITOR
		/// <summary>Adds the asset if its type matches T and it isn't tracked yet. Used by the auto-tracker.</summary>
		public override bool TryTrackAsset(UID asset)
		{
			if (asset is not T typed || _registry.Objects.Contains(typed)) return false;

			_registry.AddObject(typed);
			EditorUtility.SetDirty(this);
			return true;
		}

		/// <summary>Removes entries whose assets were deleted. Used by the auto-tracker.</summary>
		public override int RemoveNullEntries()
		{
			int removed = _registry.RemoveNullEntries();
			if (removed > 0)
			{
				EditorUtility.SetDirty(this);
			}

			return removed;
		}
#endif

		
#if UNITY_EDITOR
		public override void RefreshAllObjects()
		{
			_registry.RefreshAllObjects();
			EditorUtility.SetDirty(this);
		}

		public override void ValidateObjects()
		{
			_registry.ValidateObjects();
		}

		public override void RegenerateUIDs()
		{
			List<IGrouping<string, T>> duplicates = _registry.Objects
			                                                 .Where(obj => obj != null && obj.UniqueID != null && !obj.UniqueID.IsEmpty())
			                                                 .GroupBy(obj => obj.UniqueID.Id)
			                                                 .Where(g => g.Count() > 1)
			                                                 .ToList();

			if (duplicates.Any())
			{
				Debug.LogWarning($"Found {duplicates.Count} duplicate UIDs in {typeof(T).Name} registry!");
			}

			foreach (IGrouping<string, T> grouping in duplicates)
			{
				// The first member keeps its identity — links and GUID-keyed save data
				// pointing at it stay valid. Only the copies get fresh GUIDs.
				foreach (T o in grouping.Skip(1))
				{
					Debug.LogWarning($"[{GetType().Name}] '{o.name}' shared its GUID with '{grouping.First().name}' — assigned a fresh one; the original keeps its identity.", o);
					o.UniqueID.GenerateNewGuid();
					EditorUtility.SetDirty(o.UniqueID);
				}
			}

			var missingUID = _registry.Objects.Where(obj => obj.UniqueID == null || obj.UniqueID.IsEmpty()).ToList();
			if (missingUID.Any())
			{
				Debug.LogWarning($"Found {missingUID.Count} {typeof(T).Name}s without valid UIDs!");
			}

			missingUID.ForEach(x =>
			{
				x.UniqueID.GenerateNewGuid();
				EditorUtility.SetDirty(x.UniqueID);
			});

			AssetDatabase.SaveAssets();
		}
#endif
	}
}