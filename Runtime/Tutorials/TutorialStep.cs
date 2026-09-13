using System.Collections.Generic;
using System.Threading;
using AK.CoreDomain.Facts;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// One step of a tutorial: the conditions that must be met before it presents,
	/// plus its presentation. The base assumes nothing about presentation — subclass
	/// assets carry whatever data their presentation needs (SpotlightTooltipStep,
	/// FragmentStep, or game-specific subclasses).
	/// </summary>
	public abstract class TutorialStep : ScriptableObject
	{
		[Tooltip("Facts that must have occurred before this step can present.")]
		public List<FactCondition> Conditions = new();
		
		[Tooltip("Seconds to wait after this step's conditions are met before it presents.")]
		public float StartDelay;

		[Tooltip("When true (default), the step freezes all UI input at the top of PresentAsync — before StartDelay — and holds until the step's presentation goes live. Set false for steps where an intermediate UI must stay interactive between conditions-met and presentation.")]
		public bool BlockInputUntilPresented = true;

		public virtual async UniTask PresentAsync(TutorialStepContext context, CancellationToken ct)
		{
			if (BlockInputUntilPresented)
			{
				context.InputGate.Hold();
			}

			if (StartDelay > 0f)
			{
				await UniTask.WaitForSeconds(StartDelay, cancellationToken: ct);
			}
		}
	}
}
