using System.Collections;
using UnityEngine;

namespace AK.Core
{
	public interface IAppStateMachine
	{
		public void ChangeState(AppState appState, bool pauseCurrent = false, TransitionContext context = null);
		public void Restart();
	}
}