using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Exports the project's identity catalog as JSON: every UID asset with its Uid, type,
	/// name, path, provenance, and external key when it has one, plus every redirect. This is
	/// the contract a server, analytics warehouse, or sibling project consumes so that the
	/// same Uid means the same thing everywhere. Deterministic ordering keeps diffs reviewable.
	/// </summary>
	public static class UidCatalogExporter
	{
		private const string DefaultPath = "Build/uid-catalog.json";

		[MenuItem(UidEditorUtility.MenuRoot + "Export Catalog (JSON)", priority = 60)]
		public static void ExportInteractive()
		{
			string path = EditorUtility.SaveFilePanel("Export UID catalog", Path.GetDirectoryName(DefaultPath), Path.GetFileName(DefaultPath), "json");
			if (string.IsNullOrEmpty(path)) return;

			Export(path);
			EditorUtility.RevealInFinder(path);
		}

		/// <summary>Batch entry: <c>-executeMethod AK.Core.Editor.UidCatalogExporter.ExportForCI</c>. Writes to Build/uid-catalog.json.</summary>
		public static void ExportForCI()
		{
			Export(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, DefaultPath));
		}

		public static void Export(string absolutePath)
		{
			var catalog = Build();
			string json = JsonUtility.ToJson(catalog, prettyPrint: true);

			Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
			File.WriteAllText(absolutePath, json, new UTF8Encoding(false));
			Debug.Log($"[UID] Exported {catalog.assets.Count} identities and {catalog.redirects.Count} redirects to {absolutePath}");
		}

		public static Catalog Build()
		{
			var catalog = new Catalog
			{
				schema     = 1,
				generated  = DateTime.UtcNow.ToString("O"),
				project    = Application.productName,
				version    = Application.version,
			};

			foreach (UID asset in UidEditorUtility.LoadAllUidAssets())
			{
				if (!asset.HasIdentity) continue;

				catalog.assets.Add(new CatalogAsset
				{
					uid         = asset.Id.ToString(),
					type        = asset.GetType().FullName,
					name        = asset.name,
					path        = AssetDatabase.GetAssetPath(asset),
					provenance  = asset.Provenance.ToString(),
					source      = asset.ProvenanceSource ?? string.Empty,
					externalKey = ExternalKeyOf(asset),
				});
			}

			catalog.assets.Sort((a, b) => string.CompareOrdinal(a.uid, b.uid));

			foreach (UidRedirectTable table in UidEditorUtility.LoadAllRedirectTables())
			{
				foreach (var e in table.Entries)
				{
					if (e.From.IsNone || e.To.IsNone) continue;
					catalog.redirects.Add(new CatalogRedirect { from = e.From.ToString(), to = e.To.ToString(), reason = e.Reason ?? string.Empty, date = e.Date ?? string.Empty });
				}
			}

			catalog.redirects.Sort((a, b) => string.CompareOrdinal(a.from, b.from));
			return catalog;
		}

		private static readonly string[] ExternalKeyFieldNames = { "EventID", "ProductID", "VariableKey", "PlacementID", "NotificationID" };

		private static string ExternalKeyOf(UID asset)
		{
			Type type = asset.GetType();
			foreach (string name in ExternalKeyFieldNames)
			{
				var field = type.GetField(name);
				if (field != null && field.FieldType == typeof(string))
				{
					return field.GetValue(asset) as string ?? string.Empty;
				}
			}

			return string.Empty;
		}

		[Serializable]
		public sealed class Catalog
		{
			public int    schema;
			public string generated;
			public string project;
			public string version;
			public List<CatalogAsset>    assets    = new();
			public List<CatalogRedirect> redirects = new();
		}

		[Serializable]
		public sealed class CatalogAsset
		{
			public string uid;
			public string type;
			public string name;
			public string path;
			public string provenance;
			public string source;
			public string externalKey;
		}

		[Serializable]
		public sealed class CatalogRedirect
		{
			public string from;
			public string to;
			public string reason;
			public string date;
		}
	}
}
