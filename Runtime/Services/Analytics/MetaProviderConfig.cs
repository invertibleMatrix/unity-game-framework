using System.Collections.Generic;

namespace AK.Services.Analytics
{
	/// <summary>
	/// Provider-config keys read by the Meta provider (AK.Services.Meta). They live here so game
	/// code can set them through <see cref="AnalyticsInitOptions.ProviderConfig"/> without
	/// referencing the Facebook SDK assembly.
	/// </summary>
	public static class MetaProviderConfig
	{
		/// <summary>
		/// "true" limits the Facebook SDK to install / app-open reporting and drops in-app events.
		/// Meta does not de-duplicate in-app events, so once AppsFlyer postbacks carry them to
		/// Meta the SDK must not send them as well or Ads Manager counts every event twice.
		/// </summary>
		public const string InstallsOnlyKey = "meta.installsOnly";

		public static bool IsInstallsOnly(IReadOnlyDictionary<string, string> config)
		{
			return config != null
				&& config.TryGetValue(InstallsOnlyKey, out string value)
				&& bool.TryParse(value, out bool installsOnly)
				&& installsOnly;
		}
	}
}
