using System;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Project-wide registry maintenance: finds every UIDRegistryAsset subclass via one
	/// AssetDatabase type query and runs the operation across all of them. This is the
	/// scalable path when per-asset buttons don't scale — and the entry point to reuse
	/// from CI or a build preprocessor.
	/// </summary>
	public static class RegistryMaintenanceMenu
	{
		[MenuItem("Tools/UGFW/Registries/Refresh All Registries")]
		private static void RefreshAllRegistries()
		{
			ForEachRegistry("Refresh", registry => registry.RefreshAllObjects(), saveAfter: true);
		}

		[MenuItem("Tools/UGFW/Registries/Validate All Registries")]
		private static void ValidateAllRegistries()
		{
			ForEachRegistry("Validate", registry => registry.ValidateObjects(), saveAfter: false);
		}

		[MenuItem("Tools/UGFW/Registries/Regenerate All Registry UIDs...")]
		private static void RegenerateAllRegistryUIDs()
		{
			bool confirmed = EditorUtility.DisplayDialog("Regenerate All Registry UIDs",
				"Every registry will regenerate duplicate and missing UIDs on its tracked assets. References stored by GUID will need re-healing. Continue?",
				"Regenerate", "Cancel");

			if (confirmed)
			{
				ForEachRegistry("Regenerate UIDs", registry => registry.RegenerateUIDs(), saveAfter: true);
			}
		}

		private static void ForEachRegistry(string operation, Action<UIDRegistryAsset> action, bool saveAfter)
		{
			string[] guids = AssetDatabase.FindAssets("t:UIDRegistryAsset");
			int processed = 0;

			try
			{
				for (int i = 0; i < guids.Length; i++)
				{
					string path = AssetDatabase.GUIDToAssetPath(guids[i]);
					EditorUtility.DisplayProgressBar($"Registry Maintenance — {operation}", path, (float)i / guids.Length);

					var registry = AssetDatabase.LoadAssetAtPath<UIDRegistryAsset>(path);
					if (registry == null) continue;

					action(registry);
					processed++;
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			if (saveAfter)
			{
				AssetDatabase.SaveAssets();
			}

			Debug.Log($"[RegistryMaintenance] {operation} complete — {processed} registries processed.");
		}
	}
}
