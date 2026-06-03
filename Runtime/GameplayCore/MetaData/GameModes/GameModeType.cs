using System;

namespace AK.CoreDomain.GameModes
{
	/// <summary>
	/// Defines the type of game mode for categorization and filtering.
	/// </summary>
	[Serializable]
	public enum GameModeType
	{
		/// <summary>
		/// Standard campaign mode with level progression.
		/// </summary>
		Campaign,
		
		/// <summary>
		/// Endless mode with increasing difficulty.
		/// </summary>
		Endless,
		
		/// <summary>
		/// Time-limited challenge mode.
		/// </summary>
		TimeAttack,
		
		/// <summary>
		/// Limited shots challenge mode.
		/// </summary>
		ShotLimit,
		
		/// <summary>
		/// Survival mode with limited lives.
		/// </summary>
		Survival,
		
		/// <summary>
		/// Puzzle mode with specific objectives.
		/// </summary>
		Puzzle,
		
		/// <summary>
		/// Multiplayer competitive mode.
		/// </summary>
		Versus,
		
		/// <summary>
		/// Cooperative multiplayer mode.
		/// </summary>
		Cooperative,
		
		/// <summary>
		/// Daily challenge mode.
		/// </summary>
		DailyChallenge,
		
		/// <summary>
		/// Special event mode.
		/// </summary>
		Event,
		
		/// <summary>
		/// Practice mode for learning.
		/// </summary>
		Practice,
		
		/// <summary>
		/// Custom game mode for game-specific needs.
		/// </summary>
		Custom
	}
}