using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor;

namespace AK.Core.Editor
{
	/// <summary>
	/// Build gate for UID identity integrity. The two silent killers — an empty logical
	/// GUID and a duplicated one — fail the build instead of shipping broken links and
	/// orphaned counts. Registries tracking deleted or identity-less assets fail too.
	/// </summary>
	public class UIDBuildValidator : IPreprocessBuildWithReport
	{
		public int callbackOrder => 0;

		public void OnPreprocessBuild(BuildReport report)
		{
			var errors = new List<string>();
			var seenIds = new Dictionary<string, string>();

			foreach (string guid in AssetDatabase.FindAssets("t:UID"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var uid = AssetDatabase.LoadAssetAtPath<UID>(path);
				if (uid == null) continue;

				if (uid.IsEmpty())
				{
					errors.Add($"[UID] '{path}' has an empty logical ID. Run Tools → UGFW → Repair UID Assets.");
					continue;
				}

				if (seenIds.TryGetValue(uid.Id, out string firstPath))
				{
					errors.Add($"[UID] Duplicate logical ID shared by '{firstPath}' and '{path}'. Assign a fresh GUID to one of them.");
				}
				else
				{
					seenIds[uid.Id] = path;
				}
			}

			foreach (string guid in AssetDatabase.FindAssets("t:UIDRegistryAsset"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var registry = AssetDatabase.LoadAssetAtPath<UIDRegistryAsset>(path);
				if (registry == null) continue;

				foreach (var obj in registry.GetTrackedObjects())
				{
					if (obj == null)
					{
						errors.Add($"[UID] Registry '{path}' tracks a deleted asset.");
					}
					else if (obj.IsEmpty())
					{
						errors.Add($"[UID] Registry '{path}' tracks '{obj.name}' which has an empty logical ID.");
					}
				}
			}

			if (errors.Count > 0)
			{
				throw new BuildFailedException(
					$"UID integrity check failed with {errors.Count} error(s):\n" + string.Join("\n", errors));
			}
		}
	}
}
