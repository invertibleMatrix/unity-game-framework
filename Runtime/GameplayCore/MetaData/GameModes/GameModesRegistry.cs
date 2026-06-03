using System.Collections.Generic;
using System.Linq;
using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.GameModes
{
	/// <summary>
	/// Registry for all game mode definitions using UID-based lookup.
	/// Provides centralized management of game mode data.
	/// </summary>
	[CreateAssetMenu(fileName = "GameModesRegistry", menuName = "Gameplay/MetaData/GameModes/GameModesRegistry")]
	public class GameModesRegistry : TypedUIDRegistryAsset<GameModeDefinition>
	{
		/// <summary>
		/// Gets all game modes of a specific type.
		/// </summary>
		public List<GameModeDefinition> GetGameModesByType(GameModeType type)
		{
			return Registry.Objects.Where(m => m.Type == type).ToList();
		}
		
		/// <summary>
		/// Gets game modes available for a specific player level.
		/// </summary>
		public List<GameModeDefinition> GetGameModesForPlayerLevel(int playerLevel)
		{
			return Registry.Objects.Where(m => m.IsAvailableForPlayerLevel(playerLevel)).ToList();
		}
		
		/// <summary>
		/// Gets game modes sorted by priority.
		/// </summary>
		public List<GameModeDefinition> GetGameModesByPriority()
		{
			return Registry.Objects.OrderBy(m => m.Priority).ToList();
		}
		
		/// <summary>
		/// Gets featured game modes.
		/// </summary>
		public List<GameModeDefinition> GetFeaturedGameModes()
		{
			return Registry.Objects.Where(m => m.IsFeatured && m.IsActive).ToList();
		}
		
		/// <summary>
		/// Gets active game modes.
		/// </summary>
		public List<GameModeDefinition> GetActiveGameModes()
		{
			return Registry.Objects.Where(m => m.IsActive).ToList();
		}
		
		/// <summary>
		/// Gets multiplayer game modes.
		/// </summary>
		public List<GameModeDefinition> GetMultiplayerGameModes()
		{
			return Registry.Objects.Where(m => m.IsMultiplayer).ToList();
		}
		
		/// <summary>
		/// Gets cooperative game modes.
		/// </summary>
		public List<GameModeDefinition> GetCooperativeGameModes()
		{
			return Registry.Objects.Where(m => m.IsCooperative).ToList();
		}
		
		/// <summary>
		/// Gets competitive game modes.
		/// </summary>
		public List<GameModeDefinition> GetCompetitiveGameModes()
		{
			return Registry.Objects.Where(m => m.IsCompetitive).ToList();
		}
		
		/// <summary>
		/// Gets game modes with leaderboards.
		/// </summary>
		public List<GameModeDefinition> GetGameModesWithLeaderboards()
		{
			return Registry.Objects.Where(m => m.HasLeaderboard).ToList();
		}
		
		/// <summary>
		/// Gets game modes with time limits.
		/// </summary>
		public List<GameModeDefinition> GetGameModesWithTimeLimit()
		{
			return Registry.Objects.Where(m => m.HasTimeLimit).ToList();
		}
		
		/// <summary>
		/// Gets game modes with shot limits.
		/// </summary>
		public List<GameModeDefinition> GetGameModesWithShotLimit()
		{
			return Registry.Objects.Where(m => m.HasShotLimit).ToList();
		}
		
		/// <summary>
		/// Gets game modes with lives.
		/// </summary>
		public List<GameModeDefinition> GetGameModesWithLives()
		{
			return Registry.Objects.Where(m => m.HasLives).ToList();
		}
		
		/// <summary>
		/// Gets game modes with level progression.
		/// </summary>
		public List<GameModeDefinition> GetGameModesWithLevelProgression()
		{
			return Registry.Objects.Where(m => m.HasLevelProgression).ToList();
		}
	}
}