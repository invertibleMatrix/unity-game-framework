namespace AK.CoreDomain.Analytics
{
	/// <summary>
	/// Defines the category of an analytics event. Definitions store the numbers, so they never
	/// change, and a removed category leaves its number unused.
	/// </summary>
	public enum AnalyticsEventCategory
	{
		None           = 0,
		Gameplay       = 1,
		Monetization   = 2,
		Engagement     = 3,
		Progression    = 4,
		Social         = 5,
		Tutorial       = 6,
		Error          = 7,
		Custom         = 8,
		LevelStarted   = 9,
		LevelFailed    = 10,
		LevelCompleted = 11,
		// 12 to 16 were a game's own events: BoosterUsed, PowerupUsed, BoosterPurchased,
		// PowerupPurchased and OpenedGachaBox.
		IAP            = 17,
		InterstitialAd = 18,
		RewardedAd     = 19,
		Ads            = 20,
		Session        = 21,
		Onboarding     = 22,
	}
}
