using AK.Services.Facts;
using AK.Systems;

namespace AK.Tutorials
{
	/// <summary>
	/// Services a tutorial step can use while presenting. Passed by the runner —
	/// steps stay stateless, nothing is injected into assets.
	/// </summary>
	public readonly struct TutorialStepContext
	{
		public readonly IUISystem         UiSystem;
		public readonly IUITargetRegistry Targets;
		public readonly IFactService      Facts;

		public TutorialStepContext(IUISystem uiSystem, IUITargetRegistry targets, IFactService facts)
		{
			UiSystem = uiSystem;
			Targets = targets;
			Facts = facts;
		}
	}
}
