using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Shared helpers for the UID editor tooling. Everything here is a thin wrapper over
	/// AssetDatabase so the tools agree on how to find, load, and describe identity assets.
	/// </summary>
	public static class UidEditorUtility
	{
		public const string MenuRoot = "Tools/UGFW/UID/";

		/// <summary>Every UID-derived asset in the project (main assets only), via one type query.</summary>
		public static List<UID> LoadAllUidAssets()
		{
			var result = new List<UID>();
			foreach (string guid in AssetDatabase.FindAssets("t:UID"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var asset = AssetDatabase.LoadAssetAtPath<UID>(path);
				if (asset != null) result.Add(asset);
			}

			return result;
		}

		public static List<UidRegistryAssetBase> LoadAllRegistries()
		{
			var result = new List<UidRegistryAssetBase>();
			foreach (string guid in AssetDatabase.FindAssets("t:UidRegistryAssetBase"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var asset = AssetDatabase.LoadAssetAtPath<UidRegistryAssetBase>(path);
				if (asset != null) result.Add(asset);
			}

			return result;
		}

		public static List<UidRedirectTable> LoadAllRedirectTables()
		{
			var result = new List<UidRedirectTable>();
			foreach (string guid in AssetDatabase.FindAssets("t:UidRedirectTable"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var asset = AssetDatabase.LoadAssetAtPath<UidRedirectTable>(path);
				if (asset != null) result.Add(asset);
			}

			return result;
		}

		/// <summary>The registry whose element type accepts this asset, or null. First match wins; validators report ambiguity.</summary>
		public static UidRegistryAssetBase FindRegistryFor(UID asset, IReadOnlyList<UidRegistryAssetBase> registries)
		{
			if (asset == null) return null;

			Type assetType = asset.GetType();
			UidRegistryAssetBase best = null;
			int bestDepth = int.MaxValue;

			foreach (var registry in registries)
			{
				if (registry == null || !registry.ElementType.IsAssignableFrom(assetType)) continue;

				int depth = InheritanceDistance(assetType, registry.ElementType);
				if (depth < bestDepth)
				{
					best = registry;
					bestDepth = depth;
				}
			}

			return best;
		}

		private static int InheritanceDistance(Type from, Type to)
		{
			int depth = 0;
			for (Type t = from; t != null; t = t.BaseType, depth++)
			{
				if (t == to) return depth;
			}

			return int.MaxValue;
		}

		public static string PathOf(UnityEngine.Object asset) => asset != null ? AssetDatabase.GetAssetPath(asset) : "<null>";

		/// <summary>Stable canonical name for deterministic minting: the asset's file name without extension.</summary>
		public static string CanonicalName(string assetPath) => Path.GetFileNameWithoutExtension(assetPath);

		/// <summary>
		/// Walks up from an asset path looking for a UidNamespace asset in the same or a parent
		/// folder. Namespaced folders yield deterministic identities for assets created in them.
		/// </summary>
		public static UidNamespace FindNamespaceFor(string assetPath)
		{
			string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
			while (!string.IsNullOrEmpty(dir) && dir.StartsWith("Assets"))
			{
				foreach (string guid in AssetDatabase.FindAssets("t:UidNamespace", new[] { dir }))
				{
					string path = AssetDatabase.GUIDToAssetPath(guid);
					if (Path.GetDirectoryName(path)?.Replace('\\', '/') != dir) continue;

					var ns = AssetDatabase.LoadAssetAtPath<UidNamespace>(path);
					if (ns != null && ns.HasIdentity) return ns;
				}

				dir = Path.GetDirectoryName(dir)?.Replace('\\', '/');
			}

			return null;
		}

		public static void SaveAsset(UnityEngine.Object asset)
		{
			if (asset == null) return;
			EditorUtility.SetDirty(asset);
			AssetDatabase.SaveAssetIfDirty(asset);
		}

		public static string Describe(UID asset)
		{
			if (asset == null) return "<null>";
			return asset.HasIdentity
				? $"{asset.name} [{asset.Id.ToShortString()}] ({PathOf(asset)})"
				: $"{asset.name} [no identity] ({PathOf(asset)})";
		}

		public static void CopyToClipboard(string text)
		{
			EditorGUIUtility.systemCopyBuffer = text ?? string.Empty;
		}
	}
}
