using AK.Core;
using UnityEngine;

namespace AK.Examples
{
	[CreateAssetMenu(fileName = "BootState",menuName = "AK/Examples/App/BootState")]
	public class BootState : AppState
	{
		[SerializeField] private AppState _mainMenuState;

		public override void OnEnter()
		{
			/*
			 * This is basically your entry point into the game. You might want to initialize runtime
			 * stuff here. Since your Bootstrap DI container has already been configured you can use [Inject]
			 * attribute
			 */
			AppStateMachine.ChangeState(_mainMenuState,false,new TransitionContext()
			{
				//Pass any data if needed
			});
		}
	}
}