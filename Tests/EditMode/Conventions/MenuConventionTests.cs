using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AK.Tests.Conventions
{
	/// <summary>
	/// Where UGFW's menu items go: its tools under Tools/UGFW, the assets it defines under
	/// Assets/Create/AK. Items added to Unity's own menus for a selection (CONTEXT/, Assets/,
	/// GameObject/) stay where Unity shows them.
	/// </summary>
	public class MenuConventionTests
	{
		private const string ToolsRoot = "Tools/UGFW/";
		private const string CreateRoot = "AK/";

		private static readonly string[] SelectionRoots = { "CONTEXT/", "Assets/", "GameObject/" };

		[Test]
		public void EveryMenuItem_IsUnderToolsUgfw()
		{
			int count = 0;
			var strays = new List<string>();
			foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<MenuItem>())
			{
				if (!UgfwAssemblies.IsUgfw(method.DeclaringType.Assembly))
				{
					continue;
				}

				foreach (MenuItem item in method.GetCustomAttributes<MenuItem>(false))
				{
					count++;
					if (!IsToolOrSelectionItem(item.menuItem))
					{
						strays.Add(method.DeclaringType.FullName + "." + method.Name + ": " + item.menuItem);
					}
				}
			}

			Assert.Greater(count, 0, "UGFW has menu items to check");
			Assert.IsEmpty(strays, "Menu items outside " + ToolsRoot + ":\n" + string.Join("\n", strays));
		}

		[Test]
		public void EveryAssetMenu_IsUnderAk()
		{
			int count = 0;
			var strays = new List<string>();
			foreach (Type type in TypeCache.GetTypesWithAttribute<CreateAssetMenuAttribute>())
			{
				if (!UgfwAssemblies.IsUgfw(type.Assembly))
				{
					continue;
				}

				foreach (CreateAssetMenuAttribute menu in type.GetCustomAttributes<CreateAssetMenuAttribute>(false))
				{
					count++;
					if (menu.menuName == null || !menu.menuName.StartsWith(CreateRoot, StringComparison.Ordinal))
					{
						strays.Add(type.FullName + ": " + menu.menuName);
					}
				}
			}

			Assert.Greater(count, 0, "UGFW has asset menus to check");
			Assert.IsEmpty(strays, "Asset menus outside Create/" + CreateRoot + ":\n" + string.Join("\n", strays));
		}

		private static bool IsToolOrSelectionItem(string path)
		{
			if (path.StartsWith(ToolsRoot, StringComparison.Ordinal))
			{
				return true;
			}

			foreach (string root in SelectionRoots)
			{
				if (path.StartsWith(root, StringComparison.Ordinal))
				{
					return true;
				}
			}

			return false;
		}
	}
}
