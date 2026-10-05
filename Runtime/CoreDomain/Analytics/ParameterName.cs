namespace AK.CoreDomain.Analytics
{
	/// <summary>
	/// Common parameter names for <see cref="AnalyticsParameter"/> and the typed TrackEvent
	/// overloads. Providers send each under its snake_case name ("ItemId" is "item_id"); a
	/// parameter the list lacks uses <see cref="AnalyticsParameter.Key"/>. Assets store the
	/// numbers, so they never change, and a removed name leaves its number unused.
	/// </summary>
	public enum ParameterName
	{
		None            = 0,
		Platform        = 1,
		DeviceModel     = 2,
		LevelNumber     = 3,
		FailReason      = 4,
		// 5 was ActiveStars, a game's own parameter.
		Duration        = 6,
		SessionDuration = 7,
		// 8 was PowerupId.
		Attempts        = 9,
		// 10 was PowerupName.
		Name            = 11,
		// 12 was EarnedStars and 13 StartIntention.
		CurrencyCode    = 14,
		Amount          = 15,
		ItemType        = 16,
		ItemId          = 17
	}
}
