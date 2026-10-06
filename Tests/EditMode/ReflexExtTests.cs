using System;
using System.Collections.Generic;
using System.Reflection;
using AK.Core;
using AK.Core.Extensions;
using NUnit.Framework;
using Reflex.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests
{
	public class ReflexExtTests
	{
		private static readonly FieldInfo InjectedContainerField =
			typeof(AppStateMachine).GetField("_container", BindingFlags.Instance | BindingFlags.NonPublic);

		private readonly List<Object> _objects = new();

		private Container _container;

		[SetUp]
		public void SetUp()
		{
			_container = new ContainerBuilder().SetName(nameof(ReflexExtTests)).Build();
		}

		[TearDown]
		public void TearDown()
		{
			foreach (Object o in _objects)
			{
				if (o != null) Object.DestroyImmediate(o);
			}

			_objects.Clear();
			_container.Dispose();
		}

		// An AppStateMachine stands in for any component with [Inject] members: it injects the
		// container itself, which needs no registration.
		private static Container InjectedContainer(AppStateMachine component) => (Container)InjectedContainerField.GetValue(component);

		// A root with an injectable component, and an inactive child with another.
		private GameObject Hierarchy(out AppStateMachine root, out AppStateMachine child)
		{
			var gameObject = new GameObject("root");
			_objects.Add(gameObject);
			root = gameObject.AddComponent<AppStateMachine>();

			var childObject = new GameObject("child");
			childObject.transform.SetParent(gameObject.transform);
			childObject.SetActive(false);
			child = childObject.AddComponent<AppStateMachine>();

			return gameObject;
		}

		[Test]
		public void Instantiate_AGameObject_InjectsEveryComponentOfTheCopy()
		{
			GameObject original = Hierarchy(out AppStateMachine originalRoot, out _);

			GameObject copy = _container.Instantiate(original);
			_objects.Add(copy);

			AppStateMachine[] copied = copy.GetComponentsInChildren<AppStateMachine>(true);
			Assert.AreEqual(2, copied.Length);
			Assert.AreSame(_container, InjectedContainer(copied[0]));
			Assert.AreSame(_container, InjectedContainer(copied[1]), "inactive children too");
			Assert.IsNull(InjectedContainer(originalRoot), "the original is left alone");
		}

		[Test]
		public void Instantiate_AComponent_InjectsTheWholeCopiedHierarchy()
		{
			Hierarchy(out AppStateMachine originalRoot, out _);

			AppStateMachine copy = _container.Instantiate(originalRoot);
			_objects.Add(copy.gameObject);

			Assert.AreNotSame(originalRoot, copy);
			Assert.AreSame(_container, InjectedContainer(copy));
			AppStateMachine copiedChild = copy.transform.GetChild(0).GetComponent<AppStateMachine>();
			Assert.AreSame(_container, InjectedContainer(copiedChild));
		}

		[Test]
		public void Inject_AGameObjectSeenAsAnObject_IsRefused()
		{
			var gameObject = new GameObject("refused");
			_objects.Add(gameObject);
			object seenAsAnObject = gameObject;

			Assert.Throws<ArgumentException>(() => seenAsAnObject.Inject(_container));
		}

		[Test]
		public void Inject_AComponent_InjectsIt()
		{
			Hierarchy(out AppStateMachine root, out AppStateMachine child);

			root.Inject(_container);

			Assert.AreSame(_container, InjectedContainer(root));
			Assert.IsNull(InjectedContainer(child), "only the object itself");
		}
	}
}
