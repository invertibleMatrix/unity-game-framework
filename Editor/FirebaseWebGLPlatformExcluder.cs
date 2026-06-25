#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AK.Editor
{
	/// <summary>
	/// Automatically excludes the Firebase SDK assemblies from WebGL builds.
	///
	/// The Firebase Unity SDK (13.x) does not properly exclude WebGL from its
	/// asmdef files, causing compilation errors like:
	///   CS0246: The type or namespace name 'FirebaseApp' could not be found
	///
	/// This script patches the Firebase asmdef files after package import to
	/// add "WebGL" to their excludePlatforms array. It runs automatically on
	/// DomainReload (after Unity recompiles) and can also be triggered manually
	/// via the menu: Tools → Firebase → Exclude WebGL from Firebase Assemblies
	///
	/// This avoids editing files in Library/PackageCache manually, as the script
	/// re-applies the patch whenever Unity reimports the packages.
	/// </summary>
	public static class FirebaseWebGLPlatformExcluder
	{
		private const string MenuPath = "Tools/Firebase/Exclude WebGL from Firebase Assemblies";

		/// <summary>
		/// Asmdef files to patch. These are relative to the package cache.
		/// </summary>
		private static readonly string[] FirebaseAsmdefRelativePaths = new[]
		{
			"Firebase/FirebaseApp/Internal/Firebase.App.Internal.asmdef",
		};

		[InitializeOnLoadMethod]
		public static void AutoPatch()
		{
			// Only patch when the active build target is WebGL — no overhead on other platforms.
			if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
				return;

			// Only patch when not building (to avoid interfering with build pipeline).
			if (BuildPipeline.isBuildingPlayer)
				return;

			// Delay execution to ensure packages are fully imported.
			EditorApplication.delayCall += () => PatchAll();
		}

		[MenuItem(MenuPath)]
		public static void PatchAll()
		{
			int patched = 0;

			foreach (string relativePath in FirebaseAsmdefRelativePaths)
			{
				if (TryFindFirebaseAsmdef(relativePath, out string fullPath))
				{
					if (PatchAsmdef(fullPath))
					{
						patched++;
					}
				}
			}

			if (patched > 0)
			{
				Debug.Log($"[FirebaseWebGLPlatformExcluder] Patched {patched} Firebase asmdef file(s) to exclude WebGL.");
				AssetDatabase.Refresh();
			}
		}

		/// <summary>
		/// Finds the Firebase asmdef file by searching the package cache.
		/// </summary>
		private static bool TryFindFirebaseAsmdef(string relativePath, out string fullPath)
		{
			// Search in Library/PackageCache for com.google.firebase.app@*
			string packageCacheDir = Path.Combine(Application.dataPath, "..", "Library", "PackageCache");

			if (!Directory.Exists(packageCacheDir))
			{
				fullPath = null;
				return false;
			}

			// Find directories matching com.google.firebase.app@*
			string[] firebaseDirs = Directory.GetDirectories(packageCacheDir, "com.google.firebase.app@*");

			foreach (string dir in firebaseDirs)
			{
				string candidate = Path.Combine(dir, relativePath.Replace('/', Path.DirectorySeparatorChar));

				if (File.Exists(candidate))
				{
					fullPath = candidate;
					return true;
				}
			}

			fullPath = null;
			return false;
		}

		/// <summary>
		/// Patches a single asmdef file to add "WebGL" to excludePlatforms.
		/// Returns true if the file was modified, false if already patched or not found.
		/// </summary>
		private static bool PatchAsmdef(string asmdefPath)
		{
			try
			{
				string json = File.ReadAllText(asmdefPath);

				// Check if already patched.
				if (json.Contains("\"WebGL\""))
				{
					return false;
				}

				// Add "WebGL" to the excludePlatforms array.
				// Handle both "excludePlatforms": [] and "excludePlatforms": ["..."]
				if (json.Contains("\"excludePlatforms\": []"))
				{
					json = json.Replace("\"excludePlatforms\": []", "\"excludePlatforms\": [\"WebGL\"]");
				}
				else if (json.Contains("\"excludePlatforms\": ["))
				{
					// Already has some excluded platforms, add WebGL.
					json = json.Replace("\"excludePlatforms\": [", "\"excludePlatforms\": [\"WebGL\", ");
				}
				else
				{
					Debug.LogWarning($"[FirebaseWebGLPlatformExcluder] Could not find excludePlatforms in {asmdefPath}");
					return false;
				}

				File.WriteAllText(asmdefPath, json);
				return true;
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[FirebaseWebGLPlatformExcluder] Failed to patch {asmdefPath}: {e.Message}");
				return false;
			}
		}
	}
}
#endif
