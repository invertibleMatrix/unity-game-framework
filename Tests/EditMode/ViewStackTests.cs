using System.Collections.Generic;
using AK.Systems;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests
{
	public class ViewStackTests
	{
		private sealed class TestView : UIView { }

		private readonly List<GameObject> _created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var go in _created)
			{
				if (go != null) Object.DestroyImmediate(go);
			}

			_created.Clear();
		}

		private TestView MakeView(string name, ViewStackBehaviour behaviour = ViewStackBehaviour.DoNothing)
		{
			var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
			go.SetActive(false);
			_created.Add(go);

			var view = go.AddComponent<TestView>();
			var so = new SerializedObject(view);
			so.FindProperty("_stackBehaviour").enumValueIndex = (int)behaviour;
			so.ApplyModifiedPropertiesWithoutUndo();
			return view;
		}

		[Test]
		public void PushPopPeek_BehaveLikeAStack()
		{
			var stack = new ViewStack();
			var a = MakeView("A");
			var b = MakeView("B");

			Assert.That(stack.Count, Is.EqualTo(0));
			Assert.That(stack.PeekOrNull(), Is.Null);

			stack.Push(a);
			stack.Push(b);

			Assert.That(stack.Count, Is.EqualTo(2));
			Assert.That(stack.Peek(), Is.SameAs(b));
			Assert.That(stack.PeekBelowTopOrNull(), Is.SameAs(a));
			Assert.That(stack.Pop(), Is.SameAs(b));
			Assert.That(stack.Peek(), Is.SameAs(a));
			Assert.That(stack.PeekBelowTopOrNull(), Is.Null);
		}

		[Test]
		public void Enumerator_YieldsTopFirst_LikeSystemStack()
		{
			var stack = new ViewStack();
			var a = MakeView("A");
			var b = MakeView("B");
			var c = MakeView("C");
			stack.Push(a);
			stack.Push(b);
			stack.Push(c);

			var seen = new List<UIView>();
			foreach (var v in stack) seen.Add(v);

			Assert.That(seen, Is.EqualTo(new UIView[] { c, b, a }));

			var copy = new List<UIView>();
			stack.CopyTopFirst(copy);
			Assert.That(copy, Is.EqualTo(seen));
		}

		[Test]
		public void Remove_MidStack_PreservesOrderOfTheRest()
		{
			var stack = new ViewStack();
			var a = MakeView("A");
			var b = MakeView("B");
			var c = MakeView("C");
			stack.Push(a);
			stack.Push(b);
			stack.Push(c);

			Assert.That(stack.Remove(b), Is.True);
			Assert.That(stack.Remove(b), Is.False);
			Assert.That(stack.Count, Is.EqualTo(2));
			Assert.That(stack[0], Is.SameAs(a));
			Assert.That(stack[1], Is.SameAs(c));
			Assert.That(stack.Contains(b), Is.False);
		}

		[Test]
		public void MoveToTop_BringsAnExistingViewToTheTop_OrPushesANewOne()
		{
			var stack = new ViewStack();
			var a = MakeView("A");
			var b = MakeView("B");
			var c = MakeView("C");
			stack.Push(a);
			stack.Push(b);
			stack.Push(c);

			stack.MoveToTop(a);
			Assert.That(stack.Count, Is.EqualTo(3));
			Assert.That(stack.Peek(), Is.SameAs(a));
			Assert.That(stack[0], Is.SameAs(b));

			var d = MakeView("D");
			stack.MoveToTop(d);
			Assert.That(stack.Count, Is.EqualTo(4));
			Assert.That(stack.Peek(), Is.SameAs(d));
		}

		[Test]
		public void BelowOrNull_ReturnsTheViewBeneath_OrNullAtBottom()
		{
			var stack = new ViewStack();
			var a = MakeView("A");
			var b = MakeView("B");
			var c = MakeView("C");
			stack.Push(a);
			stack.Push(b);
			stack.Push(c);

			Assert.That(stack.BelowOrNull(c), Is.SameAs(b));
			Assert.That(stack.BelowOrNull(b), Is.SameAs(a));
			Assert.That(stack.BelowOrNull(a), Is.Null);
			Assert.That(stack.BelowOrNull(MakeView("X")), Is.Null);
		}

		[Test]
		public void IsCoveredAbove_DetectsHidingViewsAboveTheTarget_Only()
		{
			var stack = new ViewStack();
			var start = MakeView("Start");
			var shop  = MakeView("Shop", ViewStackBehaviour.HideBelow);
			var toast = MakeView("Toast");
			stack.Push(start);
			stack.Push(shop);
			stack.Push(toast);

			Assert.That(stack.IsCoveredAbove(start), Is.True, "Shop hides below, so Start is covered");
			Assert.That(stack.IsCoveredAbove(shop), Is.False, "Toast is DoNothing");
			Assert.That(stack.IsCoveredAbove(toast), Is.False, "top of stack");

			stack.Remove(shop);
			Assert.That(stack.IsCoveredAbove(start), Is.False, "after Shop leaves, only Toast remains above");

			var popup = MakeView("Popup", ViewStackBehaviour.PauseOnlyBelow);
			stack.Push(popup);
			Assert.That(stack.IsCoveredAbove(start), Is.True, "PauseOnlyBelow also counts as covering");
		}

		[Test]
		public void Operations_DoNotAllocate_OnceWarm()
		{
			var stack = new ViewStack();
			var views = new UIView[8];
			for (int i = 0; i < views.Length; i++) views[i] = MakeView("V" + i);
			foreach (var v in views) stack.Push(v);

			for (int i = 0; i < 4; i++) Exercise(stack, views);

			int allocations = GcAllocations.Count(() =>
			{
				for (int i = 0; i < 100; i++) Exercise(stack, views);
			});

			Assert.That(allocations, Is.EqualTo(0), "stack operations must not allocate");
		}

		private static void Exercise(ViewStack stack, UIView[] views)
		{
			stack.Remove(views[3]);
			stack.MoveToTop(views[3]);
			stack.BelowOrNull(views[5]);
			stack.IsCoveredAbove(views[1]);
			stack.Contains(views[0]);
			int n = 0;
			foreach (var v in stack) n += v != null ? 1 : 0;
			stack.MoveToTop(views[0]);
		}
	}
}
