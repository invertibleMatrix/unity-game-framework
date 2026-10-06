using System;
using System.Collections.Generic;
using System.Threading;
using AK.CoreDomain.Facts;
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
	/// How the tutorial runner holds input around a step, and how a run stops. A step with
	/// BlockInputUntilPresented is covered from its start until it releases the hold or ends,
	/// and the gate ends a hold its step never gets to release. An abandoned run gives input
	/// back at once, cancels its step and records nothing, and the next run starts afresh.
	/// </summary>
	public class TutorialRunnerTests
	{
		private readonly List<Object> _created = new();
		private UISystemHarness _h;
		private StubFactService _facts;

		private UIInputGate Gate => _h.System.InputGate;

		[SetUp]
		public void SetUp()
		{
			_h = new UISystemHarness();
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

		/// <summary>A step the test drives: each presentation waits for the test to bring it up, then to finish it.</summary>
		private sealed class ScriptedStep : TutorialStep
		{
			public bool ReleasesHoldWhenUp  = true;
			public bool HonoursCancellation = true;

			public readonly List<Presentation> Presentations = new();

			public Presentation Current => Presentations[Presentations.Count - 1];

			public override async UniTask PresentAsync(TutorialStepContext context, CancellationToken ct)
			{
				var presentation = new Presentation(context, ct);
				Presentations.Add(presentation);

				await Until(presentation.Up, ct);
				if (ReleasesHoldWhenUp) context.InputHold.Release();
				await Until(presentation.Done, ct);
			}

			private UniTask Until(UniTaskCompletionSource signal, CancellationToken ct) =>
				HonoursCancellation ? signal.Task.AttachExternalCancellation(ct) : signal.Task;
		}

		private sealed class Presentation
		{
			public readonly TutorialStepContext     Context;
			public readonly CancellationToken       Token;
			public readonly UniTaskCompletionSource Up   = new();
			public readonly UniTaskCompletionSource Done = new();

			public Presentation(TutorialStepContext context, CancellationToken token)
			{
				Context = context;
				Token = token;
			}

			/// <summary>The presentation is up: a step that governs input itself releases its hold.</summary>
			public void BringUp() => Up.TrySetResult();

			/// <summary>The player completes the step.</summary>
			public void Finish()
			{
				Up.TrySetResult();
				Done.TrySetResult();
			}
		}

		private ScriptedStep MakeStep(string name, bool blocksInput = true)
		{
			var step = ScriptableObject.CreateInstance<ScriptedStep>();
			step.name = name;
			step.BlockInputUntilPresented = blocksInput;
			_created.Add(step);
			return step;
		}

		private TutorialProvider MakeProvider(params TutorialStep[] steps)
		{
			var progress = ScriptableObject.CreateInstance<FactType>();
			_created.Add(progress);

			var provider = ScriptableObject.CreateInstance<TutorialProvider>();
			provider.name = "Intro";
			provider.Steps = new List<TutorialStep>(steps);
			provider.ProgressFact = progress;
			_created.Add(provider);

			provider.Init(_facts, _h.System, null);
			return provider;
		}

		/// <summary>Advances the gate's clock frame by frame, in steps it takes whole.</summary>
		private void Advance(float seconds)
		{
			while (seconds > 0f)
			{
				float step = Math.Min(seconds, UIInputGate.MaxFrameGapSeconds);
				Gate.Tick(step);
				seconds -= step;
			}
		}

		private static void Complete(UniTask task) => UISystemHarness.Complete(task);

		// ---------------------------------------------------------------
		// The step's hold
		// ---------------------------------------------------------------

		[Test]
		public void BlockingStep_HoldsInput_FromItsStart_UntilItsPresentationIsUp()
		{
			ScriptedStep step = MakeStep("Tap");
			TutorialProvider provider = MakeProvider(step);

			UniTask run = provider.RunDueAsync();

			Assert.That(Gate.IsHeld, Is.True, "held from the step's start");
			Assert.That(step.Current.Context.InputHold.IsHeld, Is.True, "the step is handed the runner's hold");
			Assert.That(Gate.DescribeHolders(), Is.EqualTo("Tutorial 'Intro', step 'Tap'"));

			step.Current.BringUp();

			Assert.That(Gate.IsHeld, Is.False, "the step released it once its presentation was up");
			Assert.That(run.Status.IsCompleted(), Is.False, "the step is still waiting for the player");

			step.Current.Finish();
			Complete(run);

			Assert.That(_facts.Recorded, Is.EqualTo(1));
		}

		[Test]
		public void BlockingStep_ThatNeverReleases_HasItsHoldReleased_WhenItEnds()
		{
			ScriptedStep step = MakeStep("Tap");
			step.ReleasesHoldWhenUp = false;
			TutorialProvider provider = MakeProvider(step);

			UniTask run = provider.RunDueAsync();
			step.Current.BringUp();

			Assert.That(Gate.IsHeld, Is.True);

			step.Current.Finish();
			Complete(run);

			Assert.That(Gate.IsHeld, Is.False);
			Assert.That(_facts.Recorded, Is.EqualTo(1));
		}

		[Test]
		public void NonBlockingStep_PresentsWithoutAHold()
		{
			ScriptedStep step = MakeStep("Free", blocksInput: false);
			TutorialProvider provider = MakeProvider(step);

			UniTask run = provider.RunDueAsync();

			Assert.That(Gate.IsHeld, Is.False);
			Assert.That(step.Current.Context.InputHold.IsSet, Is.False, "the step is handed no hold");

			step.Current.Finish();
			Complete(run);
		}

		[Test]
		public void EachStep_IsHeldFromItsOwnStart()
		{
			ScriptedStep first  = MakeStep("First");
			ScriptedStep second = MakeStep("Second");
			TutorialProvider provider = MakeProvider(first, second);

			UniTask run = provider.RunDueAsync();
			InputHold firstHold = first.Current.Context.InputHold;
			first.Current.Finish();

			InputHold secondHold = second.Current.Context.InputHold;
			Assert.That(firstHold.IsHeld, Is.False);
			Assert.That(secondHold.IsHeld, Is.True, "the next step's start is covered by a hold of its own");
			Assert.That(Gate.DescribeHolders(), Is.EqualTo("Tutorial 'Intro', step 'Second'"));

			second.Current.Finish();
			Complete(run);

			Assert.That(Gate.IsHeld, Is.False);
			Assert.That(_facts.Recorded, Is.EqualTo(2));
		}

		[TestCase(0f, 0f)]
		[TestCase(2f, 2f)]
		[TestCase(-5f, 0f, Description = "a negative start delay counts as none")]
		[TestCase(float.NaN, 0f, Description = "so does one that isn't a number, as in the step's wait")]
		public void StepHold_TimesOut_AfterTheStartDelay_PlusTheAllowance(float startDelay, float countedDelay)
		{
			ScriptedStep step = MakeStep("Tap");
			step.ReleasesHoldWhenUp = false;
			step.StartDelay = startDelay;
			TutorialProvider provider = MakeProvider(step);
			float timeout = countedDelay + TutorialProvider.InputHoldAllowanceSeconds;

			UniTask run = provider.RunDueAsync();
			Advance(timeout - UIInputGate.MaxFrameGapSeconds);

			Assert.That(Gate.IsHeld, Is.True);

			using (ExpectedLog.Warning("Tutorial 'Intro', step 'Tap'' timed out"))
			{
				Advance(UIInputGate.MaxFrameGapSeconds);
			}

			Assert.That(Gate.IsHeld, Is.False, "the gate gave input back");

			step.Current.Finish();
			Complete(run);

			Assert.That(_facts.Recorded, Is.EqualTo(1), "the step itself carried on");
		}

		// ---------------------------------------------------------------
		// Stopping a run
		// ---------------------------------------------------------------

		[Test]
		public void AbandonRun_MidStep_GivesInputBack_CancelsTheStep_AndRecordsNothing()
		{
			ScriptedStep step = MakeStep("Tap");
			TutorialProvider provider = MakeProvider(step);

			UniTask run = provider.RunDueAsync();
			Presentation abandoned = step.Current;

			provider.AbandonRun();

			Assert.That(Gate.IsHeld, Is.False, "input comes back at once");
			Assert.That(abandoned.Token.IsCancellationRequested, Is.True, "the step is told to stop");
			Complete(run);
			Assert.That(_facts.Recorded, Is.EqualTo(0));

			Assert.That(provider.HasDueSteps, Is.True, "the next checkpoint presents the step again");
			UniTask next = provider.RunDueAsync();

			Assert.That(step.Presentations.Count, Is.EqualTo(2));
			Assert.That(Gate.IsHeld, Is.True);

			step.Current.Finish();
			Complete(next);

			Assert.That(_facts.Recorded, Is.EqualTo(1));
			Assert.That(Gate.IsHeld, Is.False);
		}

		[Test]
		public void AbandonedStep_ThatFinishesAnyway_RecordsNothing_AndLeavesTheNextRunAlone()
		{
			ScriptedStep step = MakeStep("Tap");
			step.HonoursCancellation = false;
			TutorialProvider provider = MakeProvider(step);

			UniTask abandonedRun = provider.RunDueAsync();
			Presentation abandoned = step.Current;

			provider.AbandonRun();

			Assert.That(Gate.IsHeld, Is.False, "input comes back although the step ignores its cancellation");
			Assert.That(abandonedRun.Status.IsCompleted(), Is.False);

			UniTask next = provider.RunDueAsync();
			Presentation current = step.Current;

			Assert.That(current, Is.Not.SameAs(abandoned), "a fresh run starts without waiting for the old one");
			Assert.That(Gate.IsHeld, Is.True);

			abandoned.Finish();
			Complete(abandonedRun);

			Assert.That(_facts.Recorded, Is.EqualTo(0), "the abandoned step's finish records nothing");
			Assert.That(current.Context.InputHold.IsHeld, Is.True, "nor does it end the next run's hold");
			Assert.That(provider.HasDueSteps, Is.False, "the next run is still in flight");

			current.Finish();
			Complete(next);

			Assert.That(_facts.Recorded, Is.EqualTo(1));
			Assert.That(Gate.IsHeld, Is.False);
		}

		[Test]
		public void Init_AbandonsTheRunInFlight()
		{
			ScriptedStep step = MakeStep("Tap");
			TutorialProvider provider = MakeProvider(step);

			UniTask run = provider.RunDueAsync();
			Presentation abandoned = step.Current;

			provider.Init(_facts, _h.System, null);

			Assert.That(Gate.IsHeld, Is.False);
			Assert.That(abandoned.Token.IsCancellationRequested, Is.True);
			Complete(run);
			Assert.That(_facts.Recorded, Is.EqualTo(0));
		}

		[Test]
		public void CallerCancellation_Throws_ReleasesInput_AndRecordsNothing()
		{
			ScriptedStep step = MakeStep("Tap");
			TutorialProvider provider = MakeProvider(step);
			using var cts = new CancellationTokenSource();

			UniTask run = provider.RunDueAsync(cts.Token);
			cts.Cancel();

			Assert.That(Gate.IsHeld, Is.False);
			Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Canceled), "the caller's cancellation is not swallowed");
			Assert.Catch<OperationCanceledException>(() => run.GetAwaiter().GetResult());
			Assert.That(_facts.Recorded, Is.EqualTo(0));
			Assert.That(provider.HasDueSteps, Is.True, "the provider is free for the next checkpoint");
		}

		[Test]
		public void RunDue_WhileARunIsInFlight_DoesNothing()
		{
			ScriptedStep step = MakeStep("Tap");
			TutorialProvider provider = MakeProvider(step);

			UniTask run = provider.RunDueAsync();
			Complete(provider.RunDueAsync());

			Assert.That(step.Presentations.Count, Is.EqualTo(1));
			Assert.That(Gate.HoldCount, Is.EqualTo(1));

			step.Current.Finish();
			Complete(run);
		}

		// ---------------------------------------------------------------
		// A missing step
		// ---------------------------------------------------------------

		[Test]
		public void NullStep_CountsAsDone_SoTheNextStepRunsOnce()
		{
			ScriptedStep step = MakeStep("Tap");
			TutorialProvider provider = MakeProvider(null, step);

			Assert.That(provider.HasDueSteps, Is.True, "a missing current step doesn't stall the tutorial");

			UniTask run;
			using (ExpectedLog.Warning("null step at index 0"))
			{
				run = provider.RunDueAsync();
			}

			step.Current.Finish();
			Complete(run);

			Assert.That(_facts.Recorded, Is.EqualTo(2), "the missing step and the one after it");
			Assert.That(provider.IsComplete, Is.True);

			Complete(provider.RunDueAsync());
			Assert.That(step.Presentations.Count, Is.EqualTo(1), "the next run doesn't show the step again");
		}

		[Test]
		public void AbandonRun_WithNoRunInFlight_DoesNothing()
		{
			ScriptedStep step = MakeStep("Tap");
			TutorialProvider provider = MakeProvider(step);

			provider.AbandonRun();
			provider.AbandonRun();

			UniTask run = provider.RunDueAsync();
			Assert.That(Gate.IsHeld, Is.True);

			step.Current.Finish();
			Complete(run);

			Assert.That(_facts.Recorded, Is.EqualTo(1));
		}
	}
}
