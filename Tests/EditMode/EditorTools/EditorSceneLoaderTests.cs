using AK.Editor;
using NUnit.Framework;
using UnityEditor;

namespace AK.Tests.EditorTools
{
	public class EditorSceneLoaderTests
	{
		[Test]
		public void FirstScenePath_IsTheFirstEnabledScene()
		{
			var scenes = new[]
			{
				new EditorBuildSettingsScene("Assets/Off.unity", false),
				new EditorBuildSettingsScene("Assets/Boot.unity", true),
				new EditorBuildSettingsScene("Assets/Game.unity", true),
			};

			Assert.AreEqual("Assets/Boot.unity", EditorSceneLoader.FirstScenePath(scenes));
		}

		[Test]
		public void FirstScenePath_SkipsAnEntryWithoutAPath()
		{
			var scenes = new[]
			{
				new EditorBuildSettingsScene(string.Empty, true),
				new EditorBuildSettingsScene("Assets/Boot.unity", true),
			};

			Assert.AreEqual("Assets/Boot.unity", EditorSceneLoader.FirstScenePath(scenes));
		}

		[Test]
		public void FirstScenePath_IsNull_WithoutAnEnabledScene()
		{
			Assert.IsNull(EditorSceneLoader.FirstScenePath(new EditorBuildSettingsScene[0]));
			Assert.IsNull(EditorSceneLoader.FirstScenePath(new[] { new EditorBuildSettingsScene("Assets/Off.unity", false) }));
		}

		[Test]
		public void ATestRun_IsSeen()
		{
			// This test runs in one, which the loader leaves alone.
			Assert.IsTrue(EditorSceneLoader.IsTestRunActive());
		}
	}
}
