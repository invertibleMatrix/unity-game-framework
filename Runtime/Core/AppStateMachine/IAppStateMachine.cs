using System;

namespace AK.Core
{
	/// <summary>The game's top-level state machine. See <see cref="AppStateMachine"/>.</summary>
	public interface IAppStateMachine
	{
		/// <summary>Fires after a state is entered, the boot state included. A resumed state doesn't fire it.</summary>
		public event Action<AppState> OnStateChange;

		public AppState CurrentState  { get; }

		/// <summary>The state that was current before the latest transition: exited, or paused under the current one.</summary>
		public AppState PreviousState { get; }

		/// <summary>
		/// Exits the current state, or pauses it with <paramref name="pauseCurrent"/>, and enters
		/// <paramref name="appState"/>, or resumes it if it is paused. A resumed state keeps its
		/// context unless <paramref name="context"/> is given. Requested while a transition runs,
		/// the change runs once that transition has been reported.
		/// </summary>
		public void     ChangeState(AppState appState, bool pauseCurrent = false, TransitionContext context = null);

		/// <summary>
		/// Exits the current state and resumes the one paused last. Nothing happens when no state
		/// is paused. Requested while a transition runs, it takes the top of the pause stack when
		/// its turn comes.
		/// </summary>
		public void     TryGoBack();
	}
}
