using System;

namespace AK.CoreDomain.Achievements
{
	/// <summary>
	/// Types of achievements based on completion criteria
	/// </summary>
	public enum AchievementType
	{
		/// <summary>
		/// Achievement based on reaching a specific level
		/// </summary>
		LevelBased,
		
		/// <summary>
		/// Achievement based on accumulating a specific amount (coins, XP, etc.)
		/// </summary>
		Accumulation,
		
		/// <summary>
		/// Achievement based on completing a specific number of actions
		/// </summary>
		CountBased,
		
		/// <summary>
		/// Achievement based on completing a specific level
		/// </summary>
		LevelComplete,
		
		/// <summary>
		/// Achievement based on earning stars on levels
		/// </summary>
		StarBased,
		
		/// <summary>
		/// Achievement based on streak (consecutive days, wins, etc.)
		/// </summary>
		Streak,
		
		/// <summary>
		/// Achievement based on time (play time, completion time, etc.)
		/// </summary>
		TimeBased,
		
		/// <summary>
		/// Achievement based on social actions (friends, sharing, etc.)
		/// </summary>
		Social,
		
		/// <summary>
		/// Achievement based on collection (collecting items, themes, etc.)
		/// </summary>
		Collection,
		
		/// <summary>
		/// Achievement based on special events or conditions
		/// </summary>
		Special,
		
		/// <summary>
		/// Achievement based on using specific powerups or features
		/// </summary>
		FeatureUsage,
		
		/// <summary>
		/// Achievement based on completing challenges
		/// </summary>
		ChallengeComplete,
		
		/// <summary>
		/// Achievement based on spending (coins, powerups, etc.)
		/// </summary>
		Spending,
		
		/// <summary>
		/// Achievement based on winning or losing streaks
		/// </summary>
		WinStreak,
		
		/// <summary>
		/// Achievement based on custom criteria defined in code
		/// </summary>
		Custom
	}
}