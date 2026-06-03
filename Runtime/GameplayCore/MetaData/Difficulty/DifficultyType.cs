using System;

namespace AK.CoreDomain.Difficulty
{
	/// <summary>
	/// Defines the type of difficulty for categorization and filtering.
	/// </summary>
	[Serializable]
	public enum DifficultyType
	{
		/// <summary>
		/// Tutorial difficulty - very easy, for teaching mechanics.
		/// </summary>
		Tutorial,
		
		/// <summary>
		/// Very easy difficulty - for beginners.
		/// </summary>
		VeryEasy,
		
		/// <summary>
		/// Easy difficulty - for casual players.
		/// </summary>
		Easy,
		
		/// <summary>
		/// Normal difficulty - standard gameplay.
		/// </summary>
		Normal,
		
		/// <summary>
		/// Hard difficulty - for experienced players.
		/// </summary>
		Hard,
		
		/// <summary>
		/// Very hard difficulty - for expert players.
		/// </summary>
		VeryHard,
		
		/// <summary>
		/// Expert difficulty - for master players.
		/// </summary>
		Expert,
		
		/// <summary>
		/// Master difficulty - for the most skilled players.
		/// </summary>
		Master,
		
		/// <summary>
		/// Insane difficulty - extremely challenging.
		/// </summary>
		Insane,
		
		/// <summary>
		/// Custom difficulty - for game-specific needs.
		/// </summary>
		Custom
	}
}