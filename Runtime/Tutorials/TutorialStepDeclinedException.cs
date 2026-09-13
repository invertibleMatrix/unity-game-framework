using System;

namespace AK.Tutorials
{
	/// <summary>
	/// Thrown by a step whose presentation surface never materialized (e.g. its
	/// UITarget did not register within the bounded wait). The runner treats it as
	/// "not now": the progress fact is not recorded, so the step stays due and the
	/// next checkpoint retries once the surface exists.
	/// </summary>
	public sealed class TutorialStepDeclinedException : Exception
	{
		public TutorialStepDeclinedException(string message) : base(message) { }
	}
}
