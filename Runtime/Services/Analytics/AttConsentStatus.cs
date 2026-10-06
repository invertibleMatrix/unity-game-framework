using System;

namespace AK.Services.Analytics
{
	/// <summary>
	/// Shared iOS App Tracking Transparency result. GameAnalytics owns the ATT prompt and
	/// reports the answer here so providers that must not reference the GA assembly (Meta)
	/// can still react. Null = the prompt has not been answered yet this session.
	/// </summary>
	public static class AttConsentStatus
	{
		public static bool? Authorized { get; private set; }

		/// <summary>Fires once per session, the first time the status becomes determined.</summary>
		public static event Action<bool> Resolved;

		public static void Report(bool authorized)
		{
			if (Authorized.HasValue)
			{
				return;
			}

			Authorized = authorized;
			Resolved?.Invoke(authorized);
		}
	}
}
