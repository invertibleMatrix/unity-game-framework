using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

#if UGFW_ADDRESSABLES
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace AK.Core.Editor
{
	/// <summary>
	/// The project-wide identity audit. One pass produces a <see cref="UidAuditReport"/> that
	/// the menu prints, the build validator gates on, and CI reads from the log.
	///
	/// Rules:
	///   missing-identity   an asset with no Uid                                        ERROR
	///   collision          a Uid owned by more than one asset                          ERROR
	///   registry-null      a registry entry pointing at a deleted asset                ERROR
	///   registry-untracked a UID asset of a registered kind that no registry tracks    WARNING
	///   registry-overlap   an asset tracked by more than one registry                  WARNING
	///   redirect-dangling  a redirect whose chain ends at an identity nobody owns      ERROR
	///   redirect-live      a redirect FROM an identity a live asset still owns         WARNING
	///   redirect-cycle     a redirect chain that never terminates                      ERROR
	///   addressable-uid    a UID asset inside an Addressables group (duplication risk) ERROR
	///   persisted-asset    a [Serializable] persisted type holding a UID reference     ERROR
	///   external-key       a definition with an external key that is empty/duplicated  WARNING
	/// </summary>
	public static class UidAuditMenu
	{
		[MenuItem(UidEditorUtility.MenuRoot + "Audit Project", priority = 0)]
		public static void RunAudit()
		{
			UidAuditReport report = Audit();

			if (report.HasErrors)      Debug.LogError(report.ToString());
			else if (report.WarningCount > 0) Debug.LogWarning(report.ToString());
			else                       Debug.Log(report.ToString());

			if (UidIdentityAuthority.UnresolvedCollisions.Count > 0)
			{
				UidCollisionResolverWindow.Open();
			}
		}

		[MenuItem(UidEditorUtility.MenuRoot + "Repair Missing Identities", priority = 1)]
		public static void RepairMissing()
		{
			UidEditorIndex.Invalidate();
			bool clean = UidIdentityAuthority.RepairMissingAndReport();
			Debug.Log(clean
				? "[UID] Repair complete. No collisions."
				: "[UID] Repair complete. Collisions remain — open Tools → UGFW → UID → Resolve Collisions.");
		}

		/// <summary>
		/// Batch-mode entry: <c>-executeMethod AK.Core.Editor.UidAuditMenu.AuditForCI</c>.
		/// Exit code 0 when clean, 1 when any error remains. Never mutates assets.
		/// </summary>
		public static void AuditForCI()
		{
			UidAuditReport report = Audit();
			Debug.Log(report.ToString());

			if (Application.isBatchMode)
			{
				EditorApplication.Exit(report.HasErrors ? 1 : 0);
			}
		}

		public static UidAuditReport Audit()
		{
			var report = new UidAuditReport();

			UidEditorIndex.Invalidate();
			UidIdentityAuthority.DetectCollisions();

			List<UID> assets                    = UidEditorUtility.LoadAllUidAssets();
			List<UidRegistryAssetBase> registries = UidEditorUtility.LoadAllRegistries();
			List<UidRedirectTable> redirects    = UidEditorUtility.LoadAllRedirectTables();

			report.AssetsScanned     = assets.Count;
			report.RegistriesScanned = registries.Count;
			report.IdentitiesIndexed = UidEditorIndex.Count;

			AuditIdentities(report);
			AuditRegistries(report, assets, registries);
			AuditRedirects(report, redirects);
			AuditAddressables(report);
			AuditPersistedTypes(report);
			AuditExternalKeys(report, assets);

			return report;
		}

		// ---------------------------------------------------------------- identities

		private static void AuditIdentities(UidAuditReport report)
		{
			foreach (string path in UidEditorIndex.AssetsWithoutIdentity)
			{
				report.Error("missing-identity", "Asset has no identity. Run Repair Missing Identities.", path);
			}

			foreach (var collision in UidEditorIndex.Collisions())
			{
				var paths = new List<string>(collision.Value.Count);
				foreach (var owner in collision.Value) paths.Add(owner.AssetPath);
				report.Error("collision", $"Identity {collision.Key.ToShortString()} owned by {paths.Count} assets: {string.Join(" | ", paths)}");
			}
		}

		// ---------------------------------------------------------------- registries

		private static void AuditRegistries(UidAuditReport report, List<UID> assets, List<UidRegistryAssetBase> registries)
		{
			var trackedBy = new Dictionary<UID, List<UidRegistryAssetBase>>();

			foreach (var registry in registries)
			{
				string registryPath = UidEditorUtility.PathOf(registry);
				foreach (UID obj in registry.GetTrackedObjects())
				{
					if (obj == null)
					{
						report.Error("registry-null", $"'{registry.name}' lists a deleted asset. Use Remove Deleted.", registryPath);
						continue;
					}

					if (!trackedBy.TryGetValue(obj, out var list))
					{
						list = new List<UidRegistryAssetBase>(1);
						trackedBy[obj] = list;
					}

					list.Add(registry);
				}
			}

			foreach (var kv in trackedBy)
			{
				if (kv.Value.Count > 1)
				{
					var names = new List<string>(kv.Value.Count);
					foreach (var r in kv.Value) names.Add(r.name);
					report.Warning("registry-overlap", $"'{kv.Key.name}' is tracked by {names.Count} registries: {string.Join(", ", names)}. Aggregate resolution takes the first.", UidEditorUtility.PathOf(kv.Key));
				}
			}

			foreach (UID asset in assets)
			{
				if (asset is UidNamespace || trackedBy.ContainsKey(asset)) continue;

				var registry = UidEditorUtility.FindRegistryFor(asset, registries);
				if (registry != null)
				{
					report.Warning("registry-untracked", $"'{asset.name}' ({asset.GetType().Name}) is not tracked by '{registry.name}'. Refresh All From Project.", UidEditorUtility.PathOf(asset));
				}
			}
		}

		// ---------------------------------------------------------------- redirects

		private static void AuditRedirects(UidAuditReport report, List<UidRedirectTable> tables)
		{
			foreach (var table in tables)
			{
				string tablePath = UidEditorUtility.PathOf(table);
				var map = new Dictionary<Uid, Uid>();
				foreach (var e in table.Entries)
				{
					if (e.From.IsNone || e.To.IsNone || e.From == e.To) continue;
					map[e.From] = e.To;
				}

				foreach (var e in table.Entries)
				{
					if (e.From.IsNone || e.To.IsNone || e.From == e.To) continue;

					if (UidEditorIndex.IsOwned(e.From))
					{
						report.Warning("redirect-live", $"Redirect from {e.From.ToShortString()} but a live asset still owns it.", tablePath);
					}

					if (HasCycle(map, e.From))
					{
						report.Error("redirect-cycle", $"Redirect chain from {e.From.ToShortString()} never terminates.", tablePath);
						continue;
					}

					Uid final = table.Follow(e.From);
					if (!UidEditorIndex.IsOwned(final))
					{
						report.Error("redirect-dangling", $"Redirect from {e.From.ToShortString()} ends at {final.ToShortString()} which no asset owns.", tablePath);
					}
				}
			}
		}

		private static bool HasCycle(Dictionary<Uid, Uid> map, Uid start)
		{
			var seen = new HashSet<Uid> { start };
			Uid current = start;
			while (map.TryGetValue(current, out Uid next))
			{
				if (!seen.Add(next)) return true;
				current = next;
			}

			return false;
		}

		// ---------------------------------------------------------------- addressables

		private static void AuditAddressables(UidAuditReport report)
		{
#if UGFW_ADDRESSABLES
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
			if (settings == null) return;

			foreach (AddressableAssetGroup group in settings.groups)
			{
				if (group == null) continue;

				foreach (AddressableAssetEntry entry in group.entries)
				{
					if (entry == null || string.IsNullOrEmpty(entry.AssetPath)) continue;

					Type mainType = AssetDatabase.GetMainAssetTypeAtPath(entry.AssetPath);
					if (mainType != null && typeof(UID).IsAssignableFrom(mainType))
					{
						report.Error("addressable-uid", $"Identity asset is in Addressables group '{group.Name}'. A bundled UID asset is duplicated per bundle that references it; keep identity assets in the main build and reference them by Uid.", entry.AssetPath);
						continue;
					}

					if (AssetDatabase.IsValidFolder(entry.AssetPath))
					{
						foreach (string guid in AssetDatabase.FindAssets("t:UID", new[] { entry.AssetPath }))
						{
							report.Error("addressable-uid", $"Identity asset is inside Addressables folder entry '{entry.address}' (group '{group.Name}').", AssetDatabase.GUIDToAssetPath(guid));
						}
					}
				}
			}
#endif
		}

		// ---------------------------------------------------------------- persisted types

		/// <summary>
		/// Persisted state must hold identities by value. A UID (asset) field inside a
		/// PersistableState subclass or any [Serializable] type it nests would serialize as
		/// an instance id and dangle after restart.
		/// </summary>
		private static void AuditPersistedTypes(UidAuditReport report)
		{
			var visited = new HashSet<Type>();

			foreach (Type type in TypeCache.GetTypesDerivedFrom(typeof(object)))
			{
				if (!IsPersistableState(type)) continue;
				WalkPersisted(type, type.Name, report, visited);
			}
		}

		private static bool IsPersistableState(Type type)
		{
			for (Type t = type.BaseType; t != null; t = t.BaseType)
			{
				if (t.IsGenericType && t.GetGenericTypeDefinition().FullName == "AK.Core.PersistableState`1") return true;
			}

			return false;
		}

		private static void WalkPersisted(Type type, string path, UidAuditReport report, HashSet<Type> visited)
		{
			if (!visited.Add(type)) return;

			foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (field.IsNotSerialized || field.IsDefined(typeof(NonSerializedAttribute), false)) continue;
				if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), false)) continue;

				Type fieldType = field.FieldType;
				Type element   = fieldType.IsArray ? fieldType.GetElementType()
				               : fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>) ? fieldType.GetGenericArguments()[0]
				               : fieldType;

				if (element == null) continue;

				if (typeof(UID).IsAssignableFrom(element))
				{
					report.Error("persisted-asset", $"{path}.{field.Name} holds a UID asset reference in persisted state. Store Uid / Uid<T> instead.");
					continue;
				}

				if (element.IsClass && element != typeof(string) && element.IsDefined(typeof(SerializableAttribute), false) && !typeof(UnityEngine.Object).IsAssignableFrom(element))
				{
					WalkPersisted(element, $"{path}.{field.Name}", report, visited);
				}
			}
		}

		// ---------------------------------------------------------------- external keys

		/// <summary>
		/// Definitions that speak to external systems carry a string key (EventID, ProductID,
		/// VariableKey). The Uid is internal; the key is what the outside sees. Empty or
		/// duplicated keys within a kind mean two definitions look identical to that system.
		/// </summary>
		private static void AuditExternalKeys(UidAuditReport report, List<UID> assets)
		{
			var byKind = new Dictionary<Type, Dictionary<string, UID>>();

			foreach (UID asset in assets)
			{
				FieldInfo keyField = FindExternalKeyField(asset.GetType());
				if (keyField == null) continue;

				string key = keyField.GetValue(asset) as string;
				if (string.IsNullOrWhiteSpace(key))
				{
					report.Warning("external-key", $"'{asset.name}' has an empty {keyField.Name}; external systems cannot address it.", UidEditorUtility.PathOf(asset));
					continue;
				}

				Type kind = keyField.DeclaringType ?? asset.GetType();
				if (!byKind.TryGetValue(kind, out var seen))
				{
					seen = new Dictionary<string, UID>(StringComparer.Ordinal);
					byKind[kind] = seen;
				}

				if (seen.TryGetValue(key, out UID first))
				{
					report.Warning("external-key", $"'{asset.name}' and '{first.name}' share {keyField.Name} '{key}'.", UidEditorUtility.PathOf(asset));
				}
				else
				{
					seen[key] = asset;
				}
			}
		}

		private static readonly string[] ExternalKeyFieldNames = { "EventID", "ProductID", "VariableKey", "PlacementID", "NotificationID" };

		private static FieldInfo FindExternalKeyField(Type type)
		{
			foreach (string name in ExternalKeyFieldNames)
			{
				FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null && field.FieldType == typeof(string)) return field;
			}

			return null;
		}
	}
}
