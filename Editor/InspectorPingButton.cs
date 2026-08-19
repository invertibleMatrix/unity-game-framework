using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class InspectorPingButton
{
	private const int MaxHistory = 50;

	private static readonly List<Object> _back = new();
	private static readonly List<Object> _forward = new();
	private static Object _lastActive;

	static InspectorPingButton()
	{
		Editor.finishedDefaultHeaderGUI += DrawHeaderButtons;
		Selection.selectionChanged += OnSelectionChanged;
		_lastActive = Selection.activeObject;
	}

	private static void OnSelectionChanged()
	{
		Object current = Selection.activeObject;
		if (current == _lastActive)
		{
			// Also fires for our own back/forward navigation — must not record it,
			// or navigating would wipe the opposite stack.
			return;
		}

		PushValid(_back, _lastActive);
		_forward.Clear();
		_lastActive = current;
	}

	private static void DrawHeaderButtons(Editor editor)
	{
		if (editor.target == null) return;

		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();

		DrawNavButton(_back, _forward, "d_Profiler.PrevFrame", "<", "Back to previous selection");
		DrawNavButton(_forward, _back, "d_Profiler.NextFrame", ">", "Forward to next selection");

		var pingIcon = EditorGUIUtility.IconContent("d_ViewToolOrbit");
		pingIcon.tooltip = "Ping this object in the Project/Hierarchy";
		if (GUILayout.Button(pingIcon, GUILayout.Height(20), GUILayout.Width(30)))
		{
			EditorGUIUtility.PingObject(editor.target);
		}

		GUILayout.EndHorizontal();
	}

	private static void DrawNavButton(List<Object> popStack, List<Object> pushStack,
		string iconName, string fallbackText, string tooltip)
	{
		GUIContent icon = EditorGUIUtility.IconContent(iconName);
		if (icon.image == null)
		{
			// Icon names shift between Unity versions — never leave a blank button.
			icon = new GUIContent(fallbackText);
		}
		icon.tooltip = tooltip;

		bool wasEnabled = GUI.enabled;
		GUI.enabled = wasEnabled && TopValid(popStack) != null;
		if (GUILayout.Button(icon, GUILayout.Height(20), GUILayout.Width(30)))
		{
			Navigate(popStack, pushStack);
		}
		GUI.enabled = wasEnabled;
	}

	private static void Navigate(List<Object> popStack, List<Object> pushStack)
	{
		Object target = PopValid(popStack);
		if (target == null)
		{
			return;
		}

		PushValid(pushStack, _lastActive);
		// Sync before assigning so OnSelectionChanged treats this as our own move.
		_lastActive = target;
		Selection.activeObject = target;
	}

	private static void PushValid(List<Object> stack, Object obj)
	{
		if (obj == null) return; // null or destroyed

		stack.Add(obj);
		if (stack.Count > MaxHistory)
		{
			stack.RemoveAt(0);
		}
	}

	private static Object PopValid(List<Object> stack)
	{
		while (stack.Count > 0)
		{
			int last = stack.Count - 1;
			Object obj = stack[last];
			stack.RemoveAt(last);
			if (obj != null) return obj; // skip destroyed entries
		}

		return null;
	}

	private static Object TopValid(List<Object> stack)
	{
		for (int i = stack.Count - 1; i >= 0; i--)
		{
			if (stack[i] != null) return stack[i];
		}

		return null;
	}
}
