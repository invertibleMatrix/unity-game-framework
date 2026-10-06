namespace AK.Core
{
	/// <summary>Data handed to a state as it is entered. Subclass it to carry some.</summary>
	public class TransitionContext
	{
		/// <summary>The context of a state entered without one. It carries nothing, so one instance serves every state.</summary>
		internal static readonly TransitionContext Empty = new();
	}
}
