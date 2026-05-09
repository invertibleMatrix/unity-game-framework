using System.Collections.Generic;
using System.Linq;
using AK.Core;
using UnityEngine;

namespace GameplayCore.MetaData.Difficulty
{
	/// <summary>
	/// Registry for all difficulty definitions using UID-based lookup.
	/// Provides centralized management of difficulty data.
	/// </summary>
	[CreateAssetMenu(fileName = "DifficultyRegistry", menuName = "Gameplay/MetaData/Difficulty/DifficultyRegistry")]
	public class DifficultyRegistry : TypedUIDRegistryAsset<DifficultyDefinition>
	{
		/// <summary>
		/// Gets all difficulties of a specific type.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesByType(DifficultyType type)
		{
			return Registry.Objects.Where(d => d.Type == type).ToList();
		}
		
		/// <summary>
		/// Gets difficulties that apply to a specific level.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesForLevel(int level)
		{
			return Registry.Objects.Where(d => d.AppliesToLevel(level)).ToList();
		}
		
		/// <summary>
		/// Gets difficulties sorted by difficulty level.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesByLevel()
		{
			return Registry.Objects.OrderBy(d => d.DifficultyLevel).ToList();
		}
		
		/// <summary>
		/// Gets the difficulty with a specific difficulty level.
		/// </summary>
		public DifficultyDefinition GetDifficultyByLevel(int level)
		{
			return Registry.Objects.FirstOrDefault(d => d.DifficultyLevel == level);
		}
		
		/// <summary>
		/// Gets difficulties with special tiles enabled.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesWithSpecialTiles()
		{
			return Registry.Objects.Where(d => d.EnableSpecialTiles).ToList();
		}
		
		/// <summary>
		/// Gets difficulties with powerups enabled.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesWithPowerups()
		{
			return Registry.Objects.Where(d => d.EnablePowerups).ToList();
		}
		
		/// <summary>
		/// Gets difficulties with enemies enabled.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesWithEnemies()
		{
			return Registry.Objects.Where(d => d.EnableEnemies).ToList();
		}
		
		/// <summary>
		/// Gets difficulties with time limits.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesWithTimeLimit()
		{
			return Registry.Objects.Where(d => d.TimeLimit > 0).ToList();
		}
		
		/// <summary>
		/// Gets difficulties with shot limits.
		/// </summary>
		public List<DifficultyDefinition> GetDifficultiesWithShotLimit()
		{
			return Registry.Objects.Where(d => d.MaxShots > 0).ToList();
		}
	}
}