using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AK.Editor
{
	/// <summary>
	/// Enters Play mode in the build's first scene when turned on, for projects whose first
	/// scene sets up the game. Turn it on from Tools/UGFW/Play From First Scene; the choice
	/// is kept per project.
	///
	/// <para>It sets <see cref="EditorSceneManager.playModeStartScene"/> as Play mode starts.
	/// The scenes open in the editor stay open, unsaved changes included, and are back when
	/// Play mode ends, so nothing asks to save them first.</para>
	///
	/// <para>Test Runner runs are left alone: a PlayMode test run enters Play mode in a scene
	/// of its own, and must not be sent to the game's first scene.</para>
	/// </summary>
	[InitializeOnLoad]
	public static class EditorSceneLoader
	{
		private const string MenuPath = "Tools/UGFW/Play From First Scene";
		private const string EnabledName = "EditorSceneLoader.PlayFromFirstScene";
		private const string LegacyEnabledKey = "EditorSceneLoader.AlwaysStartsFromScene";

		// The start scene this loader set, so it clears only its own. SessionState outlives the
		// domain reload that entering Play mode does.
		private const string AssignedSceneKey = "AK.Editor.EditorSceneLoader.AssignedScene";

		// What the Test Runner calls its PlayMode scenes (Assets/InitTestScene<guid>.unity).
		private const string TestRunScenePrefix = "Assets/InitTestScene";

		private static readonly MethodInfo IsRunActiveMethod = FindIsRunActive();

		static EditorSceneLoader()
		{
			ProjectEditorPrefs.AdoptLegacyBool(LegacyEnabledKey, EnabledName);
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		/// <summary>Whether Play mode starts in the build's first scene, in this project.</summary>
		public static bool Enabled
		{
			get => ProjectEditorPrefs.GetBool(EnabledName);
			set
			{
				ProjectEditorPrefs.SetBool(EnabledName, value);
				if (!value && !EditorApplication.isPlayingOrWillChangePlaymode)
				{
					Release();
				}
			}
		}

		[MenuItem(MenuPath, false, 0)]
		private static void Toggle() => Enabled = !Enabled;

		[MenuItem(MenuPath, true, 0)]
		private static bool ToggleValidate()
		{
			Menu.SetChecked(MenuPath, Enabled);
			return true;
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.EnteredEditMode)
			{
				Release();
				return;
			}

			if (state != PlayModeStateChange.ExitingEditMode)
			{
				return;
			}

			if (!Enabled || IsTestRunActive())
			{
				Release();
				return;
			}

			if (!TryAssign(FirstScenePath(EditorBuildSettings.scenes)))
			{
				Release();
				Debug.LogWarning("[UGFW] Play From First Scene is on, but the build has no enabled scene to start in, so Play mode starts in the open scenes.");
			}
		}

		/// <summary>Makes the scene at <paramref name="scenePath"/> the Play mode start scene; false when there is no such scene.</summary>
		private static bool TryAssign(string scenePath)
		{
			SceneAsset scene = string.IsNullOrEmpty(scenePath) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
			if (scene == null)
			{
				return false;
			}

			EditorSceneManager.playModeStartScene = scene;
			SessionState.SetString(AssignedSceneKey, scenePath);
			return true;
		}

		/// <summary>Takes back the start scene this loader set. One set by anything else is kept.</summary>
		private static void Release()
		{
			string assigned = SessionState.GetString(AssignedSceneKey, null);
			if (string.IsNullOrEmpty(assigned))
			{
				return;
			}

			SceneAsset current = EditorSceneManager.playModeStartScene;
			if (current != null && AssetDatabase.GetAssetPath(current) == assigned)
			{
				EditorSceneManager.playModeStartScene = null;
			}

			SessionState.EraseString(AssignedSceneKey);
		}

		/// <summary>The first enabled scene in <paramref name="scenes"/>, where a build starts; null when there is none.</summary>
		internal static string FirstScenePath(EditorBuildSettingsScene[] scenes)
		{
			foreach (EditorBuildSettingsScene scene in scenes)
			{
				if (scene.enabled && !string.IsNullOrEmpty(scene.path))
				{
					return scene.path;
				}
			}

			return null;
		}

		/// <summary>
		/// Whether the Test Runner is running tests. The Test Framework has no public way to tell:
		/// this asks its internal <c>TestRunnerApi.IsRunActive</c>, and also takes the scene a
		/// PlayMode run enters in as a sign, in case that method is renamed.
		/// </summary>
		internal static bool IsTestRunActive()
		{
			if (IsRunActiveMethod != null && (bool)IsRunActiveMethod.Invoke(null, null))
			{
				return true;
			}

			return SceneManager.GetActiveScene().path.StartsWith(TestRunScenePrefix, StringComparison.Ordinal);
		}

		private static MethodInfo FindIsRunActive()
		{
			Type api = Type.GetType("UnityEditor.TestTools.TestRunner.Api.TestRunnerApi, UnityEditor.TestRunner");
			MethodInfo method = api?.GetMethod("IsRunActive", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			return method != null && method.ReturnType == typeof(bool) ? method : null;
		}
	}
}
