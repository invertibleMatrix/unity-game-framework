using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace AK.Tests.Conventions
{
	/// <summary>
	/// How UGFW names its namespaces, checked over every UGFW assembly loaded in the editor.
	/// Each type is in the AK namespace or one inside it, so UGFW's names stay out of the
	/// game's. No namespace is named after a type in it: where the namespace is in scope, the
	/// type's name means the namespace, which is why the Framework Design Guidelines rule it out.
	/// </summary>
	public class NamespaceConventionTests
	{
		// IL2CPP finds its option attribute by this full name, so the attribute keeps it. It is
		// internal to the assembly that uses it.
		private const string Il2CppNamespace = "Unity.IL2CPP.CompilerServices";

		[Test]
		public void TheAssembliesUgfwAlwaysCompiles_AreChecked()
		{
			var loaded = new HashSet<string>();
			foreach (Assembly assembly in UgfwAssemblies.Loaded())
			{
				loaded.Add(assembly.GetName().Name);
			}

			CollectionAssert.IsSubsetOf(UgfwAssemblies.AlwaysCompiled, loaded);
		}

		[Test]
		public void EveryType_IsInTheAkNamespace()
		{
			var strays = new List<string>();
			foreach (Assembly assembly in UgfwAssemblies.Loaded())
			{
				foreach (Type type in UgfwAssemblies.WrittenTypes(assembly))
				{
					if (UgfwAssemblies.IsWithin(type.Namespace, "AK") || (type.Namespace == Il2CppNamespace && !type.IsPublic))
					{
						continue;
					}

					strays.Add(type.FullName + " (" + assembly.GetName().Name + ")");
				}
			}

			Assert.IsEmpty(strays, "Types outside the AK namespace:\n" + string.Join("\n", strays));
		}

		[Test]
		public void NoNamespace_IsNamedAfterATypeInIt()
		{
			var clashes = new List<string>();
			foreach (Assembly assembly in UgfwAssemblies.Loaded())
			{
				foreach (Type type in UgfwAssemblies.WrittenTypes(assembly))
				{
					string ns = type.Namespace;
					if (ns != null && string.Equals(NameWithoutArity(type.Name), ns.Substring(ns.LastIndexOf('.') + 1), StringComparison.Ordinal))
					{
						clashes.Add(type.FullName + " (" + assembly.GetName().Name + ")");
					}
				}
			}

			Assert.IsEmpty(clashes, "Types named like their namespace:\n" + string.Join("\n", clashes));
		}

		// "Registry`1" is Registry<T>.
		private static string NameWithoutArity(string typeName)
		{
			int tick = typeName.IndexOf('`');
			return tick < 0 ? typeName : typeName.Substring(0, tick);
		}
	}
}
