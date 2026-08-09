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

		public abstract UniTask PresentAsync(TutorialStepContext context, CancellationToken ct);
	}
}
