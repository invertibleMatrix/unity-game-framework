using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Inspector for registry assets: element type, tracked count, health (nulls, missing
	/// identities, duplicates, untracked project assets), and the maintenance actions.
	/// The list itself is read-only here — the auto-tracker owns membership.
	/// </summary>
	[CustomEditor(typeof(UidRegistryAssetBase), true)]
	public sealed class UidRegistryAssetEditor : UnityEditor.Editor
	{
		private bool _showEntries;
		private Vector2 _scroll;

		private int _nulls;
		private int _noIdentity;
		private int _duplicates;
		private int _untracked;
		private int _analyzedVersion = -1;

		public override void OnInspectorGUI()
		{
			var registry = (UidRegistryAssetBase)target;

			Analyze(registry);

			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				EditorGUILayout.LabelField("Element type", registry.ElementType.Name);
				EditorGUILayout.LabelField("Tracked", registry.ObjectCount.ToString());

				if (_nulls > 0)      EditorGUILayout.HelpBox($"{_nulls} deleted asset(s) still listed.", MessageType.Warning);
				if (_noIdentity > 0) EditorGUILayout.HelpBox($"{_noIdentity} tracked asset(s) have no identity and are skipped at runtime.", MessageType.Error);
				if (_duplicates > 0) EditorGUILayout.HelpBox($"{_duplicates} tracked asset(s) share an identity with another entry and are skipped at runtime.", MessageType.Error);
				if (_untracked > 0)  EditorGUILayout.HelpBox($"{_untracked} {registry.ElementType.Name} asset(s) in the project are not tracked here.", MessageType.Warning);

				if (_nulls + _noIdentity + _duplicates + _untracked == 0)
				{
					EditorGUILayout.HelpBox("Healthy. Every tracked asset has a unique identity and every project asset of this type is tracked.", MessageType.Info);
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Refresh From Project"))
				{
					registry.Editor_RefreshFromProject();
					AssetDatabase.SaveAssetIfDirty(registry);
					_analyzedVersion = -1;
				}

				using (new EditorGUI.DisabledScope(_nulls == 0))
				{
					if (GUILayout.Button("Remove Deleted"))
					{
						registry.Editor_RemoveNullEntries();
						AssetDatabase.SaveAssetIfDirty(registry);
						_analyzedVersion = -1;
					}
				}

				if (GUILayout.Button("Audit Project"))
				{
					UidAuditMenu.RunAudit();
				}
			}

			EditorGUILayout.Space(4);
			_showEntries = EditorGUILayout.Foldout(_showEntries, $"Entries ({registry.ObjectCount})", true);
			if (_showEntries)
			{
				DrawEntries(registry);
			}

			EditorGUILayout.Space(6);
			DrawPropertiesExcluding(serializedObject, "m_Script", "_registry");
			serializedObject.ApplyModifiedProperties();
		}

		private void Analyze(UidRegistryAssetBase registry)
		{
			if (_analyzedVersion == UidEditorIndex.Version && !serializedObject.hasModifiedProperties) return;

			_nulls = _noIdentity = _duplicates = _untracked = 0;
			var seen = new HashSet<Uid>();
			var trackedGuids = new HashSet<string>();

			foreach (UID obj in registry.GetTrackedObjects())
			{
				if (obj == null) { _nulls++; continue; }
				if (!obj.HasIdentity) { _noIdentity++; continue; }
				if (!seen.Add(obj.Id)) _duplicates++;

				trackedGuids.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)));
			}

			foreach (string guid in AssetDatabase.FindAssets($"t:{registry.ElementType.Name}"))
			{
				if (!trackedGuids.Contains(guid)) _untracked++;
			}

			_analyzedVersion = UidEditorIndex.Version;
		}

		private void DrawEntries(UidRegistryAssetBase registry)
		{
			_scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(320));
			foreach (UID obj in registry.GetTrackedObjects())
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					if (obj == null)
					{
						EditorGUILayout.LabelField("<deleted>", EditorStyles.miniLabel);
						continue;
					}

					EditorGUILayout.ObjectField(obj, registry.ElementType, false);
					GUILayout.Label(obj.HasIdentity ? obj.Id.ToShortString() : "no identity", EditorStyles.miniLabel, GUILayout.Width(80));
				}
			}
			EditorGUILayout.EndScrollView();
		}
	}
}
