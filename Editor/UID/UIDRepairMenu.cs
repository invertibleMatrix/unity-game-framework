using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// One-pass repair for UID identity damage:
	///   Phase 1 — every UID asset with an empty logical GUID on disk gets a fresh,
	///             persisted identity (the OnValidate regen previously never saved).
	///   Phase 2 — every UIDRef link in ScriptableObjects and prefabs whose stored GUID
	///             no longer resolves is healed by asset name and rewritten.
	/// </summary>
	public static class UIDRepairMenu
	{
		[MenuItem("Tools/UGFW/Repair UID Assets (Empty IDs + Broken Links)")]
		private static void Repair()
		{
			int identitiesFixed = RepairEmptyIdentities();
			int linksHealed = HealBrokenLinks();

			AssetDatabase.SaveAssets();
			Debug.Log($"[UIDRepair] Done — {identitiesFixed} empty identities fixed, {linksHealed} broken links healed.");
		}

		private static int RepairEmptyIdentities()
		{
			int fixedCount = 0;

			foreach (string guid in AssetDatabase.FindAssets("t:UID"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var uid = AssetDatabase.LoadAssetAtPath<UID>(path);

				if (uid != null && uid.IsEmpty())
				{
					uid.GenerateNewGuid();
					EditorUtility.SetDirty(uid);
					fixedCount++;
					Debug.Log($"[UIDRepair] '{path}' had an empty identity — assigned {uid.Id}.", uid);
				}
			}

			return fixedCount;
		}

		private static int HealBrokenLinks()
		{
			var byGuid = new Dictionary<string, UID>();
			var byName = new Dictionary<string, UID>();

			foreach (string guid in AssetDatabase.FindAssets("t:UID"))
			{
				var uid = AssetDatabase.LoadAssetAtPath<UID>(AssetDatabase.GUIDToAssetPath(guid));
				if (uid == null || uid.IsEmpty()) continue;

				byGuid[uid.Id] = uid;
				byName.TryAdd(uid.name, uid);
			}

			int healed = 0;

			foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
				if (asset != null)
				{
					healed += HealLinksInObject(asset, byGuid, byName);
				}
			}

			foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (prefab == null) continue;

				foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
				{
					if (component != null)
					{
						healed += HealLinksInObject(component, byGuid, byName);
					}
				}
			}

			return healed;
		}

		private static int HealLinksInObject(Object target, Dictionary<string, UID> byGuid, Dictionary<string, UID> byName)
		{
			int healed = 0;
			var so = new SerializedObject(target);
			SerializedProperty property = so.GetIterator();

			while (property.Next(true))
			{
				if (property.propertyType != SerializedPropertyType.Generic) continue;

				var guidProp = property.FindPropertyRelative("_guid");
				var nameProp = property.FindPropertyRelative("_assetName");
				if (guidProp == null || nameProp == null) continue;
				if (string.IsNullOrEmpty(guidProp.stringValue)) continue;

				if (!byGuid.ContainsKey(guidProp.stringValue) &&
				    byName.TryGetValue(nameProp.stringValue, out UID healedTarget))
				{
					guidProp.stringValue = healedTarget.Id;
					healed++;
					Debug.Log($"[UIDRepair] Healed link '{property.displayName}' on '{target.name}' → '{healedTarget.name}'.", target);
				}
			}

			if (healed > 0)
			{
				so.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(target);
			}

			return healed;
		}
	}
}
