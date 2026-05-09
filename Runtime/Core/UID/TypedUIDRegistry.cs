using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace AK.Core
{
	[Serializable]
	public class TypedUIDRegistry<T> where T : UID
	{
		[InlineEditor] [SerializeField]
		protected List<T> _objects = new();
		
		protected Dictionary<string, T> _uidToObject;
		protected Dictionary<T, string> _objectToUid;

		public IReadOnlyList<T> Objects => _objects;

		public void Initialize()
		{
			BuildLookups();
		}

		private void BuildLookups()
		{
			_uidToObject = new Dictionary<string, T>();
			_objectToUid = new Dictionary<T, string>();

			foreach (var obj in _objects)
			{
				if (obj != null && obj.UniqueID != null && !obj.UniqueID.IsEmpty())
				{
					_uidToObject[obj.UniqueID.Id] = obj;
					_objectToUid[obj] = obj.UniqueID.Id;
				}
			}
		}

		public T GetObjectByUID(string guid)
		{
			if (_uidToObject == null)
				Initialize();

			return _uidToObject.TryGetValue(guid, out var obj) ? obj : null;
		}

		public T GetObjectByUID(UID uid)
		{
			if (uid.IsEmpty())
			{
				Debug.LogWarning($"{uid} is Empty or Null");
			}

			T obj = GetObjectByUID(uid.Id);
			if (obj == null)
			{
				Debug.LogError($"Object not found in Registry for {uid.name}");
			}

			return obj;
		}

		public string GetUID(T obj)
		{
			if (_objectToUid == null)
				Initialize();

			return _objectToUid.TryGetValue(obj, out var uid) ? uid : null;
		}

		public IReadOnlyList<T> GetAllObjects()
		{
			return _objects.AsReadOnly();
		}

		// Editor helpers
#if UNITY_EDITOR
		public void RefreshAllObjects()
		{
			var type = typeof(T);
			var allObjects = AssetDatabase.FindAssets($"t:{type.Name}")
			                              .Select(AssetDatabase.GUIDToAssetPath)
			                              .Select(AssetDatabase.LoadAssetAtPath<T>)
			                              .Where(obj => obj != null && obj.UniqueID != null && !obj.UniqueID.IsEmpty())
			                              .ToList();

			_objects = allObjects;
		}

		public void ValidateObjects()
		{
			var duplicates = _objects
			                 .Where(obj => obj != null && obj.UniqueID != null && !obj.UniqueID.IsEmpty())
			                 .GroupBy(obj => obj.UniqueID.Id)
			                 .Where(g => g.Count() > 1)
			                 .ToList();

			if (duplicates.Any())
			{
				Debug.LogWarning($"Found {duplicates.Count} duplicate UIDs in {typeof(T).Name} registry!");
			}

			var missingUID = _objects.Where(obj => obj.UniqueID == null || obj.UniqueID.IsEmpty()).ToList();
			if (missingUID.Any())
			{
				Debug.LogWarning($"Found {missingUID.Count} {typeof(T).Name}s without valid UIDs!");
			}
		}
#endif
	}
}