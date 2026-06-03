using AK.Core;
using AK.Systems;
using Reflex.Attributes;
using UnityEngine;

namespace AK.Examples
{
	[CreateAssetMenu(fileName = "MainMenuState", menuName = "AK/Examples/App/MainMenuState")]
	public class MainMenuState : AppState<MainMenuStateContext>
	{
		[Inject] private IUISystem _uiSystem;

		public override void OnEnter()
		{
			_uiSystem.DisplayToast("Entered Main Menu State");
		}
	}
}