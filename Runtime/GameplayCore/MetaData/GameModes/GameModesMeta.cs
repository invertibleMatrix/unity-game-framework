using System.Collections.Generic;
using System.Linq;
using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.MetaData.GameModes
{
	/// <summary>
	/// Container for all game mode metadata with query methods.
	/// Provides centralized access to game mode definitions and filtering capabilities.
	/// </summary>
	[CreateAssetMenu(fileName = "GameModesMeta", menuName = "Gameplay/MetaData/GameModes/GameModesMeta")]
	public class GameModesMeta : ScriptableObject
	{
		[Header("Registry")]
		[InlineEditor]
		[SerializeField]
		private GameModesRegistry _registry;
		
		public GameModesRegistry Registry => _registry;
		
		/// <summary>
		/// Gets a game mode by its UID.
		/// </summary>
		public GameModeDefinition GetGameMode(UID uid)
		{
			return _registry.GetObjectByUID(uid);
		}
		
		/// <summary>
		/// Gets all game modes.
		/// </summary>
		public IReadOnlyList<GameModeDefinition> GetAllGameModes()
		{
			return _registry.Registry.Objects;
		}
		
		/// <summary>
		/// Gets game modes of a specific type.
		/// </summary>
		public List<GameModeDefinition> GetGameModesByType(GameModeType type)
		{
			return _registry.GetGameModesByType(type);
		}
		
		/// <summary>
		/// Gets game modes available for a specific player level.
		/// </summary>
		public List<GameModeDefinition> GetGameModesForPlayerLevel(int playerLevel)
		{
			return _registry.GetGameModesForPlayerLevel(playerLevel);
		}
		
		/// <summary>
		/// Gets game modes sorted by priority.
		/// </summary>
		public List<GameModeDefinition> GetGameModesByPriority()
		{
			return _registry.GetGameModesByPriority();
		}
		
		/// <summary>
		/// Gets featured game modes.
		/// </summary>
		public List<GameModeDefinition> GetFeaturedGameModes()
		{
			return _registry.GetFeaturedGameModes();
		}
		
		/// <summary>
		/// Gets active game modes.
		/// </summary>
		public List<GameModeDefinition> GetActiveGameModes()
		{
			return _registry.GetActiveGameModes();
		}
		
		/// <summary>
		/// Gets multiplayer game modes.
		/// </summary>
		public List<GameModeDefinition> GetMultiplayerGameModes()
		{
			return _registry.GetMultiplayerGameModes();
		}
		
		/// <summary>
		/// Gets cooperative game modes.
		/// </summary>
		public List<GameModeDefinition> GetCooperativeGameModes()
		{
			return _registry.GetCooperativeGameModes();
		}
		
		/// <summary>
		/// Gets competitive game modes.
		/// </summary>
		public List<GameModeDefinition> GetCompetitiveGameModes()
		{
			return _registry.GetCompetitiveGameModes();
		}
		
		/// <summary>
		/// Gets game modes with leaderboards.
		/// </summary>
		public List<GameModeDefinition> GetGameModesWithLeaderboards()
		{
			return _registry.GetGameModesWithLeaderboards();
		}
		
		/// <summary>
		/// Gets game modes with time limits.
		/// </summary>
		public List<GameModeDefinition> GetGameModesWithTimeLimit()
		{
			return _registry.GetGameModesWithTimeLimit();
		}
		
		/// <summary>
		/// Gets game modes with shot limits.
		/// </summary>
		public List<GameModeDefinition> GetGameModesWithShotLimit()
		{
			return _registry.GetGameModesWithShotLimit();
		}
		
		/// <summary>
		/// Gets game modes with lives.
		/// </summary>
		public List<GameModeDefinition> GetGameModesWithLives()
		{
			return _registry.GetGameModesWithLives();
		}
		
		/// <summary>
		/// Gets game modes with level progression.
		/// </summary>
		public List<GameModeDefinition> GetGameModesWithLevelProgression()
		{
			return _registry.GetGameModesWithLevelProgression();
		}
		
		/// <summary>
		/// Gets campaign game modes.
		/// </summary>
		public List<GameModeDefinition> GetCampaignGameModes()
		{
			return _registry.GetGameModesByType(GameModeType.Campaign)
				.OrderBy(m => m.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets endless game modes.
		/// </summary>
		public List<GameModeDefinition> GetEndlessGameModes()
		{
			return _registry.GetGameModesByType(GameModeType.Endless)
				.OrderBy(m => m.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets time attack game modes.
		/// </summary>
		public List<GameModeDefinition> GetTimeAttackGameModes()
		{
			return _registry.GetGameModesByType(GameModeType.TimeAttack)
				.OrderBy(m => m.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets shot limit game modes.
		/// </summary>
		public List<GameModeDefinition> GetShotLimitGameModes()
		{
			return _registry.GetGameModesByType(GameModeType.ShotLimit)
				.OrderBy(m => m.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets survival game modes.
		/// </summary>
		public List<GameModeDefinition> GetSurvivalGameModes()
		{
			return _registry.GetGameModesByType(GameModeType.Survival)
				.OrderBy(m => m.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets puzzle game modes.
		/// </summary>
		public List<GameModeDefinition> GetPuzzleGameModes()
		{
			return _registry.GetGameModesByType(GameModeType.Puzzle)
				.OrderBy(m => m.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets daily challenge game modes.
		/// </summary>
		public List<GameModeDefinition> GetDailyChallengeGameModes()
		{
			return _registry.GetGameModesByType(GameModeType.DailyChallenge)
				.OrderBy(m => m.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets event game modes.
		/// </summary>
		public List<GameModeDefinition> GetEventGameModes()
		{
			return _registry.GetGameModesByType(GameModeType.Event)
				.OrderBy(m => m.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets practice game modes.
		/// </summary>
		public List<GameModeDefinition> GetPracticeGameModes()
		{
			return _registry.GetGameModesByType(GameModeType.Practice)
				.OrderBy(m => m.Priority)
				.ToList();
		}
	}
}