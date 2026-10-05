using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain.Facts;
using AK.Systems;
using AK.Tutorials;
using AK.Tests.Support;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AK.Tests
{
	/// <summary>
	/// Target-gated tutorial steps: a step whose UITargetId is not registered is not due,
	/// and becomes due once the target registers — no step ever waits for its surface, and
	/// no checkpoint pass parks on one that is closed. (The surface that registers a target
	/// requests the checkpoint; that wiring lives in the game's views, not here.)
	/// </summary>
	public class TutorialTargetGatingTests
	{
		private UITargetRegistry _registry;
		private StubFactService _facts;
		private readonly List<UnityEngine.Object> _created = new();

		[SetUp]
		public void SetUp()
		{
			_registry = new UITargetRegistry();
			_facts = new StubFactService();
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var o in _created)
				if (o != null) UnityEngine.Object.DestroyImmediate(o);
			_created.Clear();
		}

		private static T MakeId<T>(string hex) where T : UID
		{
			var id = ScriptableObject.CreateInstance<T>();
			var so = new UnityEditor.SerializedObject(id);
			so.FindProperty("_id._value").stringValue = hex;
			so.ApplyModifiedPropertiesWithoutUndo();
			return id;
		}

		private RectTransform MakeRect()
		{
			var go = new GameObject("Target", typeof(RectTransform));
			_created.Add(go);
			return go.GetComponent<RectTransform>();
		}

		/// <summary>A target on its own root canvas.</summary>
		private RectTransform MakeTargetOnCanvas(int sortingOrder, RenderMode mode = RenderMode.ScreenSpaceOverlay)
		{
			var canvasGo = new GameObject("Canvas" + sortingOrder, typeof(RectTransform), typeof(Canvas));
			_created.Add(canvasGo);
			var canvas = canvasGo.GetComponent<Canvas>();
			canvas.renderMode = mode;
			canvas.sortingOrder = sortingOrder;

			if (mode == RenderMode.ScreenSpaceCamera)
			{
				// Without a camera, a camera canvas renders (and reports itself) as an overlay.
				var cameraGo = new GameObject("UICamera", typeof(Camera));
				_created.Add(cameraGo);
				canvas.worldCamera = cameraGo.GetComponent<Camera>();
			}

			var target = new GameObject("Target", typeof(RectTransform));
			target.transform.SetParent(canvasGo.transform, false);
			return (RectTransform)target.transform;
		}

		private RectTransform Get(UITargetId id) => _registry.TryGet(id, out var target) ? target : null;

		/// <summary>Mirrors the game's mode steps: gates on a UITargetId and resolves it on present.</summary>
		private sealed class GatedStep : TutorialStep
		{
			public UITargetId TargetId;

			public override bool IsTargetPresent(TutorialStepContext context) => IsTargetRegistered(context, TargetId);

			public override UniTask PresentAsync(TutorialStepContext context, CancellationToken ct)
			{
				ResolveRegisteredTarget(context, TargetId);
				return UniTask.CompletedTask;
			}
		}

		private GatedStep MakeStep(UITargetId id)
		{
			var step = ScriptableObject.CreateInstance<GatedStep>();
			step.TargetId = id;
			_created.Add(step);
			return step;
		}

		private TutorialProvider MakeProvider(TutorialStep step, FactType progress)
		{
			var provider = ScriptableObject.CreateInstance<TutorialProvider>();
			provider.Steps = new List<TutorialStep> { step };
			var so = new UnityEditor.SerializedObject(provider);
			so.FindProperty("ProgressFact").objectReferenceValue = progress;
			so.ApplyModifiedPropertiesWithoutUndo();
			_created.Add(provider);
			provider.Init(_facts, null, _registry);
			return provider;
		}

		[Test]
		public void Step_WithNoTargetId_ReportsPresent_SoPresentCanWarnAndSkip()
		{
			// A misconfigured step must reach PresentAsync (which logs and skips) rather
			// than sit "not due" forever with nothing in the log.
			GatedStep step = MakeStep(null);
			var context = new TutorialStepContext(null, _registry, _facts);

			Assert.That(step.IsTargetPresent(context), Is.True);
		}

		[Test]
		public void Step_IsTargetPresent_FollowsRegistration()
		{
			UITargetId id = MakeId<UITargetId>("22222222222222222222222222222222");
			GatedStep step = MakeStep(id);

			var context = new TutorialStepContext(null, _registry, _facts);

			Assert.That(step.IsTargetPresent(context), Is.False, "not present before registration");

			RectTransform rect = MakeRect();
			_registry.Register(id, rect);

			Assert.That(step.IsTargetPresent(context), Is.True, "present after registration");

			_registry.Unregister(id, rect);

			Assert.That(step.IsTargetPresent(context), Is.False, "gone after unregister");
		}

		[Test]
		public void Step_ResolvingAnAbsentTarget_Declines()
		{
			UITargetId id = MakeId<UITargetId>("33333333333333333333333333333333");
			GatedStep step = MakeStep(id);
			var context = new TutorialStepContext(null, _registry, _facts);

			Assert.Throws<TutorialStepDeclinedException>(() => step.PresentAsync(context, CancellationToken.None),
				"an absent target at present-time declines instead of waiting");
		}

		[Test]
		public void Registry_WithSeveralInstances_ReturnsTheTopmost()
		{
			UITargetId id = MakeId<UITargetId>("88888888888888888888888888888888");
			RectTransform low = MakeTargetOnCanvas(5);
			RectTransform high = MakeTargetOnCanvas(10);
			RectTransform underCamera = MakeTargetOnCanvas(50, RenderMode.ScreenSpaceCamera);

			_registry.Register(id, high);
			_registry.Register(id, low);
			_registry.Register(id, underCamera);

			Assert.That(Get(id), Is.SameAs(high), "overlay canvases draw over camera canvases, then the higher order wins");

			high.gameObject.SetActive(false);
			Assert.That(Get(id), Is.SameAs(low), "an inactive instance loses to an active one");

			_registry.Unregister(id, low);
			Assert.That(Get(id), Is.SameAs(underCamera), "unregistering one instance leaves the others");

			_registry.Unregister(id, underCamera);
			Assert.That(Get(id), Is.SameAs(high), "with no active instance left, an inactive one still answers");
		}

		[Test]
		public void Registry_SortsANestedCanvasWithTheCanvasItDrawsWith()
		{
			UITargetId id = MakeId<UITargetId>("99999999999999999999999999999999");
			RectTransform middle = MakeTargetOnCanvas(10);
			RectTransform nestedHost = MakeTargetOnCanvas(1);
			var nested = nestedHost.gameObject.AddComponent<Canvas>();

			_registry.Register(id, nestedHost);
			_registry.Register(id, middle);

			Assert.That(Get(id), Is.SameAs(middle), "a nested canvas that doesn't override sorting draws with its root (1)");

			// A nested canvas keeps an order only while it overrides sorting.
			nested.overrideSorting = true;
			nested.sortingOrder = 100;
			Assert.That(Get(id), Is.SameAs(nestedHost), "overriding, it draws at its own order (100)");

			nested.overrideSorting = false;
			Assert.That(Get(id), Is.SameAs(middle), "no longer overriding, it draws with its root again");
		}

		[Test]
		public void Registry_GivesTiesToTheLatestRegistered_AndKeepsOneEntryPerInstance()
		{
			UITargetId id = MakeId<UITargetId>("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
			RectTransform a = MakeTargetOnCanvas(3);
			RectTransform b = MakeTargetOnCanvas(3);

			_registry.Register(id, a);
			_registry.Register(id, b);
			Assert.That(Get(id), Is.SameAs(b), "the view shown last wins between equals");

			_registry.Register(id, a);
			Assert.That(Get(id), Is.SameAs(b), "registering again keeps an instance where it was");

			_registry.Unregister(id, a);
			_registry.Unregister(id, b);
			Assert.That(_registry.TryGet(id, out _), Is.False, "one unregister removes an instance registered twice");
		}

		[Test]
		public void Registry_ForgetsDestroyedInstances()
		{
			UITargetId id = MakeId<UITargetId>("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
			RectTransform a = MakeTargetOnCanvas(1);
			RectTransform b = MakeTargetOnCanvas(2);
			_registry.Register(id, a);
			_registry.Register(id, b);

			UnityEngine.Object.DestroyImmediate(b.gameObject);
			Assert.That(Get(id), Is.SameAs(a));

			UnityEngine.Object.DestroyImmediate(a.gameObject);
			Assert.That(_registry.TryGet(id, out _), Is.False);
		}

		[Test]
		public void Provider_StepIsNotDue_WhileItsTargetIsUnregistered()
		{
			UITargetId id = MakeId<UITargetId>("44444444444444444444444444444444");
			GatedStep step = MakeStep(id);
			TutorialProvider provider = MakeProvider(step, MakeId<FactType>("55555555555555555555555555555555"));

			Assert.That(provider.HasDueSteps, Is.False, "facts are met, target is not");

			RectTransform rect = MakeRect();
			_registry.Register(id, rect);

			Assert.That(provider.HasDueSteps, Is.True, "registration is what makes the step due");

			_registry.Unregister(id, rect);

			Assert.That(provider.HasDueSteps, Is.False, "and unregistering makes it not due again");
		}

		[Test]
		public void Provider_RunDue_RecordsNothingWithoutATarget_AndProgressOnceRegistered()
		{
			UITargetId id = MakeId<UITargetId>("66666666666666666666666666666666");
			GatedStep step = MakeStep(id);
			TutorialProvider provider = MakeProvider(step, MakeId<FactType>("77777777777777777777777777777777"));

			UISystemHarness.Complete(provider.RunDueAsync());
			Assert.That(_facts.Recorded, Is.EqualTo(0), "an unregistered target records nothing");

			_registry.Register(id, MakeRect());

			Assert.That(provider.HasDueSteps, Is.True);
			UISystemHarness.Complete(provider.RunDueAsync());

			Assert.That(_facts.Recorded, Is.EqualTo(1), "progress recorded once the step ran");
			Assert.That(provider.IsComplete, Is.True);
		}
	}
}
