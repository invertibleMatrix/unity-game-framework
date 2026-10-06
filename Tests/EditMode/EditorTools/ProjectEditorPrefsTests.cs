using System;
using AK.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AK.Tests.EditorTools
{
	// Runs against the real EditorPrefs, in scopes and under a legacy key of its own that each
	// test deletes.
	public class ProjectEditorPrefsTests
	{
		private const string Name = "ProjectEditorPrefsTests.Flag";

		private string _scope;
		private string _otherScope;
		private string _legacyKey;

		[SetUp]
		public void SetUp()
		{
			string id = Guid.NewGuid().ToString("N");
			_scope = "Test" + id;
			_otherScope = "OtherTest" + id;
			_legacyKey = "AK.Tests.ProjectEditorPrefsTests." + id;
		}

		[TearDown]
		public void TearDown()
		{
			EditorPrefs.DeleteKey(ProjectEditorPrefs.Key(Name, _scope));
			EditorPrefs.DeleteKey(ProjectEditorPrefs.Key(Name, _otherScope));
			EditorPrefs.DeleteKey(_legacyKey);
		}

		[Test]
		public void Key_PutsTheScopeBetweenTheRootAndTheName()
		{
			Assert.AreEqual("UGFW.abc.Tool.Flag", ProjectEditorPrefs.Key("Tool.Flag", "abc"));
		}

		[Test]
		public void TheScope_ComesFromTheProjectsAssetsFolder()
		{
			Assert.AreEqual(ProjectEditorPrefs.ScopeOf(Application.dataPath), ProjectEditorPrefs.ProjectScope);
			Assert.AreEqual(ProjectEditorPrefs.ScopeOf("D:/Games/A/Assets"), ProjectEditorPrefs.ScopeOf("D:/Games/A/Assets"));
			Assert.AreNotEqual(ProjectEditorPrefs.ScopeOf("D:/Games/A/Assets"), ProjectEditorPrefs.ScopeOf("D:/Games/B/Assets"));
		}

		[Test]
		public void AValue_IsSeenOnlyInItsScope()
		{
			Assert.IsTrue(ProjectEditorPrefs.GetBool(Name, true, _scope), "the fallback while unset");

			ProjectEditorPrefs.SetBool(Name, true, _scope);

			Assert.IsTrue(ProjectEditorPrefs.GetBool(Name, false, _scope));
			Assert.IsFalse(ProjectEditorPrefs.GetBool(Name, false, _otherScope));
			Assert.IsFalse(EditorPrefs.HasKey(ProjectEditorPrefs.Key(Name, _otherScope)));
		}

		[Test]
		public void AdoptLegacyBool_CopiesTheOldValue_AndKeepsIt()
		{
			EditorPrefs.SetBool(_legacyKey, true);

			ProjectEditorPrefs.AdoptLegacyBool(_legacyKey, Name, _scope);

			Assert.IsTrue(ProjectEditorPrefs.GetBool(Name, false, _scope));
			Assert.IsTrue(EditorPrefs.HasKey(_legacyKey), "other projects adopt it too");
		}

		[Test]
		public void AdoptLegacyBool_KeepsTheScopesOwnValue()
		{
			EditorPrefs.SetBool(_legacyKey, true);
			ProjectEditorPrefs.SetBool(Name, false, _scope);

			ProjectEditorPrefs.AdoptLegacyBool(_legacyKey, Name, _scope);

			Assert.IsFalse(ProjectEditorPrefs.GetBool(Name, true, _scope));
		}

		[Test]
		public void AdoptLegacyBool_WithoutAnOldValue_LeavesTheScopeUnset()
		{
			ProjectEditorPrefs.AdoptLegacyBool(_legacyKey, Name, _scope);

			Assert.IsFalse(EditorPrefs.HasKey(ProjectEditorPrefs.Key(Name, _scope)));
		}
	}
}
