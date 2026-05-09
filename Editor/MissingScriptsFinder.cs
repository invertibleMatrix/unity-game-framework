using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class MissingScriptsFinder : EditorWindow
{
	[MenuItem("Tools/Missing Scripts/Find Missing Scripts")]
	public static void FindMissing()
	{
		// Use a HashSet to avoid selecting the same object twice 
		// (in case you selected both a parent and a child)
		HashSet<GameObject> brokenObjects = new HashSet<GameObject>();

		foreach (GameObject selectedGo in Selection.gameObjects)
		{
			// Get components on the object and ALL its children
			// The 'true' argument ensures it finds components on inactive objects too
			Component[] allComponents = selectedGo.GetComponentsInChildren<Component>(true);

			foreach (Component c in allComponents)
			{
				if (c == null)
				{
					// Find the GameObject that owns this null component
					// We use the SerializedObject trick because 'c.gameObject' 
					// fails if the component itself is null.
                    
					// Actually, for missing scripts, GetComponentsInChildren returns 
					// the objects in a way we can track. Let's use the transform approach:
					Transform[] allTransforms = selectedGo.GetComponentsInChildren<Transform>(true);
					foreach (Transform t in allTransforms)
					{
						if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
						{
							brokenObjects.Add(t.gameObject);
						}
					}
					break; 
				}
			}
		}

		// Apply the new selection (deselects everything else)
		GameObject[] finalSelection = new GameObject[brokenObjects.Count];
		brokenObjects.CopyTo(finalSelection);
		Selection.objects = finalSelection;
	}
	
	[MenuItem("Tools/Missing Scripts/Remove Missing From Selection")]
	public static void RemoveMissingFromSelection()
	{
		GameObject[] currentSelection = Selection.gameObjects;
		int totalRemoved = 0;

		foreach (GameObject go in currentSelection)
		{
			// Record the state for Undo support (Ctrl+Z)
			Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
            
			int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
			if (count > 0)
			{
				totalRemoved += count;
				// Marks the scene/prefab as "dirty" so Unity knows you need to save
				EditorUtility.SetDirty(go);
			}
		}

		Debug.Log($"Successfully removed {totalRemoved} missing script slots from {currentSelection.Length} objects.");
	}
}