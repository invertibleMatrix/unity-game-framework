using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Where a human decides which asset keeps a shared identity. For each collision the
	/// window lists every owner with its path, provenance, and registry membership, and
	/// offers one action per owner: "Keep" (this one survives, the others are re-minted).
	/// Imported identities cannot be chosen as losers; the window says so instead of failing.
	/// </summary>
	public sealed class UidCollisionResolverWindow : EditorWindow
	{
		private Vector2 _scroll;
		private List<UidRegistryAssetBase> _registries;

		[MenuItem(UidEditorUtility.MenuRoot + "Resolve Collisions", priority = 20)]
		public static void Open()
		{
			var window = GetWindow<UidCollisionResolverWindow>("UID Collisions");
			window.minSize = new Vector2(520, 240);
			window.Refresh();
		}

		private void OnEnable()
		{
			UidIdentityAuthority.CollisionsChanged += Repaint;
			UidEditorIndex.Rebuilt                 += Repaint;
		}

		private void OnDisable()
		{
			UidIdentityAuthority.CollisionsChanged -= Repaint;
			UidEditorIndex.Rebuilt                 -= Repaint;
		}

		private void Refresh()
		{
			UidEditorIndex.Invalidate();
			UidIdentityAuthority.DetectCollisions();
			_registries = UidEditorUtility.LoadAllRegistries();
			Repaint();
		}

		private void OnGUI()
		{
			using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
			{
				if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(70))) Refresh();
				GUILayout.FlexibleSpace();
				GUILayout.Label($"{UidIdentityAuthority.UnresolvedCollisions.Count} collision(s)", EditorStyles.miniLabel);
			}

			if (UidIdentityAuthority.UnresolvedCollisions.Count == 0)
			{
				EditorGUILayout.HelpBox("No identity collisions. Every Uid in the project is owned by exactly one asset.", MessageType.Info);
				return;
			}

			EditorGUILayout.HelpBox(
				"Two or more assets carry the same identity. Saves, servers, and registries cannot tell them apart. " +
				"Choose which asset keeps the identity; the others receive fresh identities. " +
				"If the losers are already referenced by saved data, add a redirect afterwards in the Redirect Table.",
				MessageType.Warning);

			_registries ??= UidEditorUtility.LoadAllRegistries();

			_scroll = EditorGUILayout.BeginScrollView(_scroll);
			foreach (Uid id in new List<Uid>(UidIdentityAuthority.UnresolvedCollisions))
			{
				DrawCollision(id);
			}
			EditorGUILayout.EndScrollView();
		}

		private void DrawCollision(Uid id)
		{
			if (!UidEditorIndex.TryGetOwners(id, out var owners) || owners.Count < 2) return;

			using (new EditorGUILayout.VerticalScope("box"))
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.SelectableLabel(id.ToString(), EditorStyles.boldLabel, GUILayout.Height(18));
					if (GUILayout.Button("Copy", GUILayout.Width(50))) UidEditorUtility.CopyToClipboard(id.ToString());
				}

				foreach (var owner in owners)
				{
					var asset = AssetDatabase.LoadAssetAtPath<UID>(owner.AssetPath);
					if (asset == null) continue;

					using (new EditorGUILayout.HorizontalScope())
					{
						EditorGUILayout.ObjectField(asset, typeof(UID), false, GUILayout.Width(220));

						var registry = UidEditorUtility.FindRegistryFor(asset, _registries);
						string info = $"{asset.Provenance}" + (registry != null ? $" · {registry.name}" : " · no registry");
						GUILayout.Label(info, EditorStyles.miniLabel);

						GUILayout.FlexibleSpace();

						using (new EditorGUI.DisabledScope(!CanBeSurvivor(asset, owners)))
						{
							if (GUILayout.Button("Keep", GUILayout.Width(60)))
							{
								if (EditorUtility.DisplayDialog(
									    "Resolve identity collision",
									    $"'{asset.name}' keeps {id.ToShortString()}. Every other owner gets a fresh identity.\n\nContinue?",
									    "Resolve", "Cancel"))
								{
									UidIdentityAuthority.ResolveCollision(id, asset);
									GUIUtility.ExitGUI();
								}
							}
						}
					}
				}

				if (AnyOwnerImported(owners))
				{
					EditorGUILayout.HelpBox("An owner has an Imported identity. Imported identities are never re-minted here; only that asset can survive, or fix the source system first.", MessageType.Error);
				}
			}
		}

		private static bool CanBeSurvivor(UID candidate, IReadOnlyList<UidEditorIndex.Entry> owners)
		{
			foreach (var owner in owners)
			{
				var asset = AssetDatabase.LoadAssetAtPath<UID>(owner.AssetPath);
				if (asset == null || asset == candidate) continue;
				if (asset.Provenance == UidProvenance.Imported) return false;
			}

			return true;
		}

		private static bool AnyOwnerImported(IReadOnlyList<UidEditorIndex.Entry> owners)
		{
			foreach (var owner in owners)
			{
				var asset = AssetDatabase.LoadAssetAtPath<UID>(owner.AssetPath);
				if (asset != null && asset.Provenance == UidProvenance.Imported) return true;
			}

			return false;
		}
	}
}
