using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Inspector for every UIDRegistryAsset subclass: default inspector plus maintenance
	/// buttons. Registered once against the non-generic base with editorForChildClasses —
	/// Unity's context-menu discovery never surfaces methods from generic base classes,
	/// so this editor replaces the Button/ContextMenu entries that silently did
	/// nothing on TypedUIDRegistryAsset subclasses.
	/// </summary>
	[CustomEditor(typeof(UIDRegistryAsset), true)]
	public class UIDRegistryAssetEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			var registry = (UIDRegistryAsset)target;

			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"Maintenance — {registry.ObjectCount} objects tracked", EditorStyles.boldLabel);

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Refresh All"))
				{
					foreach (var t in targets)
					{
						Undo.RecordObject(t, "Refresh Registry Objects");
						((UIDRegistryAsset)t).RefreshAllObjects();
					}
				}

				if (GUILayout.Button("Validate"))
				{
					foreach (var t in targets)
					{
						((UIDRegistryAsset)t).ValidateObjects();
					}
				}

				using (new EditorGUI.DisabledScope(registry.ObjectCount == 0))
				{
					if (GUILayout.Button("Regenerate UIDs"))
					{
						bool confirmed = EditorUtility.DisplayDialog("Regenerate UIDs",
							"Missing UIDs and duplicate COPIES receive fresh GUIDs; the first member of each duplicate group keeps its identity, so existing links and counts stay valid. Continue?",
							"Regenerate", "Cancel");

						if (confirmed)
						{
							foreach (var t in targets)
							{
								((UIDRegistryAsset)t).RegenerateUIDs();
							}
						}
					}
				}
			}
		}
	}
}
