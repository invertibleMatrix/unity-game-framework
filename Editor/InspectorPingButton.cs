using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class InspectorPingButton
{
	// Static constructor is called automatically when Unity loads or compiles
	static InspectorPingButton()
	{
		// Subscribe to the event that draws after the standard header
		Editor.finishedDefaultHeaderGUI += DrawPingButton;
	}

	private static void DrawPingButton(Editor editor)
	{
		// We only want this on the actual Inspector, not usually in other editor windows
		// that might reuse Editor headers (optional check)
		if (editor.target == null) return;

		// Start a horizontal row to place our button nicely
		GUILayout.BeginHorizontal();
        
		// FlexibleSpace pushes elements to the right or fills gaps
		GUILayout.FlexibleSpace();

		// Draw the button. 
		// We use a built-in Unity Icon for a cleaner look ("d_ViewToolOrbit" looks like an eye/target)
		// You can simply use new GUIContent("Ping") for text.
		var icon = EditorGUIUtility.IconContent("d_ViewToolOrbit");
		icon.tooltip = "Ping this object in the Project/Hierarchy";

		// Create a small button style
		if (GUILayout.Button(icon, GUILayout.Height(20), GUILayout.Width(30)))
		{
			// The Magic: This highlights the object in the Project or Hierarchy
			EditorGUIUtility.PingObject(editor.target);
            
			// Optional: Also select it (if you want to force selection change)
			// Selection.activeObject = editor.target; 
		}

		GUILayout.EndHorizontal();
	}
}