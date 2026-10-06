using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting;

namespace AK.Tests.Conventions
{
	/// <summary>
	/// How UGFW's assemblies get through managed code stripping. A package assembly is never a
	/// stripping root: an IL2CPP build keeps it only when a scene uses one of its types or a kept
	/// assembly's code does, and an asmdef reference the code never uses doesn't count. So an
	/// assembly that is reached only through a startup hook, like a provider filling its factory,
	/// is removed whole, hook included, unless it carries [AlwaysLinkAssembly]. The editor never
	/// strips, so nothing short of a device build would show it.
	/// </summary>
	public class LinkerConventionTests
	{
		[Test]
		public void SelfRegisteringAssemblies_AreAlwaysLinked()
		{
			HashSet<string> player = PlayerAssemblyNames();
			CollectionAssert.Contains(player, "AK.Core", "UGFW's player assemblies weren't found.");

			// What UGFW's own player code uses. The editor assemblies never reach the linker.
			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (Assembly assembly in UgfwAssemblies.Loaded())
			{
				if (!player.Contains(assembly.GetName().Name))
				{
					continue;
				}

				foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
				{
					used.Add(reference.Name);
				}
			}

			var removed = new SortedSet<string>(StringComparer.Ordinal);
			foreach (MethodInfo hook in TypeCache.GetMethodsWithAttribute<RuntimeInitializeOnLoadMethodAttribute>())
			{
				Assembly assembly = hook.DeclaringType.Assembly;
				string name = assembly.GetName().Name;
				if (UgfwAssemblies.IsUgfw(name) && player.Contains(name) && !used.Contains(name) && !assembly.IsDefined(typeof(AlwaysLinkAssemblyAttribute), false))
				{
					removed.Add(name);
				}
			}

			Assert.IsEmpty(removed, "Assemblies IL2CPP builds would remove, startup hooks included:\n" + string.Join("\n", removed));
		}

		private static HashSet<string> PlayerAssemblyNames()
		{
			var names = new HashSet<string>(StringComparer.Ordinal);
			foreach (UnityEditor.Compilation.Assembly assembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.PlayerWithoutTestAssemblies))
			{
				names.Add(assembly.name);
			}

			return names;
		}
	}
}
