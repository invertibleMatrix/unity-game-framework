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
	/// MetaDataAsset base: UID identity, so references can be GUID links resolved
	/// through the provider registry instead of hard asset references.
	/// </summary>
	[CreateAssetMenu(fileName = "TutorialProvider", menuName = "AK/Tutorials/Tutorial Provider")]
	public class TutorialProvider : MetaDataAsset
	{
		[Tooltip("Ordered steps of this tutorial.")]
		public List<TutorialStep> Steps = new();

		[Tooltip("This tutorial's progress counter: recorded once per completed step — its count is the furthest completed step.")]
		public FactType ProgressFact;

		[Tooltip("Optional kill-switch: the tutorial runs only while this remote bool evaluates true. Empty means always enabled.")]
		public RemoteBool EnabledGate;

		private IFactService        _facts;
		private TutorialStepContext _stepContext;
		private UIInputGate         _inputGate;
		private bool                _isRunning;

		public void Init(IFactService facts, IUISystem uiSystem, IUITargetRegistry targets)
		{
			_facts = facts;
			_inputGate = new UIInputGate();
			_stepContext = new TutorialStepContext(uiSystem, targets, facts, _inputGate);
			// Assets outlive play sessions when domain reload is disabled — a step
			// that was mid-flight when Play stopped never runs its finally, so
			// _isRunning can arrive stale and permanently gate HasDueSteps.
			_isRunning = false;
		}

		public bool IsComplete => ProgressFact != null && _facts != null &&
		                          _facts.Count(ProgressFact) >= Steps.Count;

		/// <summary>True when the current step's conditions are met — this tutorial has work that can run right now.</summary>
		public bool HasDueSteps
		{
			get
			{
				if (_facts == null || _isRunning || IsComplete || Steps.Count == 0 || ProgressFact == null) return false;
				if (EnabledGate != null && !EnabledGate.Value) return false;

				var step = Steps[_facts.Count(ProgressFact)];
				return step != null && _facts.AreMet(step.Conditions);
			}
		}

		/// <summary>
		/// Checkpoint semantics: runs steps whose conditions are met and returns when
		/// the next step isn't due. The caller's chain re-evaluates at the next
		/// checkpoint — providers hold no long-lived waits.
		/// </summary>
		public async UniTask RunDueAsync(CancellationToken ct = default)
		{
			if (_isRunning || IsComplete) return;

			if (EnabledGate != null && !EnabledGate.Value) return;

			if (Steps.Count == 0 || ProgressFact == null)
			{
				Debug.LogError($"[TutorialProvider] '{name}' has no steps or no progress fact.", this);
				return;
			}

			_isRunning = true;

			try
			{
				int completedCount = _facts.Count(ProgressFact);

				for (int i = completedCount; i < Steps.Count; i++)
				{
					ct.ThrowIfCancellationRequested();

					var step = Steps[i];
					if (step == null)
					{
						Debug.LogWarning($"[TutorialProvider] '{name}' has a null step at index {i} — skipping.");
						continue;
					}

					if (!_facts.AreMet(step.Conditions))
					{
						return;
					}

					try
					{
						await step.PresentAsync(_stepContext, ct);
					}
					catch (TutorialStepDeclinedException)
					{
						// The step's surface never appeared — leave the progress counter
						// untouched so the next checkpoint retries instead of killing
						// the tutorial.
						return;
					}
					finally
					{
						_inputGate.Release();
					}

					_facts.Record(ProgressFact);
				}
			}
			finally
			{
				_isRunning = false;
			}
		}
	}
}
