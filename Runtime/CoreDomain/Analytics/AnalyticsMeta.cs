using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.CoreDomain.Analytics;
using UnityEngine;

namespace AK.CoreDomain
{
	/// <summary>
	/// Container for all analytics event definitions with powerful query methods.
	/// Similar to IAPMeta but for analytics events.
	/// </summary>
	[CreateAssetMenu(fileName = "AnalyticsMeta", menuName = "AK/MetaData/Analytics/AnalyticsMeta")]
	public class AnalyticsMeta : MetaDataAsset, IMetaWithRegistry
	{
		[SerializeField] private AnalyticsRegistry _registry;

		public AnalyticsEventIds Ids;

		[Header("Analytics Events")] [Tooltip("All analytics event definitions.")]
		public List<AnalyticsEventDefinition> Events;

		public AnalyticsRegistry    Registry      => _registry;
		public UidRegistryAssetBase RegistryAsset => _registry;

		public void InitializeMeta() { }

		/// <summary>Resolves an event definition by identity. False when unknown — never falls back to a name.</summary>
		public bool TryGetEvent(Uid<AnalyticsEventDefinition> eventId, out AnalyticsEventDefinition definition)
		{
			if (_registry != null)
			{
				return _registry.TryResolve(eventId, out definition);
			}

			definition = null;
			return false;
		}

		/// <summary>
		/// Looks up a definition by its EventID string (or ProviderEventName).
		/// Used by the fail-open string API so games do not have to pass asset UIDs.
		/// </summary>
		public AnalyticsEventDefinition GetEventByName(string eventName)
		{
			if (string.IsNullOrEmpty(eventName) || Events == null)
			{
				return null;
			}

			for (int i = 0; i < Events.Count; i++)
			{
				AnalyticsEventDefinition def = Events[i];
				if (def == null)
				{
					continue;
				}

				if (def.EventID == eventName || def.ProviderEventName == eventName || def.DesignEventId == eventName)
				{
					return def;
				}
			}

			return null;
		}

		/// <summary>
		/// Gets all events of a specific category.
		/// </summary>
		public List<AnalyticsEventDefinition> GetEventsByCategory(AnalyticsEventCategory category)
		{
			return Events.Where(e => e.Category == category).ToList();
		}

		/// <summary>
		/// Gets all gameplay events.
		/// </summary>
		public List<AnalyticsEventDefinition> GetGameplayEvents()
		{
			return Events.Where(e => e.Category == AnalyticsEventCategory.Gameplay).ToList();
		}

		/// <summary>
		/// Gets all monetization events.
		/// </summary>
		public List<AnalyticsEventDefinition> GetMonetizationEvents()
		{
			return Events.Where(e => e.Category == AnalyticsEventCategory.Monetization).ToList();
		}

		/// <summary>
		/// Gets all engagement events.
		/// </summary>
		public List<AnalyticsEventDefinition> GetEngagementEvents()
		{
			return Events.Where(e => e.Category == AnalyticsEventCategory.Engagement).ToList();
		}

		/// <summary>
		/// Gets all progression events.
		/// </summary>
		public List<AnalyticsEventDefinition> GetProgressionEvents()
		{
			return Events.Where(e => e.Category == AnalyticsEventCategory.Progression).ToList();
		}

		/// <summary>
		/// Gets all active events.
		/// </summary>
		public List<AnalyticsEventDefinition> GetActiveEvents()
		{
			return Events.Where(e => e.IsActive).ToList();
		}

		/// <summary>
		/// Gets all active events for a specific category.
		/// </summary>
		public List<AnalyticsEventDefinition> GetActiveEventsByCategory(AnalyticsEventCategory category)
		{
			return Events.Where(e => e.Category == category && e.IsActive).ToList();
		}

		/// <summary>
		/// Gets all events that should be batched.
		/// </summary>
		public List<AnalyticsEventDefinition> GetBatchableEvents()
		{
			return Events.Where(e => e.ShouldBatch && e.IsActive).ToList();
		}

		/// <summary>
		/// Gets all events sorted by priority (highest first).
		/// </summary>
		public List<AnalyticsEventDefinition> GetEventsByPriority()
		{
			return Events.OrderByDescending(e => e.Priority).ToList();
		}

		/// <summary>
		/// Gets all events available for the current level.
		/// </summary>
		public List<AnalyticsEventDefinition> GetAvailableEvents(int currentLevel = 1)
		{
			return Events.Where(e => e.IsActive && e.IsAvailable(currentLevel)).ToList();
		}

		/// <summary>
		/// Gets all events that require batching.
		/// </summary>
		public List<AnalyticsEventDefinition> GetEventsRequiringBatching()
		{
			return Events.Where(e => e.ShouldBatch && e.IsActive).ToList();
		}

		/// <summary>
		/// Checks if an event exists by EventID.
		/// </summary>
		public bool HasEvent(string eventID)
		{
			return Events.Any(e => e.EventID == eventID);
		}
	}
}