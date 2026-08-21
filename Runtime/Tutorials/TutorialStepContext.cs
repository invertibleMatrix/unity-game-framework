using AK.Services.Facts;
using AK.Systems;

namespace AK.Tutorials
{
	/// <summary>
	/// Services a tutorial step can use while presenting. Passed by the runner -
	/// steps stay stateless, nothing is injected into assets.
	/// </summary>
	public readonly struct TutorialStepContext
	{
		public readonly IUISystem         UiSystem;
		public readonly IUITargetRegistry Targets;
		public readonly IFactService      Facts;

		/// <summary>
		/// Held by the runner from step kick-in, so no click slips into the gap
		/// before the step's presentation appears. A step releases it once its
		/// presentation governs input itself (spotlight shown, view in tutorial
		/// mode) - the tutored control becomes clickable then. The runner's
		/// finally-Release is the safety net so input can never strand.
		/// </summary>
		public readonly UIInputGate       InputGate;

		public TutorialStepContext(IUISystem uiSystem, IUITargetRegistry targets, IFactService facts, UIInputGate inputGate)
		{
			UiSystem = uiSystem;
			Targets = targets;
			Facts = facts;
			InputGate = inputGate;
		}
	}
}
