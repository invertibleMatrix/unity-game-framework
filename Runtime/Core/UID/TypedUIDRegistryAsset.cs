using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AK.Core
{
	public abstract class TypedUIDRegistryAsset<T> : ScriptableObject where T : UID
	{
		[SerializeField] protected TypedUIDRegistry<T> _registry = new();

		public TypedUIDRegistry<T> Registry => _registry;

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
		public string           GetUID(T obj)               => _registry.GetUID(obj);
		public IReadOnlyList<T> GetAllObjects()             => _registry.GetAllObjects();

		
#if UNITY_EDITOR
		[Button("Refresh All Objects")]
		[ContextMenu("Refresh All Objects")]
		public void RefreshAllObjects()
		{
			_registry.RefreshAllObjects();
			EditorUtility.SetDirty(this);
		}

		[Button("Validate Objects")]
		[ContextMenu("Validate Objects")]
		public void ValidateObjects()
		{
			_registry.ValidateObjects();
		}

		[Button("Regenerate UIDs")]
		[ContextMenu("Regenerate UIDs")]
		public void RefreshIds()
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
				foreach (T o in grouping)
				{
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
		}
#endif
	}
}