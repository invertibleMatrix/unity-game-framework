using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Paste an identity, see what it is. Accepts 32-hex, dashed, or braced forms, follows
	/// redirects, and shows the owning asset (or every owner when there is a collision). The
	/// tool you reach for when a save file, server log, or analytics row hands you a hex string.
	/// </summary>
	public sealed class UidInspectorWindow : EditorWindow
	{
		private string _input = string.Empty;
		private Uid    _parsed;
		private bool   _valid;
		private Vector2 _scroll;
		private List<UidRedirectTable> _redirects;

		[MenuItem(UidEditorUtility.MenuRoot + "Inspect Identity…", priority = 21)]
		public static void Open()
		{
			var window = GetWindow<UidInspectorWindow>("UID Inspector");
			window.minSize = new Vector2(480, 200);
		}

		private void OnEnable()
		{
			_redirects = UidEditorUtility.LoadAllRedirectTables();
		}

		private void OnGUI()
		{
			EditorGUILayout.Space(4);

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUI.BeginChangeCheck();
				_input = EditorGUILayout.TextField("Identity", _input);
				if (EditorGUI.EndChangeCheck())
				{
					_valid = Uid.TryParse(_input, out _parsed) && _parsed.IsSet;
				}

				if (GUILayout.Button("Paste", GUILayout.Width(50)))
				{
					_input = EditorGUIUtility.systemCopyBuffer?.Trim() ?? string.Empty;
					_valid = Uid.TryParse(_input, out _parsed) && _parsed.IsSet;
				}
			}

			if (string.IsNullOrWhiteSpace(_input))
			{
				EditorGUILayout.HelpBox("Paste a Uid (32 hex, dashed, or braced). Drag an identity asset here to read its Uid.", MessageType.None);
				HandleDrop();
				return;
			}

			if (!_valid)
			{
				EditorGUILayout.HelpBox("Not a valid Uid.", MessageType.Error);
				return;
			}

			_scroll = EditorGUILayout.BeginScrollView(_scroll);
			DrawForms();
			DrawOwners(_parsed, "Direct owner");
			DrawRedirects();
			EditorGUILayout.EndScrollView();
		}

		private void DrawForms()
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				Row("Canonical", _parsed.ToString());
				Row("Dashed", _parsed.ToDashedString());
				Row("Short", _parsed.ToShortString());
			}
		}

		private static void Row(string label, string value)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.PrefixLabel(label);
				EditorGUILayout.SelectableLabel(value, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
				if (GUILayout.Button("Copy", GUILayout.Width(48))) UidEditorUtility.CopyToClipboard(value);
			}
		}

		private static void DrawOwners(Uid id, string title)
		{
			EditorGUILayout.Space(4);
			EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

			if (!UidEditorIndex.TryGetOwners(id, out var owners) || owners.Count == 0)
			{
				EditorGUILayout.HelpBox("No asset in this project owns this identity.", MessageType.Warning);
				return;
			}

			if (owners.Count > 1)
			{
				EditorGUILayout.HelpBox($"{owners.Count} assets own this identity — collision.", MessageType.Error);
			}

			foreach (var owner in owners)
			{
				var asset = AssetDatabase.LoadAssetAtPath<UID>(owner.AssetPath);
				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.ObjectField(asset, typeof(UID), false);
					GUILayout.Label(asset != null ? $"{asset.GetType().Name} · {asset.Provenance}" : owner.TypeName, EditorStyles.miniLabel);
				}
			}
		}

		private void DrawRedirects()
		{
			_redirects ??= UidEditorUtility.LoadAllRedirectTables();

			foreach (var table in _redirects)
			{
				if (table == null || !table.TryFollow(_parsed, out Uid target)) continue;

				EditorGUILayout.Space(4);
				EditorGUILayout.HelpBox($"'{table.name}' redirects this identity to {target.ToShortString()}.", MessageType.Info);
				DrawOwners(target, "Redirect target owner");
			}
		}

		private void HandleDrop()
		{
			Rect drop = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
			GUI.Box(drop, "Drop identity asset", EditorStyles.helpBox);

			Event evt = Event.current;
			if (!drop.Contains(evt.mousePosition)) return;

			if (evt.type == EventType.DragUpdated)
			{
				DragAndDrop.visualMode = DragAndDropVisualMode.Link;
				evt.Use();
			}
			else if (evt.type == EventType.DragPerform)
			{
				DragAndDrop.AcceptDrag();
				foreach (var obj in DragAndDrop.objectReferences)
				{
					if (obj is UID uid && uid.HasIdentity)
					{
						_input  = uid.Id.ToString();
						_parsed = uid.Id;
						_valid  = true;
						break;
					}
				}

				evt.Use();
			}
		}
	}
}
