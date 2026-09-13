using AK.Systems.UI;

namespace AK.Systems
{
	/// <summary>
	/// Toasts and banners are ordinary pooled views; these helpers only spare callers the
	/// <c>onInit</c> boilerplate. Kept off <see cref="IUISystem"/> because the system has no
	/// business knowing about two specific prefabs.
	/// </summary>
	public static class UINotifications
	{
		public static void DisplayToast(this IUISystem ui, string text)
		{
			ui.Show<UIViewToast>(onInit: toast => toast.Init(0, text));
		}

		public static void DisplayBanner(this IUISystem ui, string text, string variantId = "")
		{
			ui.Show<UIViewBanner>(ShowOptions.Variant(variantId), banner => banner.Init(text));
		}
	}
}
