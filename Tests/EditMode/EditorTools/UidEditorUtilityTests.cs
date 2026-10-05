using System.Collections.Generic;
using AK.Core;
using AK.Core.Editor;
using NUnit.Framework;
using UnityEditor;

namespace AK.Tests.EditorTools
{
	/// <summary>
	/// The checks the UID tools make on paths: the files the postprocessors load, and the folders
	/// a namespace lookup walks. The tests read assets already in the project and load none of them.
	/// </summary>
	public class UidEditorUtilityTests
	{
		[TestCase("Assets/A.asset", true)]
		[TestCase("Assets/A.ASSET", true)]
		[TestCase("Assets/A.prefab", false)]
		[TestCase("Assets/A.assets", false)]
		[TestCase("Assets/A.asset.meta", false)]
		public void IsAssetFile_GoesByTheExtension(string path, bool expected)
		{
			Assert.AreEqual(expected, UidEditorUtility.IsAssetFile(path));
		}

		[TestCase("Assets", true)]
		[TestCase("Assets/Data", true)]
		[TestCase("Packages/com.example.game", true)]
		[TestCase("Packages/com.example.game/Data", true)]
		[TestCase("Packages", false)]
		[TestCase("Packages/", false)]
		[TestCase("AssetsBackup", false)]
		[TestCase("Library/Data", false)]
		[TestCase("", false)]
		public void IsAssetFolder_GoesByThePath(string folder, bool expected)
		{
			Assert.AreEqual(expected, UidEditorUtility.IsAssetFolder(folder));
		}

		[Test]
		public void IsUidAsset_IsFalse_WhereThereIsNoAsset()
		{
			Assert.IsFalse(UidEditorUtility.IsUidAsset("Assets/UidEditorUtilityTests_Missing/Nothing.asset"));
		}

		[Test]
		public void IsUidAsset_IsTrue_ForAUidAsset_LeftUnloaded()
		{
			string path = UnloadedAssetFile(AssetDatabase.FindAssets("t:" + nameof(UID)));

			Assert.IsTrue(UidEditorUtility.IsUidAsset(path), path);
			Assert.IsFalse(AssetDatabase.IsMainAssetAtPathLoaded(path), path);
		}

		[Test]
		public void IsUidAsset_IsFalse_ForAnotherAsset_LeftUnloaded()
		{
			var uids = new HashSet<string>(AssetDatabase.FindAssets("t:" + nameof(UID)));
			var others = new List<string>();
			foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject"))
			{
				if (!uids.Contains(guid))
				{
					others.Add(guid);
				}
			}

			string path = UnloadedAssetFile(others);

			Assert.IsFalse(UidEditorUtility.IsUidAsset(path), path);
			Assert.IsFalse(AssetDatabase.IsMainAssetAtPathLoaded(path), path);
		}

		// The first .asset file whose main asset is not in memory, so a test can tell whether a
		// check loaded it.
		private static string UnloadedAssetFile(IEnumerable<string> guids)
		{
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (UidEditorUtility.IsAssetFile(path) && !AssetDatabase.IsMainAssetAtPathLoaded(path))
				{
					return path;
				}
			}

			Assert.Inconclusive("The project has no such .asset file that is not loaded.");
			return null;
		}
	}
}
