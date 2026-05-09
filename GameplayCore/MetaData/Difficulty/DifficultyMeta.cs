using System.Collections.Generic;
using System.Linq;
using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.MetaData.Difficulty
{
	/// <summary>
	/// Container for all difficulty metadata with query methods.
	/// Provides centralized access to difficulty definitions and filtering capabilities.
	/// </summary>
	[CreateAssetMenu(fileName = "DifficultyMeta", menuName = "Gameplay/MetaData/Difficulty/DifficultyMeta")]
	public class DifficultyMeta : ScriptableObject
	{
		[Header("Registry")]
		[InlineEditor]
		[SerializeField]
		private DifficultyRegistry _registry;
		
		public DifficultyRegistry Registry => _registry;
		
		/// <summary>
		/// Gets a difficulty by its UID.
		/// </summary>
		public DifficultyDefinition GetDifficulty(UID uid)
		{
			return _registry.GetObjectByUID(uid);
		}
		
		/// <summary>
		/// Gets all difficulties.
		/// </summary>
		public IReadOnlyList<DifficultyDefinition> GetAllDifficulties()
		{
			return _registry.Registry.Objects;
		}
		
		/// <summary>
		/// Gets difficulties of a specific type.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesByType(DifficultyType type)
		{
			return _registry.GetDifficultiesByType(type);
		}
		
		/// <summary>
		/// Gets difficulties that apply to a specific level.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesForLevel(int level)
		{
			return _registry.GetDifficultiesForLevel(level);
		}
		
		/// <summary>
		/// Gets difficulties sorted by difficulty level.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesByLevel()
		{
			return _registry.GetDifficultiesByLevel();
		}
		
		/// <summary>
		/// Gets the difficulty with a specific difficulty level.
		/// </summary>
		public DifficultyDefinition GetDifficultyByLevel(int level)
		{
			return _registry.GetDifficultyByLevel(level);
		}
		
		/// <summary>
		/// Gets the best matching difficulty for a specific level.
		/// </summary>
		public DifficultyDefinition GetDifficultyForLevel(int level)
		{
			var applicableDifficulties = _registry.GetDifficultiesForLevel(level);
			
			if (applicableDifficulties.Count == 0)
			{
				return null;
			}
			
			// Return the difficulty with the highest difficulty level that applies
			return applicableDifficulties.OrderByDescending(d => d.DifficultyLevel).FirstOrDefault();
		}
		
		/// <summary>
		/// Gets difficulties with special tiles enabled.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesWithSpecialTiles()
		{
			return _registry.GetDifficultiesWithSpecialTiles();
		}
		
		/// <summary>
		/// Gets difficulties with powerups enabled.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesWithPowerups()
		{
			return _registry.GetDifficultiesWithPowerups();
		}
		
		/// <summary>
		/// Gets difficulties with enemies enabled.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesWithEnemies()
		{
			return _registry.GetDifficultiesWithEnemies();
		}
		
		/// <summary>
		/// Gets difficulties with time limits.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesWithTimeLimit()
		{
			return _registry.GetDifficultiesWithTimeLimit();
		}
		
		/// <summary>
		/// Gets difficulties with shot limits.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesWithShotLimit()
		{
			return _registry.GetDifficultiesWithShotLimit();
		}
		
		/// <summary>
		/// Gets tutorial difficulties.
		/// </summary>
		public List<DifficultyDefinition> GetTutorialDifficulties()
		{
			return _registry.GetDifficultiesByType(DifficultyType.Tutorial);
		}
		
		/// <summary>
		/// Gets easy difficulties (VeryEasy, Easy).
		/// </summary>
		public List<DifficultyDefinition> GetEasyDifficulties()
		{
			return _registry.Registry.Objects
				.Where(d => d.Type == DifficultyType.VeryEasy || d.Type == DifficultyType.Easy)
				.OrderBy(d => d.DifficultyLevel)
				.ToList();
		}
		
		/// <summary>
		/// Gets normal difficulties.
		/// </summary>
		public List<DifficultyDefinition> GetNormalDifficulties()
		{
			return _registry.GetDifficultiesByType(DifficultyType.Normal)
				.OrderBy(d => d.DifficultyLevel)
				.ToList();
		}
		
		/// <summary>
		/// Gets hard difficulties (Hard, VeryHard).
		/// </summary>
		public List<DifficultyDefinition> GetHardDifficulties()
		{
			return _registry.Registry.Objects
				.Where(d => d.Type == DifficultyType.Hard || d.Type == DifficultyType.VeryHard)
				.OrderBy(d => d.DifficultyLevel)
				.ToList();
		}
		
		/// <summary>
		/// Gets expert difficulties (Expert, Master, Insane).
		/// </summary>
		public List<DifficultyDefinition> GetExpertDifficulties()
		{
			return _registry.Registry.Objects
				.Where(d => d.Type == DifficultyType.Expert || d.Type == DifficultyType.Master || d.Type == DifficultyType.Insane)
				.OrderBy(d => d.DifficultyLevel)
				.ToList();
		}
		
		/// <summary>
		/// Gets the minimum difficulty level.
		/// </summary>
		public int GetMinDifficultyLevel()
		{
			return _registry.Registry.Objects.Min(d => d.DifficultyLevel);
		}
		
		/// <summary>
		/// Gets the maximum difficulty level.
		/// </summary>
		public int GetMaxDifficultyLevel()
		{
			return _registry.Registry.Objects.Max(d => d.DifficultyLevel);
		}
		
		/// <summary>
		/// Gets the average difficulty level.
		/// </summary>
		public float GetAverageDifficultyLevel()
		{
			return (float)_registry.Registry.Objects.Average(d => d.DifficultyLevel);
		}
	}
}