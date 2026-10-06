using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AK.Systems.Editor
{
	/// <summary>
	/// The view repository's inspector: the list, and under it a clean-up that reports the
	/// entries the UI system can't use and removes them with one button. What counts is up to
	/// <see cref="UIViewRepositoryCleanup"/>.
	/// </summary>
	[CustomEditor(typeof(UIViewRepository), true)]
	public sealed class UIViewRepositoryEditor : UnityEditor.Editor
	{
		private const string DialogTitle = "Clean Up View Repository";
		private const string KeptNote =
			"Only the very same view listed twice is a duplicate. Prefab variants and copies are other prefabs, so they stay.";

		private readonly List<UIViewRepositoryCleanup.Finding> _findings = new();
		private int _scannedDirtyCount = -1;
		private bool _rescan = true;
		private bool _detailsOpen;

		public override void OnInspectorGUI()
		{
			_rescan |= serializedObject.UpdateIfRequiredOrScript();
			DrawDefaultInspector();

			// Scanned on Layout only, so the controls below match between Layout and Repaint.
			int dirtyCount = EditorUtility.GetDirtyCount(target);
			if (Event.current.type == EventType.Layout && (_rescan || dirtyCount != _scannedDirtyCount))
			{
				_rescan = false;
				_scannedDirtyCount = dirtyCount;
				UIViewRepositoryCleanup.Find(serializedObject, _findings);
			}

			EditorGUILayout.Space();
			DrawCleanUp();
		}

		private void DrawCleanUp()
		{
			EditorGUILayout.LabelField("Clean Up", EditorStyles.boldLabel);

			if (_findings.Count == 0)
			{
				EditorGUILayout.HelpBox("Every entry is a view, listed once.", MessageType.Info);
				return;
			}

			EditorGUILayout.HelpBox(Summary(_findings) + "\n" + KeptNote, MessageType.Warning);

			_detailsOpen = EditorGUILayout.Foldout(_detailsOpen, "Entries to remove", true);
			if (_detailsOpen)
			{
				SerializedProperty views = serializedObject.FindProperty(UIViewRepositoryCleanup.ViewsProperty);
				using (new EditorGUI.IndentLevelScope())
				{
					foreach (UIViewRepositoryCleanup.Finding finding in _findings)
					{
						EditorGUILayout.LabelField($"Element {finding.Index}", Describe(finding, views));
					}
				}
			}

			if (GUILayout.Button(_findings.Count == 1 ? "Remove 1 Entry" : $"Remove {_findings.Count} Entries"))
			{
				RemoveAfterConfirming();

				// The list may have changed under this GUI pass.
				GUIUtility.ExitGUI();
			}
		}

		private void RemoveAfterConfirming()
		{
			UIViewRepositoryCleanup.Find(serializedObject, _findings);
			if (_findings.Count == 0)
			{
				return;
			}

			string summary = Summary(_findings);
			if (!EditorUtility.DisplayDialog(
				    DialogTitle,
				    $"Remove {EntryCount(_findings.Count)} from {target.name}?\n\n{summary}\n\n{KeptNote}",
				    "Remove",
				    "Cancel"))
			{
				return;
			}

			// Described before they go, while the indices still match the list.
			SerializedProperty views = serializedObject.FindProperty(UIViewRepositoryCleanup.ViewsProperty);
			var log = new StringBuilder();
			foreach (UIViewRepositoryCleanup.Finding finding in _findings)
			{
				log.Append("\nElement ").Append(finding.Index).Append(": ").Append(Describe(finding, views));
			}

			Undo.IncrementCurrentGroup();
			Undo.SetCurrentGroupName(DialogTitle);

			var removed = new List<UIViewRepositoryCleanup.Finding>(_findings.Count);
			UIViewRepositoryCleanup.Remove(serializedObject, removed);
			_rescan = true;

			Debug.Log($"[UIViewRepository] Removed {EntryCount(removed.Count)} from {target.name}. {summary}{log}", target);
		}

		private static string Summary(List<UIViewRepositoryCleanup.Finding> findings)
		{
			int missing = 0, empty = 0, duplicates = 0;
			foreach (UIViewRepositoryCleanup.Finding finding in findings)
			{
				switch (finding.Problem)
				{
					case UIViewRepositoryCleanup.Problem.Missing:
						missing++;
						break;
					case UIViewRepositoryCleanup.Problem.Empty:
						empty++;
						break;
					default:
						duplicates++;
						break;
				}
			}

			var text = new StringBuilder();
			if (missing > 0)
			{
				text.Append(missing == 1 ? "1 entry points at a view that no longer exists. " : $"{missing} entries point at views that no longer exist. ");
			}

			if (empty > 0)
			{
				text.Append(empty == 1 ? "1 entry is empty. " : $"{empty} entries are empty. ");
			}

			if (duplicates > 0)
			{
				text.Append(duplicates == 1 ? "1 entry is a duplicate of an earlier one. " : $"{duplicates} entries are duplicates of earlier ones. ");
			}

			return text.ToString().TrimEnd();
		}

		private static string Describe(UIViewRepositoryCleanup.Finding finding, SerializedProperty views)
		{
			switch (finding.Problem)
			{
				case UIViewRepositoryCleanup.Problem.Missing:
					return "the view no longer exists";
				case UIViewRepositoryCleanup.Problem.Empty:
					return "empty";
				default:
					// Out of range only if the list changed since the scan; the next Layout rescans.
					Object view = finding.Index < views.arraySize ? views.GetArrayElementAtIndex(finding.Index).objectReferenceValue : null;
					return view != null
						? $"{view.name}, a duplicate of Element {finding.DuplicateOf}"
						: $"a duplicate of Element {finding.DuplicateOf}";
			}
		}

		private static string EntryCount(int count) => count == 1 ? "1 entry" : $"{count} entries";
	}
}
