using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Keeps every registry asset in sync with the project without anyone pressing Refresh.
	/// A UID asset that appears is tracked by the most specific registry whose element type
	/// accepts it; one that disappears is untracked; a move is a no-op for tracking (Unity
	/// references survive moves) but still triggers a null sweep in case the asset was
	/// deleted mid-move.
	///
	/// Runs after <see cref="UidIdentityAuthority"/> (higher postprocess order) so the asset
	/// already has its identity when it enters a registry.
	/// </summary>
	public sealed class UidRegistryAutoTracker : AssetPostprocessor
	{
		public override int GetPostprocessOrder() => 100;

		private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
		{
			List<UidRegistryAssetBase> registries = null;
			HashSet<UidRegistryAssetBase> dirtied = null;

			// The type is read before anything loads, so only UID assets are loaded.
			foreach (string path in imported)
			{
				if (!UidEditorUtility.IsUidAsset(path)) continue;

				var asset = AssetDatabase.LoadAssetAtPath<UID>(path);
				if (asset == null || asset is UidNamespace) continue;

				registries ??= UidEditorUtility.LoadAllRegistries();
				var registry = UidEditorUtility.FindRegistryFor(asset, registries);
				if (registry != null && registry.Editor_TryTrack(asset))
				{
					(dirtied ??= new HashSet<UidRegistryAssetBase>()).Add(registry);
				}
			}

			// A deleted file's type can't be read, so any deleted .asset file sweeps the registries.
			if (Array.Exists(deleted, UidEditorUtility.IsAssetFile))
			{
				registries ??= UidEditorUtility.LoadAllRegistries();
				foreach (var registry in registries)
				{
					if (registry.Editor_RemoveNullEntries() > 0) (dirtied ??= new HashSet<UidRegistryAssetBase>()).Add(registry);
				}
			}

			if (dirtied == null) return;

			foreach (var registry in dirtied)
			{
				AssetDatabase.SaveAssetIfDirty(registry);
			}
		}

		/// <summary>Full resync of every registry from the project. The "I don't trust the state" button.</summary>
		[MenuItem(UidEditorUtility.MenuRoot + "Registries/Refresh All From Project", priority = 40)]
		public static void RefreshAll()
		{
			var registries = UidEditorUtility.LoadAllRegistries();
			int total = 0;

			foreach (var registry in registries)
			{
				total += registry.Editor_RefreshFromProject();
				AssetDatabase.SaveAssetIfDirty(registry);
			}

			Debug.Log($"[UID] Refreshed {registries.Count} registr{(registries.Count == 1 ? "y" : "ies")} — {total} tracked assets.");
		}
	}
}
