using AppsFlyerSDK;
using UnityEngine;

namespace AK.Services.Analytics
{
	/// <summary>
	/// Persistent host for the native → C# callbacks. The plugin delivers conversion data
	/// and app-open attribution via UnitySendMessage to the GameObject passed to initSDK,
	/// so the object name must stay stable for the whole session.
	/// </summary>
	[AddComponentMenu("")]
	public sealed class AppsFlyerCallbacks : MonoBehaviour, IAppsFlyerConversionData
	{
		public const string HostObjectName = "AppsFlyer";

		private static AppsFlyerCallbacks _instance;

		public static AppsFlyerCallbacks Ensure()
		{
			if (_instance != null)
			{
				return _instance;
			}

			var host = new GameObject(HostObjectName);
			DontDestroyOnLoad(host);
			_instance = host.AddComponent<AppsFlyerCallbacks>();
			return _instance;
		}

		// af_status (Organic / Non-organic), media_source, campaign, is_first_launch — the
		// quickest way to confirm a test install attributed the way the dashboard says.
		public void onConversionDataSuccess(string conversionData)
		{
			Debug.Log($"[AppsFlyer] Conversion data: {conversionData}");
		}

		public void onConversionDataFail(string error)
		{
			Debug.LogWarning($"[AppsFlyer] Conversion data failed: {error}");
		}

		public void onAppOpenAttribution(string attributionData)
		{
			Debug.Log($"[AppsFlyer] App open attribution: {attributionData}");
		}

		public void onAppOpenAttributionFailure(string error)
		{
			Debug.LogWarning($"[AppsFlyer] App open attribution failed: {error}");
		}
	}
}
