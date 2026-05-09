using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// This script ensures that when you press the Play button in the Unity Editor,
/// it starts from the first scene in Build Settings (index 0) when enabled via the menu option.
/// This is essential for projects that use an initialization scene to set up core managers and services.
/// </summary>
[InitializeOnLoad]
public class EditorSceneLoader
{
    // EditorPrefs key for storing the toggle state
    private const string AlwaysStartsFromSceneKey = "EditorSceneLoader.AlwaysStartsFromScene";
    private const string PreviousSceneKey = "EditorSceneLoader.PreviousScene";

    static EditorSceneLoader()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    // Menu item to toggle the "Always Starts From Scene" option
    [MenuItem("Tools/AK/AlwaysStartsFromScene 0", false, 0)]
    private static void ToggleAlwaysStartsFromScene()
    {
        bool currentState = GetAlwaysStartsFromScene();
        EditorPrefs.SetBool(AlwaysStartsFromSceneKey, !currentState);
    }

    // Validates the menu item and shows a checkmark when enabled
    [MenuItem("Tools/AK/AlwaysStartsFromScene 0", true, 0)]
    private static bool ValidateToggleAlwaysStartsFromScene()
    {
        Menu.SetChecked("Tools/AK/AlwaysStartsFromScene 0", GetAlwaysStartsFromScene());
        return true;
    }

    // Helper method to get the toggle state from EditorPrefs
    private static bool GetAlwaysStartsFromScene()
    {
        return EditorPrefs.GetBool(AlwaysStartsFromSceneKey, false); // Default to false
    }

    // Get the scene path at index 0 from Build Settings
    private static string GetBootstrapScenePath()
    {
        if (EditorBuildSettings.scenes.Length > 0 && !string.IsNullOrEmpty(EditorBuildSettings.scenes[0].path))
        {
            return EditorBuildSettings.scenes[0].path;
        }
        return null;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // Only proceed if the "Always Starts From Scene" option is enabled
        if (!GetAlwaysStartsFromScene())
        {
            return;
        }

        // Get the bootstrap scene path (index 0 from Build Settings)
        string bootstrapScenePath = GetBootstrapScenePath();

        // If no valid bootstrap scene is found, do nothing
        if (string.IsNullOrEmpty(bootstrapScenePath))
        {
            return;
        }

        // This logic triggers when you are about to enter Play Mode from the Editor.
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // Store the path of the scene you are currently in, so we can return to it later.
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            string currentScenePath = SceneManager.GetActiveScene().path;
            EditorPrefs.SetString(PreviousSceneKey, currentScenePath);

            // If the scene we are about to play is not already the bootstrap scene,
            // then load the bootstrap scene.
            if (currentScenePath != bootstrapScenePath)
            {
                EditorSceneManager.OpenScene(bootstrapScenePath);
            }
        }
        // This logic triggers when you exit Play Mode and are returning to the Editor.
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            // Load the scene you were in before you pressed Play.
            string previousScenePath = EditorPrefs.GetString(PreviousSceneKey, bootstrapScenePath);
            if (!string.IsNullOrEmpty(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath);
            }
        }
    }
}