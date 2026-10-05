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
		/// The input hold the runner takes for a step with BlockInputUntilPresented, from the
		/// moment the step starts, so no tap slips in before its presentation appears; empty
		/// for other steps. A step releases it once its presentation governs input itself
		/// (spotlight shown, view in tutorial mode), which is when the tutored control becomes
		/// clickable. The runner releases it when the step ends anyway, and a second release
		/// does nothing, so input can't be left blocked.
		/// </summary>
		public readonly InputHold InputHold;

		public TutorialStepContext(IUISystem uiSystem, IUITargetRegistry targets, IFactService facts, InputHold inputHold = default)
		{
			UiSystem = uiSystem;
			Targets = targets;
			Facts = facts;
			InputHold = inputHold;
		}

		/// <summary>This context, for a step that presents under <paramref name="inputHold"/>.</summary>
		public TutorialStepContext WithInputHold(InputHold inputHold) => new(UiSystem, Targets, Facts, inputHold);
	}
}
