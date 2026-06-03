using System.Collections.Generic;
using System.Linq;
using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Tutorial
{
	/// <summary>
	/// Registry for all tutorial definitions using UID-based lookup.
	/// Provides centralized management of tutorial data.
	/// </summary>
	[CreateAssetMenu(fileName = "TutorialsRegistry", menuName = "Gameplay/MetaData/Tutorial/TutorialsRegistry")]
	public class TutorialsRegistry : TypedUIDRegistryAsset<TutorialDefinition>
	{
		/// <summary>
		/// Gets all tutorials of a specific type.
		/// </summary>
		public List<TutorialDefinition> GetTutorialsByType(TutorialType type)
		{
			return Registry.Objects.Where(t => t.Type == type).ToList();
		}
		
		/// <summary>
		/// Gets tutorials that should be shown for a specific level.
		/// </summary>
		public List<TutorialDefinition> GetTutorialsForLevel(int level)
		{
			return Registry.Objects.Where(t => t.ShouldShowForLevel(level)).ToList();
		}
		
		/// <summary>
		/// Gets tutorials that should be shown for a specific player level.
		/// </summary>
		public List<TutorialDefinition> GetTutorialsForPlayerLevel(int playerLevel)
		{
			return Registry.Objects.Where(t => t.ShouldShowForPlayerLevel(playerLevel)).ToList();
		}
		
		/// <summary>
		/// Gets tutorials sorted by priority (lower priority = shown first).
		/// </summary>
		public List<TutorialDefinition> GetTutorialsByPriority()
		{
			return Registry.Objects.OrderBy(t => t.Priority).ToList();
		}
		
		/// <summary>
		/// Gets tutorials that can be skipped.
		/// </summary>
		public List<TutorialDefinition> GetSkippableTutorials()
		{
			return Registry.Objects.Where(t => t.CanSkip).ToList();
		}
		
		/// <summary>
		/// Gets tutorials that can be replayed.
		/// </summary>
		public List<TutorialDefinition> GetReplayableTutorials()
		{
			return Registry.Objects.Where(t => t.CanReplay).ToList();
		}
		
		/// <summary>
		/// Gets tutorials that should only be shown once.
		/// </summary>
		public List<TutorialDefinition> GetOneTimeTutorials()
		{
			return Registry.Objects.Where(t => t.ShowOnce).ToList();
		}
	}
}