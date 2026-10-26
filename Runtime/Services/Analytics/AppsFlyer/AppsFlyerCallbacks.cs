using System;
using System.Collections.Generic;
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

			Dictionary<string, object> data;
			try
			{
				data = AppsFlyer.CallbackStringToDictionary(conversionData);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[AppsFlyer] Conversion data unreadable: {e.Message}");
				return;
			}

			if (data != null)
			{
				InstallAttribution.Report(ToInstallSource(data));
			}
		}

		/// <summary>
		/// AppsFlyer's conversion keys. Ad networks differ on the ad set and ad keys: Meta sends
		/// adset and ad, most others af_adset and af_ad.
		/// </summary>
		public static InstallSource ToInstallSource(IReadOnlyDictionary<string, object> data)
		{
			bool organic = string.Equals(Value(data, "af_status"), "Organic", StringComparison.OrdinalIgnoreCase);
			return new InstallSource(
				organic,
				Value(data, "media_source"),
				Value(data, "campaign"),
				Value(data, "af_adset") ?? Value(data, "adset"),
				Value(data, "af_ad") ?? Value(data, "ad"));
		}

		private static string Value(IReadOnlyDictionary<string, object> data, string key)
		{
			if (!data.TryGetValue(key, out object value) || value == null)
			{
				return null;
			}

			string text = value.ToString();
			return string.IsNullOrEmpty(text) || text == "null" ? null : text;
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
