using System;

namespace AK.Core
{
	public interface IAppStateMachine
	{
		public event Action<AppState> OnStateChange;

		public AppState CurrentState  { get; }
		public AppState PreviousState { get; }
		public void     ChangeState(AppState appState, bool pauseCurrent = false, TransitionContext context = null);
		public void     TryGoBack();
	}
}