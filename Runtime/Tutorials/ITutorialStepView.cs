using Cysharp.Threading.Tasks;

namespace AK.Tutorials
{
	/// <summary>
	/// Finish contract for views shown as tutorial steps (e.g. choreographed tutorial
	/// fragments). The fragment presenter awaits it to complete the step.
	/// </summary>
	public interface ITutorialStepView
	{
		UniTask WaitUntilFinish();
	}
}
