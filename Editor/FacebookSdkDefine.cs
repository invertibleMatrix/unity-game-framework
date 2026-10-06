using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace AK.Editor
{
	/// <summary>
	/// Keeps <c>-define:UGFW_FACEBOOK_SDK</c> in Assets/csc.rsp while the Facebook SDK for Unity is
	/// in the project, so the Meta provider (AK.Services.Meta) compiles with it and is left out
	/// without it. The SDK comes as DLLs, not a package, so no version define can find it; its
	/// Facebook.Unity.dll can.
	///
	/// <para>The provider's code is under <c>#if</c>, not the assembly under a define constraint.
	/// Unity 6.3 checks constraints against the file's defines as they were when the domain
	/// loaded, so the compile after a change still sees the old ones: adding the define would
	/// leave the provider out until some later compile, and removing it would compile the
	/// provider against an SDK that is gone. The compiler itself always gets the file as it is.</para>
	///
	/// <para>The check runs after every domain reload and DLL import. The file applies to every
	/// build target and build profile. Commit it: a batch-mode build reads it at startup, before
	/// any check runs, and in batch mode the check only warns.</para>
	/// </summary>
	[InitializeOnLoad]
	public static class FacebookSdkDefine
	{
		/// <summary>The define AK.Services.Meta needs.</summary>
		public const string Symbol = "UGFW_FACEBOOK_SDK";

		/// <summary>The response file that holds the define.</summary>
		public const string ResponseFilePath = "Assets/csc.rsp";

		private const string SdkAssembly = "Facebook.Unity.dll";
		private const string DefineLine = "-define:" + Symbol;

		static FacebookSdkDefine()
		{
			// In batch mode Sync only warns, and a build may start before the editor updates.
			if (Application.isBatchMode)
			{
				Sync();
			}
			else
			{
				SyncSoon();
			}
		}

		/// <summary>Whether the Facebook SDK's Facebook.Unity.dll is in the project.</summary>
		public static bool SdkFound()
		{
			return Array.IndexOf(CompilationPipeline.GetPrecompiledAssemblyNames(), SdkAssembly) >= 0;
		}

		/// <summary>Whether Assets/csc.rsp has the define.</summary>
		public static bool Defined()
		{
			return HasDefine(ReadResponseFile());
		}

		/// <summary>
		/// Adds the define to Assets/csc.rsp or removes it, to match <see cref="SdkFound"/>, and
		/// recompiles if that changed it. Other lines in the file are kept; a file left empty is
		/// deleted.
		/// </summary>
		public static void Sync()
		{
			bool found = SdkFound();
			string text = ReadResponseFile();
			if (HasDefine(text) == found)
			{
				return;
			}

			if (Application.isBatchMode)
			{
				Debug.LogWarning(found
					? $"[UGFW] The Facebook SDK is in the project, but {ResponseFilePath} doesn't define {Symbol}, so the Meta provider isn't compiled. Open the project in the editor and commit {ResponseFilePath}."
					: $"[UGFW] {ResponseFilePath} defines {Symbol}, but the Facebook SDK isn't in the project, so the Meta provider can't compile. Remove the line '{DefineLine}'.");
				return;
			}

			string updated = WithDefine(text, found);
			if (updated == null)
			{
				AssetDatabase.DeleteAsset(ResponseFilePath);
			}
			else
			{
				File.WriteAllText(ResponseFilePath, updated, new UTF8Encoding(false));
				AssetDatabase.ImportAsset(ResponseFilePath);
			}

			Debug.Log(found
				? $"[UGFW] Found the Facebook SDK: {ResponseFilePath} now defines {Symbol}, and the Meta provider compiles."
				: $"[UGFW] The Facebook SDK is gone: {ResponseFilePath} no longer defines {Symbol}, and the Meta provider is left out.");
			CompilationPipeline.RequestScriptCompilation();
		}

		/// <summary>
		/// Runs <see cref="Sync"/> on the next editor update, once the reload or import in progress
		/// is over. An update callback runs with the editor in the background too, unlike delayCall.
		/// </summary>
		internal static void SyncSoon()
		{
			EditorApplication.update -= SyncOnce;
			EditorApplication.update += SyncOnce;
		}

		private static void SyncOnce()
		{
			EditorApplication.update -= SyncOnce;
			Sync();
		}

		/// <summary>Whether the response file text <paramref name="text"/> has the define's line.</summary>
		internal static bool HasDefine(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}

			foreach (string line in text.Split('\n'))
			{
				if (line.Trim() == DefineLine)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// The response file text <paramref name="text"/> with the define's line added at the end
		/// or removed, keeping the other lines and their line endings. Null when no other line
		/// would be left.
		/// </summary>
		internal static string WithDefine(string text, bool defined)
		{
			text ??= string.Empty;
			if (defined)
			{
				if (HasDefine(text))
				{
					return text;
				}

				string newline = text.Contains("\r\n") ? "\r\n" : "\n";
				bool ended = text.Length == 0 || text.EndsWith("\n", StringComparison.Ordinal);
				return text + (ended ? string.Empty : newline) + DefineLine + newline;
			}

			var kept = new StringBuilder(text.Length);
			int start = 0;
			while (start < text.Length)
			{
				int end = text.IndexOf('\n', start);
				end = end < 0 ? text.Length : end + 1;
				string line = text.Substring(start, end - start);
				if (line.Trim() != DefineLine)
				{
					kept.Append(line);
				}

				start = end;
			}

			string result = kept.ToString();
			return result.Trim().Length == 0 ? null : result;
		}

		private static string ReadResponseFile()
		{
			return File.Exists(ResponseFilePath) ? File.ReadAllText(ResponseFilePath) : null;
		}
	}

	/// <summary>Has <see cref="FacebookSdkDefine"/> check again whenever a DLL comes, goes or moves.</summary>
	internal sealed class FacebookSdkDllWatcher : AssetPostprocessor
	{
		private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
		{
			if (AnyDll(imported) || AnyDll(deleted) || AnyDll(moved))
			{
				FacebookSdkDefine.SyncSoon();
			}
		}

		private static bool AnyDll(string[] paths)
		{
			foreach (string path in paths)
			{
				if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}
	}
}
