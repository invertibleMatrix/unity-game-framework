using System.Collections.Generic;
using System.Text;

namespace AK.Core.Editor
{
	/// <summary>
	/// Result of one project audit. Errors block a build; warnings are surfaced but do not.
	/// Each finding carries the asset path so a CI log line is actionable on its own.
	/// </summary>
	public sealed class UidAuditReport
	{
		public enum Severity { Error, Warning, Info }

		public readonly struct Finding
		{
			public readonly Severity Severity;
			public readonly string   Rule;
			public readonly string   Message;
			public readonly string   AssetPath;

			public Finding(Severity severity, string rule, string message, string assetPath)
			{
				Severity  = severity;
				Rule      = rule;
				Message   = message;
				AssetPath = assetPath;
			}
		}

		public readonly List<Finding> Findings = new();

		public int AssetsScanned;
		public int RegistriesScanned;
		public int IdentitiesIndexed;

		public int ErrorCount
		{
			get
			{
				int n = 0;
				foreach (var f in Findings) if (f.Severity == Severity.Error) n++;
				return n;
			}
		}

		public int WarningCount
		{
			get
			{
				int n = 0;
				foreach (var f in Findings) if (f.Severity == Severity.Warning) n++;
				return n;
			}
		}

		public bool HasErrors => ErrorCount > 0;

		public void Error(string rule, string message, string assetPath = null)   => Findings.Add(new Finding(Severity.Error, rule, message, assetPath));
		public void Warning(string rule, string message, string assetPath = null) => Findings.Add(new Finding(Severity.Warning, rule, message, assetPath));
		public void Info(string rule, string message, string assetPath = null)    => Findings.Add(new Finding(Severity.Info, rule, message, assetPath));

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append("[UID Audit] ").Append(AssetsScanned).Append(" identity assets, ")
			  .Append(RegistriesScanned).Append(" registries, ")
			  .Append(IdentitiesIndexed).Append(" identities — ")
			  .Append(ErrorCount).Append(" error(s), ").Append(WarningCount).Append(" warning(s)");

			foreach (var f in Findings)
			{
				sb.AppendLine();
				sb.Append("  ").Append(f.Severity.ToString().ToUpperInvariant()).Append(' ')
				  .Append('[').Append(f.Rule).Append("] ").Append(f.Message);
				if (!string.IsNullOrEmpty(f.AssetPath)) sb.Append("  @ ").Append(f.AssetPath);
			}

			return sb.ToString();
		}
	}
}
