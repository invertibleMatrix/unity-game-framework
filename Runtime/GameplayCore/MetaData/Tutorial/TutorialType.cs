using System;

namespace GameplayCore.MetaData.Tutorial
{
	/// <summary>
	/// Defines the type of tutorial for categorization and filtering.
	/// </summary>
	[Serializable]
	public enum TutorialType
	{
		/// <summary>
		/// Tutorial shown when the player first starts the game.
		/// </summary>
		Onboarding,
		
		/// <summary>
		/// Tutorial for basic gameplay mechanics (shooting, aiming, etc.).
		/// </summary>
		GameplayBasics,
		
		/// <summary>
		/// Tutorial for powerup usage and mechanics.
		/// </summary>
		Powerup,
		
		/// <summary>
		/// Tutorial for booster usage and mechanics.
		/// </summary>
		Booster,
		
		/// <summary>
		/// Tutorial for special tile types and interactions.
		/// </summary>
		SpecialTiles,
		
		/// <summary>
		/// Tutorial for advanced strategies and techniques.
		/// </summary>
		Advanced,
		
		/// <summary>
		/// Tutorial for UI features and navigation.
		/// </summary>
		UI,
		
		/// <summary>
		/// Tutorial for store and IAP features.
		/// </summary>
		Store,
		
		/// <summary>
		/// Tutorial for event-specific features.
		/// </summary>
		Event,
		
		/// <summary>
		/// Custom tutorial type for game-specific needs.
		/// </summary>
		Custom
	}
}