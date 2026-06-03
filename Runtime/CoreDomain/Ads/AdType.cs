namespace AK.CoreDomain.Ads
{
	/// <summary>
	/// Defines the type of ad.
	/// </summary>
	public enum AdType
	{
		/// <summary>
		/// Rewarded video ad - player watches to get rewards.
		/// </summary>
		Rewarded = 0,
		
		/// <summary>
		/// Interstitial ad - full-screen ad shown at natural breaks.
		/// </summary>
		Interstitial = 1,
		
		/// <summary>
		/// Banner ad - small ad displayed at screen edges.
		/// </summary>
		Banner = 2,
		
		/// <summary>
		/// App open ad - shown when the app launches.
		/// </summary>
		AppOpen = 3,
		
		/// <summary>
		/// Rewarded interstitial - full-screen ad with rewards.
		/// </summary>
		RewardedInterstitial = 4
	}
}