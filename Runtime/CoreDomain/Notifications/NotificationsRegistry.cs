using System.Collections.Generic;
using System.Linq;
using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Notifications
{
	/// <summary>
	/// Registry for all notification definitions using UID-based lookup.
	/// Provides centralized management of notification data.
	/// </summary>
	[CreateAssetMenu(fileName = "NotificationsRegistry", menuName = "AK/MetaData/Notifications/NotificationsRegistry")]
	public class NotificationsRegistry : TypedUIDRegistryAsset<NotificationDefinition>
	{
		/// <summary>
		/// Gets all notifications of a specific type.
		/// </summary>
		public List<NotificationDefinition> GetNotificationsByType(NotificationType type)
		{
			return Registry.Objects.Where(n => n.Type == type).ToList();
		}
		
		/// <summary>
		/// Gets notifications available for a specific player level.
		/// </summary>
		public List<NotificationDefinition> GetNotificationsForPlayerLevel(int playerLevel)
		{
			return Registry.Objects.Where(n => n.ShouldShowForPlayerLevel(playerLevel)).ToList();
		}
		
		/// <summary>
		/// Gets notifications sorted by priority.
		/// </summary>
		public List<NotificationDefinition> GetNotificationsByPriority()
		{
			return Registry.Objects.OrderBy(n => n.Priority).ToList();
		}
		
		/// <summary>
		/// Gets notifications that can be dismissed.
		/// </summary>
		public List<NotificationDefinition> GetDismissibleNotifications()
		{
			return Registry.Objects.Where(n => n.CanDismiss).ToList();
		}
		
		/// <summary>
		/// Gets notifications that can be snoozed.
		/// </summary>
		public List<NotificationDefinition> GetSnoozableNotifications()
		{
			return Registry.Objects.Where(n => n.CanSnooze).ToList();
		}
		
		/// <summary>
		/// Gets notifications with actions.
		/// </summary>
		public List<NotificationDefinition> GetNotificationsWithActions()
		{
			return Registry.Objects.Where(n => n.HasAction).ToList();
		}
		
		/// <summary>
		/// Gets notifications with rewards.
		/// </summary>
		public List<NotificationDefinition> GetNotificationsWithRewards()
		{
			return Registry.Objects.Where(n => n.Rewards.Count > 0).ToList();
		}
		
		/// <summary>
		/// Gets notifications that show only once.
		/// </summary>
		public List<NotificationDefinition> GetOneTimeNotifications()
		{
			return Registry.Objects.Where(n => n.ShowOnce).ToList();
		}
		
		/// <summary>
		/// Gets scheduled notifications.
		/// </summary>
		public List<NotificationDefinition> GetScheduledNotifications()
		{
			return Registry.Objects.Where(n => n.IsScheduled).ToList();
		}
		
		/// <summary>
		/// Gets enabled notifications that can be scheduled as native push notifications.
		/// </summary>
		public List<NotificationDefinition> GetEnabledNotifications()
		{
			return Registry.Objects.Where(n => n.IsEnabled).ToList();
		}
		
		/// <summary>
		/// Gets enabled notifications of a specific type.
		/// </summary>
		public List<NotificationDefinition> GetEnabledNotificationsByType(NotificationType type)
		{
			return Registry.Objects.Where(n => n.IsEnabled && n.Type == type).ToList();
		}
	}
}