using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AK.Editor
{
	/// <summary>
	/// Tools > UGFW > Integrations: each UGFW assembly with optional parts, the packages it looks
	/// for, which are installed, and the defines and assemblies that turns on, read from the
	/// assembly definitions; then the Facebook SDK and the Meta provider. Nothing in it is set by
	/// hand: version defines follow the installed packages, and <see cref="FacebookSdkDefine"/>
	/// follows the Facebook SDK.
	/// </summary>
	public sealed class UGFWIntegrationsWindow : EditorWindow
	{
		private const string UgfwPrefix = "AK.";
		private const string TestsConstraint = "UNITY_INCLUDE_TESTS";
		private const string MetaProvider = "AK.Services.Analytics.Providers.MetaProvider, AK.Services.Meta";

		private List<AssemblyStatus> _assemblies = new();
		private bool _facebookSdkFound;
		private bool _facebookSdkDefined;
		private bool _metaProviderCompiled;
		private Vector2 _scroll;

		[MenuItem("Tools/UGFW/Integrations")]
		private static void Open()
		{
			GetWindow<UGFWIntegrationsWindow>().minSize = new Vector2(640, 280);
		}

		// Runs again after every domain reload, so a recompile or a package change shows.
		private void OnEnable()
		{
			titleContent = new GUIContent("UGFW Integrations");
			Refresh();
		}

		private void Refresh()
		{
			_assemblies = ReadAssemblies();
			_facebookSdkFound = FacebookSdkDefine.SdkFound();
			_facebookSdkDefined = FacebookSdkDefine.Defined();
			_metaProviderCompiled = Type.GetType(MetaProvider) != null;
		}

		private void OnGUI()
		{
			EditorGUILayout.Space(4);
			EditorGUILayout.HelpBox(
				"UGFW turns its optional parts on from what is installed. A version define is on while its package is installed at the version shown, and an assembly compiles while its constraints are met.",
				MessageType.Info);

			if (GUILayout.Button("Refresh", GUILayout.Width(80)))
			{
				Refresh();
			}

			// Wide enough for a package name and its version range.
			float labelWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = Mathf.Max(340f, position.width * 0.45f);

			_scroll = EditorGUILayout.BeginScrollView(_scroll);
			foreach (AssemblyStatus assembly in _assemblies)
			{
				DrawAssembly(assembly);
			}

			DrawFacebookSdk();
			EditorGUILayout.EndScrollView();
			EditorGUIUtility.labelWidth = labelWidth;
		}

		private void DrawAssembly(AssemblyStatus assembly)
		{
			EditorGUILayout.Space(6);
			EditorGUILayout.LabelField(assembly.Name, assembly.Compiled ? "compiled" : "not compiled", EditorStyles.boldLabel);

			EditorGUI.indentLevel++;
			foreach (VersionDefineStatus define in assembly.VersionDefines)
			{
				EditorGUILayout.LabelField(
					define.Resource + Requirement(define.Expression),
					$"{define.InstalledVersion ?? "not installed"}  ->  {define.Define} {OnOff(define.On)}");
			}

			foreach (string constraint in assembly.OtherConstraints)
			{
				EditorGUILayout.LabelField(constraint, "a project define");
			}

			EditorGUI.indentLevel--;
		}

		private void DrawFacebookSdk()
		{
			EditorGUILayout.Space(6);
			EditorGUILayout.LabelField("AK.Services.Meta", _metaProviderCompiled ? "Meta provider compiled" : "Meta provider left out", EditorStyles.boldLabel);

			EditorGUI.indentLevel++;
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(
				"Facebook SDK (Facebook.Unity.dll)",
				$"{(_facebookSdkFound ? "found" : "not found")}  ->  {FacebookSdkDefine.Symbol} {OnOff(_facebookSdkDefined)} in {FacebookSdkDefine.ResponseFilePath}");

			if (GUILayout.Button("Check again", GUILayout.Width(90)))
			{
				FacebookSdkDefine.Sync();
				Refresh();
			}

			EditorGUILayout.EndHorizontal();
			EditorGUI.indentLevel--;
		}

		private static string Requirement(string expression)
		{
			if (string.IsNullOrEmpty(expression))
			{
				return string.Empty;
			}

			// A bare version means that version or later; anything else is a range.
			return char.IsDigit(expression[0]) ? $" ({expression} or later)" : $" {expression}";
		}

		private static string OnOff(bool on)
		{
			return on ? "on" : "off";
		}

		/// <summary>UGFW's assembly definitions with version defines or constraints, tests left out, by name.</summary>
		private static List<AssemblyStatus> ReadAssemblies()
		{
			var compiled = new Dictionary<string, Assembly>(StringComparer.Ordinal);
			foreach (Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
			{
				compiled[assembly.name] = assembly;
			}

			var statuses = new List<AssemblyStatus>();
			foreach (string guid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset"))
			{
				var asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid));
				AsmdefJson asmdef = asset != null ? JsonUtility.FromJson<AsmdefJson>(asset.text) : null;
				if (asmdef?.name == null || !asmdef.name.StartsWith(UgfwPrefix, StringComparison.Ordinal)
				    || Array.IndexOf(asmdef.defineConstraints, TestsConstraint) >= 0
				    || (asmdef.versionDefines.Length == 0 && asmdef.defineConstraints.Length == 0))
				{
					continue;
				}

				compiled.TryGetValue(asmdef.name, out Assembly compiledAssembly);
				statuses.Add(new AssemblyStatus(asmdef, compiledAssembly));
			}

			statuses.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
			return statuses;
		}

		private sealed class AssemblyStatus
		{
			public readonly string Name;
			public readonly bool Compiled;
			public readonly List<VersionDefineStatus> VersionDefines = new();

			// Constraints no version define of the assembly's own sets.
			public readonly List<string> OtherConstraints = new();

			public AssemblyStatus(AsmdefJson asmdef, Assembly compiled)
			{
				Name = asmdef.name;
				Compiled = compiled != null;

				var ownDefines = new HashSet<string>(StringComparer.Ordinal);
				foreach (VersionDefineJson define in asmdef.versionDefines)
				{
					ownDefines.Add(define.define);
					bool on = compiled != null && Array.IndexOf(compiled.defines, define.define) >= 0;
					VersionDefines.Add(new VersionDefineStatus(define.name, define.expression, define.define, InstalledVersion(define.name), on));
				}

				foreach (string constraint in asmdef.defineConstraints)
				{
					if (!ownDefines.Contains(constraint))
					{
						OtherConstraints.Add(constraint);
					}
				}
			}

			private static string InstalledVersion(string resource)
			{
				return resource == "Unity" ? Application.unityVersion : PackageInfo.FindForPackageName(resource)?.version;
			}
		}

		private readonly struct VersionDefineStatus
		{
			public readonly string Resource;
			public readonly string Expression;
			public readonly string Define;
			public readonly string InstalledVersion;
			public readonly bool On;

			public VersionDefineStatus(string resource, string expression, string define, string installedVersion, bool on)
			{
				Resource = resource;
				Expression = expression;
				Define = define;
				InstalledVersion = installedVersion;
				On = on;
			}
		}

		// The parts of an assembly definition file this window reads.
		[Serializable]
		private sealed class AsmdefJson
		{
			public string name;
			public string[] defineConstraints = Array.Empty<string>();
			public VersionDefineJson[] versionDefines = Array.Empty<VersionDefineJson>();
		}

		[Serializable]
		private sealed class VersionDefineJson
		{
			public string name;
			public string expression;
			public string define;
		}
	}
}
