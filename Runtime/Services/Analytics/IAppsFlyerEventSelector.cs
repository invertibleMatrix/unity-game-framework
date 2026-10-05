using System.Collections.Generic;
using AK.CoreDomain.Analytics;

namespace AK.Services.Analytics
{
	/// <summary>
	/// Picks the design events a game sends to AppsFlyer. AppsFlyer is the attribution source,
	/// not the analytics warehouse, so it should get only the signals ad networks optimize on,
	/// under AppsFlyer's standard names (see <see cref="AppsFlyerEventMapper"/>) so partner
	/// postbacks map without setup. Purchases, rewarded views and ad revenue need no selector.
	/// </summary>
	public interface IAppsFlyerEventSelector
	{
		/// <summary>
		/// Returns true to send the design event <paramref name="evt"/>, whose id has
		/// <paramref name="parts"/>, as <paramref name="eventName"/> with
		/// <paramref name="values"/>. Returns false to drop it.
		/// </summary>
		bool TrySelect(
			AnalyticsEvent evt,
			DesignEventParts parts,
			out string eventName,
			out Dictionary<string, string> values);
	}
}
