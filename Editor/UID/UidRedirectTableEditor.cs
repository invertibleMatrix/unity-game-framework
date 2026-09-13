using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Inspector for the redirect table. Each entry shows From (hex, with its current owner
	/// if one still exists — usually none, which is the point) and To (an asset picker), plus
	/// the reason and date. Validation flags targets that no longer exist, self-redirects,
	/// and chains that exceed the hop limit.
	/// </summary>
	[CustomEditor(typeof(UidRedirectTable))]
	public sealed class UidRedirectTableEditor : UnityEditor.Editor
	{
		private string _newFrom = string.Empty;
		private UID    _newTo;
		private string _newReason = string.Empty;

		public override void OnInspectorGUI()
		{
			var table = (UidRedirectTable)target;
			serializedObject.Update();

			EditorGUILayout.HelpBox(
				"A redirect says \"the old identity now means this asset\". Add one when content is replaced and saved data or servers " +
				"still carry the old identity. Never redirect to hide a collision — resolve the collision first.",
				MessageType.None);

			DrawEntries(table);
			EditorGUILayout.Space(8);
			DrawAdd(table);

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawEntries(UidRedirectTable table)
		{
			var entries = serializedObject.FindProperty("_entries");
			EditorGUILayout.LabelField($"Entries ({entries.arraySize})", EditorStyles.boldLabel);

			for (int i = 0; i < entries.arraySize; i++)
			{
				var entry = entries.GetArrayElementAtIndex(i);
				var from  = entry.FindPropertyRelative("From");
				var to    = entry.FindPropertyRelative("To");

				using (new EditorGUILayout.VerticalScope("box"))
				{
					using (new EditorGUILayout.HorizontalScope())
					{
						EditorGUILayout.LabelField("From", GUILayout.Width(40));
						EditorGUILayout.PropertyField(from, GUIContent.none);
						if (GUILayout.Button("✕", GUILayout.Width(22)))
						{
							entries.DeleteArrayElementAtIndex(i);
							break;
						}
					}

					using (new EditorGUILayout.HorizontalScope())
					{
						EditorGUILayout.LabelField("To", GUILayout.Width(40));
						EditorGUILayout.PropertyField(to, GUIContent.none);
					}

					EditorGUILayout.PropertyField(entry.FindPropertyRelative("Reason"));
					using (new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.PropertyField(entry.FindPropertyRelative("Date"));
					}

					DrawEntryValidation(table, i);
				}
			}
		}

		private static void DrawEntryValidation(UidRedirectTable table, int index)
		{
			if (index >= table.Entries.Count) return;
			var e = table.Entries[index];

			if (e.From.IsNone || e.To.IsNone)
			{
				EditorGUILayout.HelpBox("Both From and To must be set; this entry is ignored.", MessageType.Warning);
				return;
			}

			if (e.From == e.To)
			{
				EditorGUILayout.HelpBox("Self-redirect; ignored.", MessageType.Warning);
				return;
			}

			if (UidEditorIndex.IsOwned(e.From))
			{
				EditorGUILayout.HelpBox("From is still owned by a live asset. Redirecting a live identity makes that asset unreachable by identity.", MessageType.Warning);
			}

			Uid final = table.Follow(e.From);
			if (!UidEditorIndex.IsOwned(final))
			{
				EditorGUILayout.HelpBox($"Chain ends at {final.ToShortString()} which no asset owns. Data redirected here will still be orphaned.", MessageType.Error);
			}
		}

		private void DrawAdd(UidRedirectTable table)
		{
			EditorGUILayout.LabelField("Add redirect", EditorStyles.boldLabel);
			using (new EditorGUILayout.VerticalScope("box"))
			{
				_newFrom   = EditorGUILayout.TextField("From (hex)", _newFrom);
				_newTo     = (UID)EditorGUILayout.ObjectField("To", _newTo, typeof(UID), false);
				_newReason = EditorGUILayout.TextField("Reason", _newReason);

				bool fromOk = Uid.TryParse(_newFrom, out Uid from) && from.IsSet;
				bool toOk   = _newTo != null && _newTo.HasIdentity;

				using (new EditorGUI.DisabledScope(!(fromOk && toOk)))
				{
					if (GUILayout.Button("Add"))
					{
						Undo.RecordObject(table, "Add redirect");
						table.Editor_Add(from, _newTo.Id, _newReason);
						UidEditorUtility.SaveAsset(table);
						_newFrom   = string.Empty;
						_newTo     = null;
						_newReason = string.Empty;
						GUIUtility.ExitGUI();
					}
				}
			}
		}
	}
}
