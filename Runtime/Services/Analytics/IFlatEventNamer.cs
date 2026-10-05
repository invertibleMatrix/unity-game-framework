using System.Collections.Generic;

namespace AK.Services.Analytics
{
	/// <summary>
	/// Names design events for providers with flat event names, such as Mixpanel and Meta. A
	/// GameAnalytics id carries values in its parts ("level:start:w2"); a flat name should not,
	/// or each value becomes its own event, so a namer picks a name for the kind of event and
	/// moves the values into properties.
	/// </summary>
	public interface IFlatEventNamer
	{
		/// <summary>
		/// Names the design event whose id has <paramref name="parts"/>, and may add to
		/// <paramref name="properties"/>, which already hold the event's parameters, its
		/// GameAnalytics id under "ga_event_id" and its value under "value". Returns false to
		/// leave the event to the default name, its parts joined with '_'.
		/// </summary>
		bool TryName(DesignEventParts parts, Dictionary<string, object> properties, out string eventName);
	}
}
