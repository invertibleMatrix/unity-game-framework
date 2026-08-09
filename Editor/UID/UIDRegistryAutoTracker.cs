using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Keeps every UIDRegistryAsset in sync with the project automatically: UID assets
	/// that appear (created, duplicated, imported, subtree pulls) are tracked by every
	/// registry whose element type accepts them; deleted assets are swept out. Kills the
	/// "forgot to Refresh All" failure class — registries are never stale.
	/// </summary>
	public class UIDRegistryAutoTracker : AssetPostprocessor
	{
		private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
		                                           string[] movedAssets, string[] movedFromAssetPaths)
		{
			bool anyDeleted = deletedAssets.Length > 0;
			var importedUIDs = new System.Collections.Generic.List<UID>();

			foreach (string path in importedAssets)
			{
				if (!path.EndsWith(".asset")) continue;

				var uid = AssetDatabase.LoadAssetAtPath<UID>(path);
				if (uid != null)
				{
					importedUIDs.Add(uid);
				}
			}

			if (importedUIDs.Count == 0 && !anyDeleted) return;

			bool changed = false;

			foreach (string registryGuid in AssetDatabase.FindAssets("t:UIDRegistryAsset"))
			{
				var registry = AssetDatabase.LoadAssetAtPath<UIDRegistryAsset>(
					AssetDatabase.GUIDToAssetPath(registryGuid));
				if (registry == null) continue;

				foreach (UID uid in importedUIDs)
				{
					if (registry.TryTrackAsset(uid))
					{
						changed = true;
					}
				}

				if (anyDeleted && registry.RemoveNullEntries() > 0)
				{
					changed = true;
				}
			}

			if (changed)
			{
				AssetDatabase.SaveAssets();
			}
		}
	}
}
