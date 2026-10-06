using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AK.Tests.Conventions
{
	/// <summary>
	/// The UGFW assemblies loaded in the editor, the ones the convention tests check: every AK
	/// assembly but the tests and the examples.
	/// </summary>
	internal static class UgfwAssemblies
	{
		/// <summary>The assemblies UGFW compiles in every project, with or without optional packages.</summary>
		internal static readonly string[] AlwaysCompiled =
		{
			"AK.Kernel",
			"AK.Core",
			"AK.Jobs",
			"AK.CoreDomain",
			"AK.Services",
			"AK.UISystem",
			"AK.Tutorials",
			"AK.Content",
			"AK.Editor",
			"AK.Core.UID.Editor",
			"AK.Tutorials.Editor",
			"AK.UISystem.Editor",
		};

		internal static bool IsUgfw(Assembly assembly) => IsUgfw(assembly.GetName().Name);

		internal static bool IsUgfw(string assemblyName)
		{
			return IsWithin(assemblyName, "AK") && !IsWithin(assemblyName, "AK.Tests") && !IsWithin(assemblyName, "AK.Examples");
		}

		internal static List<Assembly> Loaded()
		{
			var result = new List<Assembly>();
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (!assembly.IsDynamic && IsUgfw(assembly))
				{
					result.Add(assembly);
				}
			}

			return result;
		}

		/// <summary>The top-level types written in <paramref name="assembly"/>; the ones the compiler adds are left out.</summary>
		internal static IEnumerable<Type> WrittenTypes(Assembly assembly)
		{
			foreach (Type type in assembly.GetTypes())
			{
				if (!type.IsNested && !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
				{
					yield return type;
				}
			}
		}

		/// <summary>Whether the dotted <paramref name="name"/> is <paramref name="root"/> or a name inside it.</summary>
		internal static bool IsWithin(string name, string root)
		{
			if (name == null || !name.StartsWith(root, StringComparison.Ordinal))
			{
				return false;
			}

			return name.Length == root.Length || name[root.Length] == '.';
		}
	}
}
