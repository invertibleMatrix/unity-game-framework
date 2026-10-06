using System;

namespace AK.Services
{
	/// <summary>
	/// Creates the store. The Unity IAP adapter (AK.Services.UnityIAP) is compiled only when
	/// com.unity.purchasing 5 is installed, and registers itself here, so games create the store
	/// without referencing it or guarding with #if.
	/// </summary>
	public static class IAPServiceFactory
	{
		/// <summary>Makes a Unity IAP service. Set by AK.Services.UnityIAP at startup; null without it.</summary>
		public static Func<IIAPService> Unity { get; set; }

		/// <summary>A new Unity IAP service, or a <see cref="NullIAPService"/> when the package isn't installed.</summary>
		public static IIAPService Create() => Unity?.Invoke() ?? new NullIAPService();
	}
}
