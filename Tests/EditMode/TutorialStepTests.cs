using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.Systems;
using AK.Tests.Support;
using AK.Tutorials;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests
{
	/// <summary>
	/// Built-in steps on a real UI system. A step whose presentation can't go up declines, so
	/// the runner records no progress and the next checkpoint retries; a fragment step with a
	/// parent target is due only while that target is registered, and shows under its view.
	/// </summary>
	public class TutorialStepTests
	{
		private readonly List<Object> _created = new();
		private UISystemHarness  _h;
		private UITargetRegistry _targets;
		private StubFactService  _facts;

		/// <summary>Exposes the serialized settings a designer picks in the Inspector.</summary>
		private sealed class TestFragmentStep : FragmentStep
		{
			public void Configure(Type fragment, UITargetId parent = null, bool closeOnFinish = true)
			{
				FragmentType = new TypeRef(fragment);
				ParentTarget = parent;
				CloseOnFinish = closeOnFinish;
			}
		}

		[SetUp]
		public void SetUp()
		{
			_h = new UISystemHarness();
			_targets = new UITargetRegistry();
			_facts = new StubFactService();
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var o in _created)
				if (o != null) Object.DestroyImmediate(o);
			_created.Clear();
			_h.Dispose();
		}

		private TutorialStepContext Context => new(_h.System, _targets, _facts);

		private TestFragmentStep MakeFragmentStep(Type fragment, UITargetId parent = null, bool closeOnFinish = true)
		{
			var step = ScriptableObject.CreateInstance<TestFragmentStep>();
			step.name = "Fragment";
			step.Configure(fragment, parent, closeOnFinish);
			_created.Add(step);
			return step;
		}

		private UITargetId MakeId(string hex)
		{
			var id = ScriptableObject.CreateInstance<UITargetId>();
			var so = new UnityEditor.SerializedObject(id);
			so.FindProperty("_id._value").stringValue = hex;
			so.ApplyModifiedPropertiesWithoutUndo();
			_created.Add(id);
			return id;
		}

		/// <summary>A target inside <paramref name="view"/>, registered under <paramref name="id"/>.</summary>
		private RectTransform RegisterTargetIn(UIView view, UITargetId id)
		{
			var go = new GameObject("Anchor", typeof(RectTransform));
			go.transform.SetParent(view.transform, false);
			var rect = (RectTransform)go.transform;
			_targets.Register(id, rect);
			return rect;
		}

		private TutorialProvider MakeProvider(TutorialStep step)
		{
			var progress = ScriptableObject.CreateInstance<AK.CoreDomain.Facts.FactType>();
			_created.Add(progress);

			var provider = ScriptableObject.CreateInstance<TutorialProvider>();
			provider.name = "Intro";
			provider.Steps = new List<TutorialStep> { step };
			provider.ProgressFact = progress;
			_created.Add(provider);

			provider.Init(_facts, _h.System, _targets);
			return provider;
		}

		[Test]
		public void FragmentStep_WhoseFragmentCannotShow_Declines_AndRecordsNothing()
		{
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.System.Show<RecordingScreen>();
			TestFragmentStep step = MakeFragmentStep(typeof(RecordingFragment));
			TutorialProvider provider = MakeProvider(step);

			using (ExpectedLog.Error("RecordingFragment.*not found in UIViewRepository"))
			using (ExpectedLog.Warning("could not show 'RecordingFragment' — declining"))
			{
				UISystemHarness.Complete(provider.RunDueAsync());
			}

			Assert.That(_facts.Recorded, Is.EqualTo(0), "no progress: the next checkpoint retries");
			Assert.That(provider.HasDueSteps, Is.True);
		}

		[Test]
		public void FragmentStep_WithAParentTarget_IsDueOnlyWhileItIsRegistered()
		{
			UITargetId parentId = MakeId("cccccccccccccccccccccccccccccccc");
			TestFragmentStep step = MakeFragmentStep(typeof(RecordingFragment), parentId);

			Assert.That(step.IsTargetPresent(Context), Is.False, "the view it shows under isn't there");

			_h.MakePrefab<RecordingScreen>(screen: true);
			RectTransform anchor = RegisterTargetIn(_h.System.Show<RecordingScreen>(), parentId);
			Assert.That(step.IsTargetPresent(Context), Is.True);

			_targets.Unregister(parentId, anchor);
			Assert.That(step.IsTargetPresent(Context), Is.False);
		}

		[Test]
		public void FragmentStep_WhoseParentTargetWentAway_Declines_InsteadOfShowingWithoutIt()
		{
			UITargetId parentId = MakeId("dddddddddddddddddddddddddddddddd");
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingFragment>();
			_h.System.Show<RecordingScreen>();
			TestFragmentStep step = MakeFragmentStep(typeof(RecordingFragment), parentId);

			Assert.Throws<TutorialStepDeclinedException>(() => step.PresentAsync(Context, CancellationToken.None).GetAwaiter().GetResult());
			Assert.That(_h.System.GetView<RecordingFragment>(), Is.Null, "nothing was shown");
		}

		[Test]
		public void FragmentStep_ShowsItsFragmentUnderTheViewHoldingTheParentTarget()
		{
			UITargetId parentId = MakeId("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
			_h.MakePrefab<RecordingScreen>(screen: true);
			_h.MakePrefab<RecordingScreen>(viewId: "other", screen: true);
			_h.MakePrefab<RecordingFragment>();
			var host = _h.System.Show<RecordingScreen>();
			_h.System.Show<RecordingScreen>(ShowOptions.Variant("other"));
			RegisterTargetIn(host, parentId);
			TestFragmentStep step = MakeFragmentStep(typeof(RecordingFragment), parentId, closeOnFinish: false);

			using (ExpectedLog.Warning("does not implement ITutorialStepView"))
			{
				UISystemHarness.Complete(step.PresentAsync(Context, CancellationToken.None));
			}

			var fragment = _h.System.GetView<RecordingFragment>();
			Assert.That(fragment, Is.Not.Null);
			Assert.That(fragment.ParentView, Is.SameAs(host), "under the target's view, not the topmost screen");
		}
	}
}
