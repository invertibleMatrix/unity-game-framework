using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.MetaData.Seasons
{
	/// <summary>
	/// Container for event definitions with query methods
	/// </summary>
	[CreateAssetMenu(fileName = "SeasonsMeta", menuName = "Gameplay/MetaData/Seasons/SeasonsMeta")]
	public class SeasonsMeta : MetaDataAsset
	{
		[Header("Events")]
		[Tooltip("All event definitions")]
		public List<EventDefinition> Events;
		
		/// <summary>
		/// Get event by ID
		/// </summary>
		public EventDefinition GetEventByID(string eventID)
		{
			return Events.FirstOrDefault(e => e.EventID == eventID);
		}
		
		/// <summary>
		/// Get event by UID
		/// </summary>
		public EventDefinition GetEventByUID(UID uid)
		{
			return Events.FirstOrDefault(e => e.UID == uid);
		}
		
		/// <summary>
		/// Get all events of a specific type
		/// </summary>
		public List<EventDefinition> GetEventsByType(EventType type)
		{
			return Events.Where(e => e.Type == type).ToList();
		}
		
		/// <summary>
		/// Get all active events
		/// </summary>
		public List<EventDefinition> GetActiveEvents()
		{
			return Events.Where(e => e.IsCurrentlyActive()).ToList();
		}
		
		/// <summary>
		/// Get events available to a player
		/// </summary>
		public List<EventDefinition> GetAvailableEvents(int playerLevel, List<UID> completedAchievements)
		{
			return Events.Where(e => e.IsAvailableToPlayer(playerLevel, completedAchievements)).ToList();
		}
		
		/// <summary>
		/// Get events with leaderboards
		/// </summary>
		public List<EventDefinition> GetEventsWithLeaderboards()
		{
			return Events.Where(e => e.HasLeaderboard).ToList();
		}
		
		/// <summary>
		/// Get events starting soon (within hours)
		/// </summary>
		public List<EventDefinition> GetEventsStartingSoon(int hours)
		{
			long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			long startTime = currentTime + (hours * 3600);
			
			return Events.Where(e => 
				e.IsActive && 
				e.StartTime > currentTime && 
				e.StartTime <= startTime
			).ToList();
		}
		
		/// <summary>
		/// Get events ending soon (within hours)
		/// </summary>
		public List<EventDefinition> GetEventsEndingSoon(int hours)
		{
			long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			long endTime = currentTime + (hours * 3600);
			
			return Events.Where(e => 
				e.IsCurrentlyActive() && 
				e.EndTime > currentTime && 
				e.EndTime <= endTime
			).ToList();
		}
		
		/// <summary>
		/// Get events for a specific level range
		/// </summary>
		public List<EventDefinition> GetEventsForLevelRange(int minLevel, int maxLevel)
		{
			return Events.Where(e => e.MinimumLevel >= minLevel && (e.MaximumLevel == 0 || e.MaximumLevel <= maxLevel)).ToList();
		}
		
		/// <summary>
		/// Get events sorted by start time
		/// </summary>
		public List<EventDefinition> GetEventsSortedByStartTime()
		{
			return Events.OrderBy(e => e.StartTime).ToList();
		}
		
		/// <summary>
		/// Get events sorted by end time
		/// </summary>
		public List<EventDefinition> GetEventsSortedByEndTime()
		{
			return Events.OrderBy(e => e.EndTime).ToList();
		}
		
		/// <summary>
		/// Get events with analytics tracking
		/// </summary>
		public List<EventDefinition> GetEventsWithAnalytics()
		{
			return Events.Where(e => !string.IsNullOrEmpty(e.CompletionEventID)).ToList();
		}
		
		/// <summary>
		/// Get total number of events
		/// </summary>
		public int GetTotalEventCount()
		{
			return Events.Count;
		}
		
		/// <summary>
		/// Get number of events by type
		/// </summary>
		public int GetEventCountByType(EventType type)
		{
			return Events.Count(e => e.Type == type);
		}
		
		/// <summary>
		/// Get event completion percentage for a player
		/// </summary>
		public float GetCompletionPercentage(List<UID> completedEvents)
		{
			if (Events.Count == 0) return 0f;
			
			int completedCount = Events.Count(e => completedEvents.Contains(e.UID));
			return (float)completedCount / Events.Count * 100f;
		}
		
		/// <summary>
		/// Get event completion percentage by type
		/// </summary>
		public float GetCompletionPercentageByType(EventType type, List<UID> completedEvents)
		{
			var typeEvents = GetEventsByType(type);
			if (typeEvents.Count == 0) return 0f;
			
			int completedCount = typeEvents.Count(e => completedEvents.Contains(e.UID));
			return (float)completedCount / typeEvents.Count * 100f;
		}
	}
}