using System.Collections.Generic;
using AK.Systems;
using AK.Systems.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Finding = AK.Systems.Editor.UIViewRepositoryCleanup.Finding;
using Problem = AK.Systems.Editor.UIViewRepositoryCleanup.Problem;

namespace AK.Tests.EditorTools
{
	public class UIViewRepositoryCleanupTests
	{
		private sealed class Popup : UIView { }

		private readonly List<Object> _created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (Object created in _created)
			{
				if (created != null)
				{
					Object.DestroyImmediate(created);
				}
			}

			_created.Clear();
		}

		[Test]
		public void Find_ReportsMissingEmptyAndDuplicateEntries_InListOrder()
		{
			Popup a = NewView("A");
			Popup b = NewView("B");
			Popup deleted = NewView("Deleted");
			UIViewRepository repository = NewRepository(a, null, b, a, deleted, b);
			Object.DestroyImmediate(deleted.gameObject);

			var findings = new List<Finding>();
			UIViewRepositoryCleanup.Find(new SerializedObject(repository), findings);

			CollectionAssert.AreEqual(new[]
			{
				new Finding(1, Problem.Empty),
				new Finding(3, Problem.Duplicate, 0),
				new Finding(4, Problem.Missing),
				new Finding(5, Problem.Duplicate, 2),
			}, findings);
		}

		[Test]
		public void LookAlikes_AreNotDuplicates()
		{
			// A copy of a prefab is another prefab, here with its own view id.
			Popup original = NewView("Popup");
			Popup copy = NewView("Popup", viewId: "small");
			Popup sameId = NewView("Popup");

			var findings = new List<Finding>();
			UIViewRepositoryCleanup.Find(new SerializedObject(NewRepository(original, copy, sameId)), findings);

			Assert.IsEmpty(findings);
		}

		[Test]
		public void Remove_KeepsTheOtherEntries_InOrder()
		{
			Popup a = NewView("A");
			Popup b = NewView("B");
			Popup c = NewView("C");
			Popup deleted = NewView("Deleted");
			UIViewRepository repository = NewRepository(a, a, null, b, deleted, c, b);
			Object.DestroyImmediate(deleted.gameObject);

			var removed = new List<Finding>();
			UIViewRepositoryCleanup.Remove(new SerializedObject(repository), removed);

			CollectionAssert.AreEqual(new[] { 1, 2, 4, 6 }, removed.ConvertAll(finding => finding.Index));
			CollectionAssert.AreEqual(new UIView[] { a, b, c }, repository.Views);
		}

		[Test]
		public void Remove_LeavesACleanListAlone()
		{
			Popup a = NewView("A");
			Popup b = NewView("B");
			UIViewRepository repository = NewRepository(a, b);
			var serialized = new SerializedObject(repository);

			var removed = new List<Finding>();
			UIViewRepositoryCleanup.Remove(serialized, removed);

			Assert.IsEmpty(removed);
			Assert.IsFalse(serialized.hasModifiedProperties);
			CollectionAssert.AreEqual(new UIView[] { a, b }, repository.Views);
		}

		private Popup NewView(string name, string viewId = "")
		{
			var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
			_created.Add(go);

			var view = go.AddComponent<Popup>();
			var serialized = new SerializedObject(view);
			serialized.FindProperty("_viewId").stringValue = viewId;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return view;
		}

		private UIViewRepository NewRepository(params UIView[] views)
		{
			var repository = ScriptableObject.CreateInstance<UIViewRepository>();
			_created.Add(repository);

			var serialized = new SerializedObject(repository);
			SerializedProperty list = serialized.FindProperty(UIViewRepositoryCleanup.ViewsProperty);
			list.arraySize = views.Length;
			for (int i = 0; i < views.Length; i++)
			{
				list.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();
			return repository;
		}
	}
}
