namespace GameplayCore.MetaData.DailyChallenges
{
	/// <summary>
	/// Types of daily challenges based on completion criteria
	/// </summary>
	public enum ChallengeType
	{
		/// <summary>
		/// Complete a specific number of levels
		/// </summary>
		LevelComplete,
		
		/// <summary>
		/// Earn a specific number of stars on levels
		/// </summary>
		StarEarn,
		
		/// <summary>
		/// Use a specific powerup a number of times
		/// </summary>
		PowerupUse,
		
		/// <summary>
		/// Pop a specific number of bubbles
		/// </summary>
		BubblePop,
		
		/// <summary>
		/// Complete levels within a time limit
		/// </summary>
		TimeLimit,
		
		/// <summary>
		/// Complete levels without losing lives
		/// </summary>
		NoLivesLost,
		
		/// <summary>
		/// Earn a specific amount of coins
		/// </summary>
		CoinEarn,
		
		/// <summary>
		/// Spend a specific amount of coins
		/// </summary>
		CoinSpend,
		
		/// <summary>
		/// Watch a specific number of ads
		/// </summary>
		AdWatch,
		
		/// <summary>
		/// Login on consecutive days
		/// </summary>
		DailyLogin,
		
		/// <summary>
		/// Complete levels with specific themes
		/// </summary>
		ThemeComplete,
		
		/// <summary>
		/// Use specific boosters
		/// </summary>
		BoosterUse,
		
		/// <summary>
		/// Complete a specific level
		/// </summary>
		SpecificLevel,
		
		/// <summary>
		/// Achieve a specific score
		/// </summary>
		ScoreAchieve,
		
		/// <summary>
		/// Complete challenges with custom criteria
		/// </summary>
		Custom
	}
}