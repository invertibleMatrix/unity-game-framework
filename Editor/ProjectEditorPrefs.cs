using UnityEditor;
using UnityEngine;

namespace AK.Editor
{
	/// <summary>
	/// EditorPrefs for UGFW's editor tools, kept apart per project. EditorPrefs are shared by
	/// every project on the machine, so each key here carries a hash of the project's Assets
	/// path: a setting made in one project stays out of the others, and a checkout in another
	/// folder has its own.
	/// </summary>
	internal static class ProjectEditorPrefs
	{
		private static string _projectScope;

		/// <summary>This project's part of every key.</summary>
		internal static string ProjectScope => _projectScope ??= ScopeOf(Application.dataPath);

		/// <summary>The scope for the project whose Assets folder is <paramref name="dataPath"/>.</summary>
		internal static string ScopeOf(string dataPath) => Hash128.Compute(dataPath).ToString();

		/// <summary>The EditorPrefs key for <paramref name="name"/> in the project <paramref name="scope"/>.</summary>
		internal static string Key(string name, string scope) => "UGFW." + scope + "." + name;

		internal static bool GetBool(string name, bool fallback = false) => GetBool(name, fallback, ProjectScope);

		internal static bool GetBool(string name, bool fallback, string scope) => EditorPrefs.GetBool(Key(name, scope), fallback);

		internal static void SetBool(string name, bool value) => SetBool(name, value, ProjectScope);

		internal static void SetBool(string name, bool value, string scope) => EditorPrefs.SetBool(Key(name, scope), value);

		/// <summary>
		/// Takes over a machine-wide value from before keys were kept per project: copies
		/// <paramref name="legacyKey"/> into the project's key when the project has none yet.
		/// The machine-wide key stays, for projects on older versions.
		/// </summary>
		internal static void AdoptLegacyBool(string legacyKey, string name) => AdoptLegacyBool(legacyKey, name, ProjectScope);

		internal static void AdoptLegacyBool(string legacyKey, string name, string scope)
		{
			string key = Key(name, scope);
			if (EditorPrefs.HasKey(key) || !EditorPrefs.HasKey(legacyKey))
			{
				return;
			}

			EditorPrefs.SetBool(key, EditorPrefs.GetBool(legacyKey));
		}
	}
}
