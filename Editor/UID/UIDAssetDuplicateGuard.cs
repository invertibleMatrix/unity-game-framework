using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Duplicating a UID-bearing asset (Ctrl+D) copies its file content — including the
	/// logical GUID — giving two assets one identity. Regeneration tooling then rewrites
	/// identities and every stored link dangles. This guard detects a UID asset being
	/// born with an identity that already exists and assigns the newcomer a fresh GUID.
	/// </summary>
	public class UIDAssetDuplicateGuard : AssetPostprocessor
	{
		private static void OnWillCreateAsset(string assetPath)
		{
			// The asset doesn't exist yet at this point — defer until it does.
			EditorApplication.delayCall += () => Guard(path: assetPath);
		}

		private static void Guard(string path)
		{
			if (!path.EndsWith(".asset")) return;

			var uid = AssetDatabase.LoadAssetAtPath<UID>(path);
			if (uid == null || uid.IsEmpty()) return;

			foreach (string otherGuid in AssetDatabase.FindAssets("t:UID"))
			{
				string otherPath = AssetDatabase.GUIDToAssetPath(otherGuid);
				if (otherPath == path) continue;

				var other = AssetDatabase.LoadAssetAtPath<UID>(otherPath);
				if (other != null && other.Id == uid.Id)
				{
					uid.GenerateNewGuid();
					EditorUtility.SetDirty(uid);
					AssetDatabase.SaveAssets();

					Debug.LogWarning(
						$"[UID] '{path}' was created with an identity already held by '{otherPath}' (asset duplication copies the logical GUID). Assigned a fresh GUID. Prefer creating UID assets from the Create menu.",
						uid);
					return;
				}
			}
		}
	}
}
