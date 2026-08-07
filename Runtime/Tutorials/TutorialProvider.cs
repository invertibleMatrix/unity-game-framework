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
	/// runner mutates. The runner waits for each step's conditions, delegates
	/// presentation to the step itself, and records ProgressFact once per finished
	/// step — the count is the furthest completed step, so tutorials resume correctly
	/// after a restart. Holds no presentation logic.
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
		private bool                _isRunning;

		public void Init(IFactService facts, IUISystem uiSystem, IUITargetRegistry targets)
		{
			_facts = facts;
			_stepContext = new TutorialStepContext(uiSystem, targets, facts);
		}

		public bool IsComplete => ProgressFact != null && _facts != null &&
		                          _facts.Count(ProgressFact) >= Steps.Count;

		public async UniTask RunAsync(CancellationToken ct = default)
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

					await WaitForConditionsAsync(step.Conditions, ct);
					await step.PresentAsync(_stepContext, ct);

					_facts.Record(ProgressFact);
				}
			}
			finally
			{
				_isRunning = false;
			}
		}

		private async UniTask WaitForConditionsAsync(List<FactCondition> conditions, CancellationToken ct)
		{
			if (conditions == null || conditions.Count == 0 || _facts.AreMet(conditions)) return;

			var completion = new UniTaskCompletionSource();

			void Handler(FactType _)
			{
				if (_facts.AreMet(conditions))
				{
					completion.TrySetResult();
				}
			}

			_facts.Changed += Handler;

			try
			{
				// Re-check after subscribing to close the check/subscribe race.
				if (_facts.AreMet(conditions)) return;
				await completion.Task.AttachExternalCancellation(ct);
			}
			finally
			{
				_facts.Changed -= Handler;
			}
		}
	}
}