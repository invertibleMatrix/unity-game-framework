using System;
using GameAnalyticsSDK;
using UnityEngine;

namespace AK.Services.Analytics
{
	/// <summary>
	/// iOS/tvOS App Tracking Transparency prompt. Request as early as possible (before MAX
	/// initializes) so ad requests can use IDFA from the first session. The GA provider
	/// re-requests at init — ATTrackingManager then returns the determined status
	/// immediately without re-showing the dialog, and GA initializes on any status
	/// (IDFV fallback), per GA docs.
	/// </summary>
	public static class GameAnalyticsAtt
	{
		private sealed class EarlyListener : IGameAnalyticsATTListener
		{
			public void GameAnalyticsATTListenerNotDetermined() { }
			public void GameAnalyticsATTListenerRestricted() { }
			public void GameAnalyticsATTListenerDenied() { }
			public void GameAnalyticsATTListenerAuthorized() { }
		}

		public static void RequestEarly()
		{
			if (Application.platform != RuntimePlatform.IPhonePlayer && Application.platform != RuntimePlatform.tvOS)
			{
				return;
			}

			try
			{
				// If the user is still staring at this prompt when GameAnalyticsProvider
				// initializes, the provider's request replaces this listener and receives
				// the answer — GA's ATT client keeps a single listener slot.
				GameAnalytics.RequestTrackingAuthorization(new EarlyListener());
			}
			catch (Exception ex)
			{
				Debug.LogError($"[GameAnalyticsAtt] Early ATT request failed: {ex.Message}");
			}
		}
	}
}
