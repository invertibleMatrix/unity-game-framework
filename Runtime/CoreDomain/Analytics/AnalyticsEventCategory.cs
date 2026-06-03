namespace AK.CoreDomain.Analytics
{
	/// <summary>
	/// Defines the category of an analytics event.
	/// </summary>
	public enum AnalyticsEventCategory
	{
		None,
		Gameplay,
		Monetization,
		Engagement,
		Progression,
		Social,
		Tutorial,
		Error,
		Custom,
		LevelStarted,
		LevelFailed,
		LevelCompleted,
		BoosterUsed,
		PowerupUsed,
		BoosterPurchased,
		PowerupPurchased,
		OpenedGachaBox,
		IAP,
		InterstitialAd,
		RewardedAd,
	}
}