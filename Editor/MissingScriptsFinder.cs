using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AK.Editor
{
	/// <summary>
	/// Finds GameObjects whose components lost their scripts, and removes those components.
	/// Find selects every GameObject with one under the selection; Remove clears the selected
	/// GameObjects, with undo.
	/// </summary>
	public static class MissingScriptsFinder
	{
		private const string MenuRoot = "Tools/UGFW/Missing Scripts/";

		[MenuItem(MenuRoot + "Find Missing Scripts")]
		public static void FindMissing()
		{
			// A set, so a GameObject selected along with its parent is listed once.
			var brokenObjects = new HashSet<GameObject>();

			foreach (GameObject selectedGo in Selection.gameObjects)
			{
				// Inactive GameObjects too.
				foreach (Transform t in selectedGo.GetComponentsInChildren<Transform>(true))
				{
					if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
					{
						brokenObjects.Add(t.gameObject);
					}
				}
			}

			var finalSelection = new GameObject[brokenObjects.Count];
			brokenObjects.CopyTo(finalSelection);
			Selection.objects = finalSelection;
		}

		[MenuItem(MenuRoot + "Remove Missing From Selection")]
		public static void RemoveMissingFromSelection()
		{
			GameObject[] currentSelection = Selection.gameObjects;
			int totalRemoved = 0;

			foreach (GameObject go in currentSelection)
			{
				// Undo (Ctrl+Z) brings the slots back.
				Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");

				int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
				if (count > 0)
				{
					totalRemoved += count;
					// Marks the scene or prefab dirty, so it gets saved.
					EditorUtility.SetDirty(go);
				}
			}

			Debug.Log($"Successfully removed {totalRemoved} missing script slots from {currentSelection.Length} objects.");
		}
	}
}
