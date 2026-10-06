using UnityEngine;

namespace AK.Services.Analytics
{
	/// <summary>
	/// AppsFlyer credentials. Lives in Resources (like the GameAnalytics / Mixpanel settings)
	/// so the provider can start at the top of boot, before DI or session context exist.
	/// Asset: Assets/Resources/AppsFlyer/AppsFlyerSettings.asset.
	/// </summary>
	public sealed class AppsFlyerSettings : ScriptableObject
	{
		public const string ResourcePath = "AppsFlyer/AppsFlyerSettings";

		[Tooltip("AppsFlyer dashboard → App Settings → Dev Key. Shared by the Android and iOS apps of one account.")]
		public string DevKey;

		[Tooltip("Numeric Apple App ID from App Store Connect, without the 'id' prefix. iOS only.")]
		public string AppleAppId;

		[Tooltip("iOS: seconds the native SDK holds the install event while the ATT prompt is on screen. 0 sends immediately.")]
		[Range(0, 120)]
		public int AttTimeoutSeconds = 60;

		[Tooltip("Verbose native SDK logging in release builds. Development builds always log.")]
		public bool DebugLogging;

		public static AppsFlyerSettings Load()
		{
			return Resources.Load<AppsFlyerSettings>(ResourcePath);
		}
	}
}
