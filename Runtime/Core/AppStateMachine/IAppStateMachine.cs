namespace AK.Core
{
	public interface IAppStateMachine
	{
		public AppState PreviousState { get; }
		public void ChangeState(AppState appState, bool pauseCurrent = false, TransitionContext context = null);
		public void TryGoBack();
	}
}
