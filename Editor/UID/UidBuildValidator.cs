using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Build gate. Runs the full audit and fails the build on any error: a missing identity,
	/// a collision, a dangling redirect, an identity asset inside Addressables, or persisted
	/// state holding an asset reference. Every one of these ships silently otherwise and
	/// surfaces as lost progress or broken links in the field.
	/// </summary>
	public sealed class UidBuildValidator : IPreprocessBuildWithReport
	{
		public int callbackOrder => -100;

		public void OnPreprocessBuild(BuildReport report)
		{
			UidAuditReport audit = UidAuditMenu.Audit();

			if (audit.HasErrors)
			{
				Debug.LogError(audit.ToString());
				throw new BuildFailedException($"[UID] {audit.ErrorCount} identity error(s). See the audit report above; run Tools → UGFW → UID → Audit Project.");
			}

			if (audit.WarningCount > 0)
			{
				Debug.LogWarning(audit.ToString());
			}
			else
			{
				Debug.Log(audit.ToString());
			}
		}
	}
}
