using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AK.Core
{
	[CreateAssetMenu(fileName = "UIDRegistry", menuName = "AK/Registries/UIDRegistry")]
	public class UIDRegistry : ScriptableObject
	{
		[InlineEditor] [SerializeField]
		private List<UID> _uids = new();

		// Runtime lookups for performance
		private Dictionary<string, UID> _guidLookup;
		private Dictionary<string, UID> _nameLookup; // Fallback lookup by asset name

		public void Initialize()
		{
			BuildLookups();
		}

		private void BuildLookups()
		{
			_guidLookup = new Dictionary<string, UID>();
			_nameLookup = new Dictionary<string, UID>();

			foreach (var uid in _uids)
			{
				if (uid != null && !uid.IsEmpty())
				{
					_guidLookup[uid.Id] = uid;
					
					// Build name lookup (name is the asset filename, stable across GUID changes)
					if (!string.IsNullOrEmpty(uid.name))
					{
						_nameLookup[uid.name] = uid;
					}
				}
			}
		}

		public UID GetUID(string guid)
		{
			if (_guidLookup == null)
				Initialize();

			return _guidLookup.TryGetValue(guid, out var uid) ? uid : null;
		}

		/// <summary>
		/// Fallback lookup by asset name.
		/// Used when a UID's GUID has changed but the asset name remains the same.
		/// </summary>
		/// <param name="name">The asset name (filename) of the UID</param>
		/// <returns>The UID if found, null otherwise</returns>
		public UID GetUIDByName(string name)
		{
			if (_nameLookup == null)
				Initialize();

			return _nameLookup.TryGetValue(name, out var uid) ? uid : null;
		}

		// Editor helpers (same as before)
#if UNITY_EDITOR
		[Button("Refresh All UIDs In Project")]
		[ContextMenu("Refresh All UIDs In Project")]
		public void RefreshAllUIDs()
		{
			var allUIDs = AssetDatabase.FindAssets("t:UID")
			                           .Select(AssetDatabase.GUIDToAssetPath)
			                           .Select(AssetDatabase.LoadAssetAtPath<UID>)
			                           .Where(uid => uid != null && !uid.IsEmpty())
			                           .ToList();

			_uids = allUIDs;
			EditorUtility.SetDirty(this);
		}

		[Button("Validate Registry")]
		[ContextMenu("Validate Registry")]
		private void ValidateRegistry()
		{
			var duplicates = _uids
			                 .Where(uid => uid != null && !uid.IsEmpty())
			                 .GroupBy(uid => uid.Id)
			                 .Where(g => g.Count() > 1)
			                 .ToList();

			if (duplicates.Any())
			{
				Debug.LogWarning($"Found {duplicates.Count} duplicate GUIDs in registry!");
			}

			var empties = _uids.Where(uid => uid == null || uid.IsEmpty()).ToList();
			if (empties.Any())
			{
				Debug.LogWarning($"Found {empties.Count} empty or null UIDs!");
			}
		}

		[Button]
		public void PrintName(string uid)
		{
			if (_guidLookup == null)
			{
				Initialize();
			}

			if (_guidLookup.TryGetValue(uid, out UID value))
			{
				Debug.Log(value.name);
			}
			else
			{
				Debug.Log("Not Found!");
			}
		}
#endif
	}
}