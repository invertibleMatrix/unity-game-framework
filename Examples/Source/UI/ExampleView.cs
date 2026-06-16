using AK.Systems;
using UnityEngine;

namespace AK.Examples.UI
{
	public class ExampleView : UIView
	{
		[SerializeField] private ExampleButton _toastViewBtn;
		[SerializeField] private ExampleButton _bannerViewBtn1;
		[SerializeField] private ExampleButton _bannerViewBtn2;
		[SerializeField] private ExampleButton _bannerViewBtn3;

		public override void RegisterResources()
		{
			_toastViewBtn.Button.onClick.AddListener(ShowToast);
			_bannerViewBtn1.Button.onClick.AddListener(ShowBanner1);
			_bannerViewBtn2.Button.onClick.AddListener(ShowBanner2);
			_bannerViewBtn3.Button.onClick.AddListener(ShowBanner3);
		}

		public override void UnRegisterResources()
		{
			_toastViewBtn.Button.onClick.RemoveListener(ShowToast);
			_bannerViewBtn1.Button.onClick.RemoveListener(ShowBanner1);
			_bannerViewBtn2.Button.onClick.RemoveListener(ShowBanner2);
			_bannerViewBtn3.Button.onClick.RemoveListener(ShowBanner3);
		}

		private void ShowToast()
		{
			UISystem.DisplayToast("This is toast message");
		}

		private void ShowBanner1()
		{
			UISystem.DisplayBanner("This is Banner", UIViewBanner.DEFAULT_ID);
		}

		private void ShowBanner2()
		{
			UISystem.DisplayBanner("This is Another", UIViewBanner.DEFAULT_TOP_ID);
		}

		private void ShowBanner3()
		{
			UISystem.DisplayBanner("Another one", UIViewBanner.AFFIRMATION_ID);
		}
	}
}