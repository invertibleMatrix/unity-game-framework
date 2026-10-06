using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain.Facts;
using AK.CoreDomain.RemoteConfig;
using AK.Services.Facts;
using AK.Systems;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// One tutorial: an ordered list of TutorialStep assets plus the progress fact the
	/// runner mutates. The runner is checkpoint-driven — at each checkpoint it runs
	/// only the steps whose conditions are met and returns; the next checkpoint
	/// resumes from the progress count, so tutorials resume correctly after a
	/// restart and never hold long-lived waits. Holds no presentation logic.
	/// MetaDataAsset base: carries an identity, so references can be Uid links resolved
	/// through the provider registry instead of hard asset references.
	///
	/// One run at a time. For a step with BlockInputUntilPresented, the runner holds the
	/// UISystem's input gate from the step's start until the step releases it or ends.
	/// <see cref="AbandonRun"/> stops a run for good: it cancels the step, releases its
	/// hold, and the run records no further progress.
	/// </summary>
	[CreateAssetMenu(fileName = "TutorialProvider", menuName = "AK/Tutorials/Tutorial Provider")]
	public class TutorialProvider : MetaDataAsset
	{
		/// <summary>
		/// The longest a step may hold input beyond its StartDelay before the gate ends the
		/// hold itself, with a warning. A presentation goes live within a frame or two once its
		/// target is registered, or after waiting a few seconds for one that is about to be
		/// (steps wait up to 3 s per target); this only catches a step that never gets to release.
		/// </summary>
		public const float InputHoldAllowanceSeconds = 10f;

		[Tooltip("Ordered steps of this tutorial.")]
		public List<TutorialStep> Steps = new();

		[Tooltip("This tutorial's progress counter: recorded once per completed step — its count is the furthest completed step.")]
		public FactType ProgressFact;

		[Tooltip("Optional kill-switch: the tutorial runs only while this remote bool evaluates true. Empty means always enabled.")]
		public RemoteBool EnabledGate;

		private IFactService            _facts;
		private TutorialStepContext     _stepContext;
		private UIInputGate             _inputGate;
		private CancellationTokenSource _run;
		private InputHold               _stepHold;

		/// <summary>
		/// Binds the tutorial to its services. A run still in flight is abandoned first: it
		/// belongs to an earlier binding, such as a pass dropped without
		/// <see cref="AbandonRun"/>, or a Play session that stopped mid-step (assets outlive
		/// Play sessions when domain reload is off, and that step's finally never ran).
		/// </summary>
		public void Init(IFactService facts, IUISystem uiSystem, IUITargetRegistry targets)
		{
			AbandonRun();

			_facts = facts;
			_inputGate = uiSystem?.InputGate;
			_stepContext = new TutorialStepContext(uiSystem, targets, facts);
		}

		/// <summary>
		/// Abandons the run in flight, if any: the step's input hold is released at once, its
		/// awaits are cancelled (so it closes what it showed), and nothing the run does later
		/// records progress. The next checkpoint starts a fresh run from the progress count.
		/// Does nothing when no run is in flight. A host calls this when the surface the run
		/// presents on goes away, as EL's board does on reconnect.
		/// </summary>
		public void AbandonRun()
		{
			CancellationTokenSource run = _run;
			_run = null;

			_stepHold.Release();
			_stepHold = default;

			if (run == null) return;

			// The run unwinds and disposes its own source, often inside this call. Abandoning
			// must not fail, whatever the step's cleanup does.
			try
			{
				run.Cancel();
			}
			catch (Exception e)
			{
				Debug.LogException(e, this);
			}
		}

		public bool IsComplete => ProgressFact != null && _facts != null &&
		                          _facts.Count(ProgressFact) >= Steps.Count;

		/// <summary>
		/// True when the current step's conditions are met and its target is registered —
		/// this tutorial has work that can run right now. A step whose surface is not open
		/// is not due; the surface that registers the target requests a checkpoint when it
		/// appears, and that checkpoint presents the step. A missing step is due: the run
		/// skips it.
		/// </summary>
		public bool HasDueSteps
		{
			get
			{
				if (_facts == null || _run != null || IsComplete || Steps.Count == 0 || ProgressFact == null) return false;
				if (EnabledGate != null && !EnabledGate.Value) return false;

				var step = Steps[_facts.Count(ProgressFact)];
				return step == null || (_facts.AreMet(step.Conditions) && step.IsTargetPresent(_stepContext));
			}
		}

		/// <summary>
		/// Checkpoint semantics: runs steps whose conditions are met and returns when
		/// the next step isn't due. The caller's chain re-evaluates at the next
		/// checkpoint — providers hold no long-lived waits. Does nothing while a run is
		/// already in flight. A run that is abandoned returns quietly; cancelling
		/// <paramref name="ct"/> throws as usual.
		/// </summary>
		public async UniTask RunDueAsync(CancellationToken ct = default)
		{
			if (_run != null || IsComplete) return;

			if (EnabledGate != null && !EnabledGate.Value) return;

			if (Steps.Count == 0 || ProgressFact == null)
			{
				Debug.LogError($"[TutorialProvider] '{name}' has no steps or no progress fact.", this);
				return;
			}

			CancellationTokenSource run = CancellationTokenSource.CreateLinkedTokenSource(ct);
			CancellationToken runToken = run.Token;
			_run = run;

			try
			{
				int completedCount = _facts.Count(ProgressFact);

				for (int i = completedCount; i < Steps.Count; i++)
				{
					ct.ThrowIfCancellationRequested();

					var step = Steps[i];
					if (step == null)
					{
						// Counted as done, so the progress count keeps pointing at the step that runs next.
						Debug.LogWarning($"[TutorialProvider] '{name}' has a null step at index {i} — skipping.", this);
						_facts.Record(ProgressFact);
						continue;
					}

					if (!_facts.AreMet(step.Conditions) || !step.IsTargetPresent(_stepContext))
					{
						return;
					}

					InputHold hold = HoldInputFor(step);
					_stepHold = hold;

					try
					{
						await step.PresentAsync(_stepContext.WithInputHold(hold), runToken);
					}
					catch (TutorialStepDeclinedException)
					{
						// The step's surface never appeared — leave the progress counter
						// untouched so the next checkpoint retries instead of killing
						// the tutorial.
						return;
					}
					catch (OperationCanceledException) when (run.IsCancellationRequested && !ct.IsCancellationRequested)
					{
						// Abandoned: the step is cancelled and the run is over.
						return;
					}
					finally
					{
						hold.Release();
						if (_run == run) _stepHold = default;
					}

					// A run abandoned while its step finished records nothing; the next run
					// presents the step again.
					if (run.IsCancellationRequested)
					{
						ct.ThrowIfCancellationRequested();
						return;
					}

					_facts.Record(ProgressFact);
				}
			}
			finally
			{
				if (_run == run) _run = null;
				run.Dispose();
			}
		}

		private InputHold HoldInputFor(TutorialStep step)
		{
			if (!step.BlockInputUntilPresented || _inputGate == null) return default;

			// Counted as the step's wait counts it: a delay that isn't above zero is none.
			float startDelay = step.StartDelay > 0f ? step.StartDelay : 0f;
			return _inputGate.Hold($"Tutorial '{name}', step '{step.name}'", startDelay + InputHoldAllowanceSeconds);
		}
	}
}
