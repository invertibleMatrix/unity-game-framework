using AK.Tutorials;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialProvider))]
public class TutorialProviderEditor : Editor
{
	private int _fromStep = -1; // -1 = all steps

	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		var provider = (TutorialProvider)target;
		if (provider == null || provider.Steps == null || provider.Steps.Count == 0)
		{
			return;
		}

		GUILayout.Space(10);
		EditorGUILayout.LabelField("Debug — Force Tutorial", EditorStyles.boldLabel);

		if (!Application.isPlaying)
		{
			EditorGUILayout.HelpBox(
				"Enter Play Mode to force tutorials — the buttons need the live fact store and UI system.",
				MessageType.Info);
			return;
		}

		if (!TutorialDebugBridge.IsReady)
		{
			EditorGUILayout.HelpBox(
				"TutorialDebugBridge is not configured yet — the host must call TutorialDebugBridge.Configure at binding time.",
				MessageType.Warning);
			return;
		}

		DrawStatus(provider);
		GUILayout.Space(4);
		DrawActions(provider);
		DrawStepBreakdown(provider);
	}

	private void DrawStatus(TutorialProvider provider)
	{
		string gate = provider.EnabledGate != null
			? (provider.EnabledGate.Value ? "on" : "KILLED (remote)")
			: "none";
		EditorGUILayout.LabelField("Progress", $"{TutorialDebugBridge.Count(provider.ProgressFact)} / {provider.Steps.Count} steps");
		EditorGUILayout.LabelField("Due now", provider.HasDueSteps ? "yes" : "no");
		EditorGUILayout.LabelField("Complete", provider.IsComplete ? "yes" : "no");
		EditorGUILayout.LabelField("Enabled gate", gate);
	}

	private void DrawActions(TutorialProvider provider)
	{
		_fromStep = Mathf.Clamp(_fromStep, -1, provider.Steps.Count - 1);

		var options = new string[provider.Steps.Count + 1];
		options[0] = "All steps";
		for (int i = 0; i < provider.Steps.Count; i++)
		{
			options[i + 1] = $"Step {i}{(provider.Steps[i] != null ? $" — {provider.Steps[i].name}" : "")}";
		}

		_fromStep = EditorGUILayout.Popup("Present from", _fromStep + 1, options) - 1;

		using (new EditorGUILayout.HorizontalScope())
		{
			if (GUILayout.Button(new GUIContent("Present (direct)",
					"Sets progress + condition facts and runs the provider's RunDueAsync immediately, bypassing any host presentation chain.")))
			{
				TutorialDebugBridge.PresentDirectly(provider, _fromStep, _fromStep < 0);
			}

			using (new EditorGUI.DisabledScope(!TutorialDebugBridge.CanPresentViaChain))
			{
				if (GUILayout.Button(new GUIContent("Present (chain)",
						"Sets progress + condition facts and kicks the host's presentation chain via ChainKick. Disabled when the host didn't set a hook.")))
				{
					TutorialDebugBridge.PresentViaChain(provider, _fromStep, _fromStep < 0);
				}
			}
		}

		using (new EditorGUILayout.HorizontalScope())
		{
			if (GUILayout.Button("Reset progress"))
			{
				TutorialDebugBridge.ResetProgress(provider);
			}

			if (GUILayout.Button("Reset ALL facts"))
			{
				TutorialDebugBridge.ResetAllFacts();
			}
		}
	}

	private void DrawStepBreakdown(TutorialProvider provider)
	{
		GUILayout.Space(4);
		for (int i = 0; i < provider.Steps.Count; i++)
		{
			var step = provider.Steps[i];
			EditorGUILayout.LabelField(
				$"Step {i}{(step != null ? $" — {step.name}" : " (null)")}",
				EditorStyles.miniBoldLabel);

			if (step?.Conditions == null)
			{
				continue;
			}

			foreach (var condition in step.Conditions)
			{
				if (condition == null || condition.Type == null)
				{
					EditorGUILayout.LabelField("    (unset condition — fails closed)");
					continue;
				}

				int have = TutorialDebugBridge.Count(condition.Type);
				bool met = have >= condition.MinCount;
				EditorGUILayout.LabelField($"    {condition.Type.name}: {have}/{condition.MinCount} {(met ? "✓" : "✗")}");
			}
		}
	}
}
