using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Inspector for every UID-derived asset. The identity block sits above the subclass
	/// fields: read-only hex with copy, provenance, registry membership, and (when things are
	/// wrong) the exact problem and the button that fixes it. Identity is never editable by
	/// hand — the only writes go through <see cref="UidIdentityAuthority"/>.
	///
	/// Custom editors for UID subclasses that replace this one can call
	/// <see cref="DrawIdentityBlock"/> to keep the block.
	/// </summary>
	[CustomEditor(typeof(UID), true)]
	[CanEditMultipleObjects]
	public class UidAssetEditor : UnityEditor.Editor
	{
		private static readonly string[] IdentityFields = { "m_Script", "_id", "_provenance", "_provenanceSource" };

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			if (targets.Length == 1)
			{
				DrawIdentityBlock((UID)target);
			}
			else
			{
				DrawMultiIdentity();
			}

			EditorGUILayout.Space(6);
			DrawPropertiesExcluding(serializedObject, IdentityFields);

			serializedObject.ApplyModifiedProperties();
		}

		public static void DrawIdentityBlock(UID asset)
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				if (!asset.HasIdentity)
				{
					EditorGUILayout.HelpBox("This asset has no identity. Nothing can reference, persist, or resolve it until one is minted.", MessageType.Error);
					if (GUILayout.Button("Mint Identity"))
					{
						UidIdentityAuthority.Mint(asset, AssetDatabase.GetAssetPath(asset), "inspector");
						GUIUtility.ExitGUI();
					}

					return;
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.PrefixLabel("Identity");
					EditorGUILayout.SelectableLabel(asset.Id.ToString(), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					if (GUILayout.Button("Copy", GUILayout.Width(48)))
					{
						UidEditorUtility.CopyToClipboard(asset.Id.ToString());
					}
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.PrefixLabel("Provenance");
					string provenance = asset.Provenance.ToString();
					if (!string.IsNullOrEmpty(asset.ProvenanceSource)) provenance += $"  ·  {asset.ProvenanceSource}";
					EditorGUILayout.LabelField(provenance, EditorStyles.miniLabel);
				}

				DrawRegistryLine(asset);
				DrawCollisionLine(asset);
			}
		}

		private static void DrawRegistryLine(UID asset)
		{
			if (asset is UidNamespace) return;

			UidRegistryAssetBase registry = RegistryCache.For(asset);

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.PrefixLabel("Registry");
				if (registry == null)
				{
					EditorGUILayout.LabelField("none — not resolvable by identity at runtime", EditorStyles.miniLabel);
					return;
				}

				bool tracked = registry.TryResolveUntyped(asset.Id, out UID resolved) && resolved == asset;
				EditorGUILayout.ObjectField(registry, typeof(UidRegistryAssetBase), false);
				if (!tracked && GUILayout.Button("Track", GUILayout.Width(52)))
				{
					registry.Editor_TryTrack(asset);
					AssetDatabase.SaveAssetIfDirty(registry);
				}
			}
		}

		private static void DrawCollisionLine(UID asset)
		{
			if (!UidEditorIndex.TryGetOwners(asset.Id, out var owners) || owners.Count < 2) return;

			EditorGUILayout.HelpBox($"Identity shared by {owners.Count} assets. Builds are blocked until resolved.", MessageType.Error);
			if (GUILayout.Button("Open Collision Resolver"))
			{
				UidCollisionResolverWindow.Open();
			}
		}

		private void DrawMultiIdentity()
		{
			int missing = 0;
			foreach (var t in targets)
			{
				if (t is UID u && !u.HasIdentity) missing++;
			}

			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				EditorGUILayout.LabelField($"{targets.Length} identity assets selected", EditorStyles.miniLabel);
				if (missing > 0)
				{
					EditorGUILayout.HelpBox($"{missing} without identity.", MessageType.Error);
					if (GUILayout.Button("Mint Missing"))
					{
						foreach (var t in targets)
						{
							if (t is UID u && !u.HasIdentity) UidIdentityAuthority.Mint(u, AssetDatabase.GetAssetPath(u), "inspector (multi)");
						}

						GUIUtility.ExitGUI();
					}
				}
			}
		}

		/// <summary>Registry-per-asset lookup, refreshed only when the project index changes.</summary>
		private static class RegistryCache
		{
			private static System.Collections.Generic.List<UidRegistryAssetBase> _registries;
			private static int _version = -1;

			public static UidRegistryAssetBase For(UID asset)
			{
				if (_registries == null || _version != UidEditorIndex.Version)
				{
					_registries = UidEditorUtility.LoadAllRegistries();
					_version    = UidEditorIndex.Version;
				}

				return UidEditorUtility.FindRegistryFor(asset, _registries);
			}
		}
	}
}
