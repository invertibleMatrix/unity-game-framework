using AK.Core;
using AK.Examples.UI;
using AK.Systems;
using Reflex.Attributes;
using Sirenix.OdinInspector;
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

			_uiSystem.Show<ExampleView>();
		}

		[Button]
		public void ShowToast(string text = "")
		{
			_uiSystem.DisplayToast(text);
		}

		[Button]
		public void ShowBanner1(string text = "")
		{
			_uiSystem.DisplayBanner(text, "banner1");
		}

		[Button]
		public void ShowBanner2(string text = "")
		{
			_uiSystem.DisplayBanner(text, "banner2");
		}
		
		[Button]
		public void ShowBanner3(string text = "")
		{
			_uiSystem.DisplayBanner(text, UIViewBanner.AFFIRMATION_ID);
		}
	}
}