using System.Collections.Generic;
using System.Linq;
using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.MetaData.Tutorial
{
	/// <summary>
	/// Container for all tutorial metadata with query methods.
	/// Provides centralized access to tutorial definitions and filtering capabilities.
	/// </summary>
	[CreateAssetMenu(fileName = "TutorialsMeta", menuName = "Gameplay/MetaData/Tutorial/TutorialsMeta")]
	public class TutorialsMeta : ScriptableObject
	{
		[Header("Registry")]
		[InlineEditor]
		[SerializeField]
		private TutorialsRegistry _registry;
		
		public TutorialsRegistry Registry => _registry;
		
		/// <summary>
		/// Gets a tutorial by its UID.
		/// </summary>
		public TutorialDefinition GetTutorial(UID uid)
		{
			return _registry.GetObjectByUID(uid);
		}
		
		/// <summary>
		/// Gets all tutorials.
		/// </summary>
		public IReadOnlyList<TutorialDefinition> GetAllTutorials()
		{
			return _registry.Registry.Objects;
		}
		
		/// <summary>
		/// Gets tutorials of a specific type.
		/// </summary>
		public List<TutorialDefinition> GetTutorialsByType(TutorialType type)
		{
			return _registry.GetTutorialsByType(type);
		}
		
		/// <summary>
		/// Gets tutorials that should be shown for a specific level.
		/// </summary>
		public List<TutorialDefinition> GetTutorialsForLevel(int level)
		{
			return _registry.GetTutorialsForLevel(level);
		}
		
		/// <summary>
		/// Gets tutorials that should be shown for a specific player level.
		/// </summary>
		public List<TutorialDefinition> GetTutorialsForPlayerLevel(int playerLevel)
		{
			return _registry.GetTutorialsForPlayerLevel(playerLevel);
		}
		
		/// <summary>
		/// Gets tutorials sorted by priority (lower priority = shown first).
		/// </summary>
		public List<TutorialDefinition> GetTutorialsByPriority()
		{
			return _registry.GetTutorialsByPriority();
		}
		
		/// <summary>
		/// Gets tutorials that can be skipped.
		/// </summary>
		public List<TutorialDefinition> GetSkippableTutorials()
		{
			return _registry.GetSkippableTutorials();
		}
		
		/// <summary>
		/// Gets tutorials that can be replayed.
		/// </summary>
		public List<TutorialDefinition> GetReplayableTutorials()
		{
			return _registry.GetReplayableTutorials();
		}
		
		/// <summary>
		/// Gets tutorials that should only be shown once.
		/// </summary>
		public List<TutorialDefinition> GetOneTimeTutorials()
		{
			return _registry.GetOneTimeTutorials();
		}
		
		/// <summary>
		/// Gets the next tutorial to show for a specific level, considering prerequisites.
		/// </summary>
		public TutorialDefinition GetNextTutorialForLevel(int level, HashSet<UID> completedTutorials)
		{
			var availableTutorials = _registry.GetTutorialsForLevel(level)
				.Where(t => !completedTutorials.Contains(t.UniqueID))
				.ToList();
			
			// Filter by prerequisites
			var validTutorials = availableTutorials
				.Where(t => ArePrerequisitesMet(t, completedTutorials))
				.OrderBy(t => t.Priority)
				.ToList();
			
			return validTutorials.FirstOrDefault();
		}
		
		/// <summary>
		/// Gets all tutorials that should be shown for a level, considering prerequisites.
		/// </summary>
		public List<TutorialDefinition> GetAvailableTutorialsForLevel(int level, HashSet<UID> completedTutorials)
		{
			return _registry.GetTutorialsForLevel(level)
				.Where(t => !completedTutorials.Contains(t.UniqueID))
				.Where(t => ArePrerequisitesMet(t, completedTutorials))
				.OrderBy(t => t.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Checks if a tutorial's prerequisites are met.
		/// </summary>
		private bool ArePrerequisitesMet(TutorialDefinition tutorial, HashSet<UID> completedTutorials)
		{
			foreach (var prereqUid in tutorial.PrerequisiteTutorials)
			{
				if (!completedTutorials.Contains(prereqUid))
				{
					return false;
				}
			}
			
			return true;
		}
		
		/// <summary>
		/// Gets onboarding tutorials (first-time player tutorials).
		/// </summary>
		public List<TutorialDefinition> GetOnboardingTutorials()
		{
			return _registry.GetTutorialsByType(TutorialType.Onboarding)
				.OrderBy(t => t.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets gameplay basics tutorials.
		/// </summary>
		public List<TutorialDefinition> GetGameplayBasicsTutorials()
		{
			return _registry.GetTutorialsByType(TutorialType.GameplayBasics)
				.OrderBy(t => t.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets powerup tutorials.
		/// </summary>
		public List<TutorialDefinition> GetPowerupTutorials()
		{
			return _registry.GetTutorialsByType(TutorialType.Powerup)
				.OrderBy(t => t.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets booster tutorials.
		/// </summary>
		public List<TutorialDefinition> GetBoosterTutorials()
		{
			return _registry.GetTutorialsByType(TutorialType.Booster)
				.OrderBy(t => t.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets special tiles tutorials.
		/// </summary>
		public List<TutorialDefinition> GetSpecialTilesTutorials()
		{
			return _registry.GetTutorialsByType(TutorialType.SpecialTiles)
				.OrderBy(t => t.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets advanced strategy tutorials.
		/// </summary>
		public List<TutorialDefinition> GetAdvancedTutorials()
		{
			return _registry.GetTutorialsByType(TutorialType.Advanced)
				.OrderBy(t => t.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets UI tutorials.
		/// </summary>
		public List<TutorialDefinition> GetUITutorials()
		{
			return _registry.GetTutorialsByType(TutorialType.UI)
				.OrderBy(t => t.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets store tutorials.
		/// </summary>
		public List<TutorialDefinition> GetStoreTutorials()
		{
			return _registry.GetTutorialsByType(TutorialType.Store)
				.OrderBy(t => t.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets event-specific tutorials.
		/// </summary>
		public List<TutorialDefinition> GetEventTutorials()
		{
			return _registry.GetTutorialsByType(TutorialType.Event)
				.OrderBy(t => t.Priority)
				.ToList();
		}
	}
}