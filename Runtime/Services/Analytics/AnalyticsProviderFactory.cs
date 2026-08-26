using System;
using AK.Services.Analytics.Providers;

namespace AK.Services.Analytics
{
	/// <summary>
	/// Optional SDK adapters register here from their own assemblies so
	/// <see cref="AK.Services.AnalyticsServiceBuilder"/> does not hard-reference vendor SDKs.
	/// </summary>
	public static class AnalyticsProviderFactory
	{
		public static Func<IAnalyticsProvider> GameAnalytics { get; set; }

		public static Func<IAnalyticsProvider> Mixpanel { get; set; }

		public static IAnalyticsProvider TryCreateGameAnalytics()
		{
			return GameAnalytics?.Invoke();
		}

		public static IAnalyticsProvider TryCreateMixpanel()
		{
			return Mixpanel?.Invoke();
		}
	}
}
