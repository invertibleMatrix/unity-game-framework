using System.Collections.Generic;
using System.Linq;
using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Notifications
{
	/// <summary>
	/// Container for all notification metadata with query methods.
	/// Provides centralized access to notification definitions and filtering capabilities.
	/// </summary>
	[CreateAssetMenu(fileName = "NotificationsMeta", menuName = "AK/MetaData/Notifications/NotificationsMeta")]
	public class NotificationsMeta : ScriptableObject, IMeta
	{
		[Header("Registry")]
		[SerializeField]
		private NotificationsRegistry _registry;
		
		public NotificationsRegistry Registry => _registry;

		public NotificationDefinition DailyRewardNotification;

		public void InitializeMeta()
		{
			if (_registry != null) _registry.Initialize();
		}
		
		/// <summary>
		/// Gets a notification by its UID.
		/// </summary>
		public NotificationDefinition GetNotification(UID uid)
		{
			return _registry.GetObjectByUID(uid);
		}
		
		/// <summary>
		/// Gets all notifications.
		/// </summary>
		public IReadOnlyList<NotificationDefinition> GetAllNotifications()
		{
			return _registry.Registry.Objects;
		}
		
		/// <summary>
		/// Gets notifications of a specific type.
		/// </summary>
		public List<NotificationDefinition> GetNotificationsByType(NotificationType type)
		{
			return _registry.GetNotificationsByType(type);
		}
		
		/// <summary>
		/// Gets notifications available for a specific player level.
		/// </summary>
		public List<NotificationDefinition> GetNotificationsForPlayerLevel(int playerLevel)
		{
			return _registry.GetNotificationsForPlayerLevel(playerLevel);
		}
		
		/// <summary>
		/// Gets notifications sorted by priority.
		/// </summary>
		public List<NotificationDefinition> GetNotificationsByPriority()
		{
			return _registry.GetNotificationsByPriority();
		}
		
		/// <summary>
		/// Gets notifications that can be dismissed.
		/// </summary>
		public List<NotificationDefinition> GetDismissibleNotifications()
		{
			return _registry.GetDismissibleNotifications();
		}
		
		/// <summary>
		/// Gets notifications that can be snoozed.
		/// </summary>
		public List<NotificationDefinition> GetSnoozableNotifications()
		{
			return _registry.GetSnoozableNotifications();
		}
		
		/// <summary>
		/// Gets notifications with actions.
		/// </summary>
		public List<NotificationDefinition> GetNotificationsWithActions()
		{
			return _registry.GetNotificationsWithActions();
		}
		
		/// <summary>
		/// Gets notifications that show only once.
		/// </summary>
		public List<NotificationDefinition> GetOneTimeNotifications()
		{
			return _registry.GetOneTimeNotifications();
		}
		
		/// <summary>
		/// Gets scheduled notifications.
		/// </summary>
		public List<NotificationDefinition> GetScheduledNotifications()
		{
			return _registry.GetScheduledNotifications();
		}
		
		/// <summary>
		/// Gets enabled notifications that can be scheduled as native push notifications.
		/// </summary>
		public List<NotificationDefinition> GetEnabledNotifications()
		{
			return _registry.GetEnabledNotifications();
		}
		
		/// <summary>
		/// Gets enabled notifications of a specific type.
		/// </summary>
		public List<NotificationDefinition> GetEnabledNotificationsByType(NotificationType type)
		{
			return _registry.GetEnabledNotificationsByType(type);
		}
		
		/// <summary>
		/// Gets info notifications.
		/// </summary>
		public List<NotificationDefinition> GetInfoNotifications()
		{
			return _registry.GetNotificationsByType(NotificationType.Info)
				.OrderBy(n => n.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets success notifications.
		/// </summary>
		public List<NotificationDefinition> GetSuccessNotifications()
		{
			return _registry.GetNotificationsByType(NotificationType.Success)
				.OrderBy(n => n.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets warning notifications.
		/// </summary>
		public List<NotificationDefinition> GetWarningNotifications()
		{
			return _registry.GetNotificationsByType(NotificationType.Warning)
				.OrderBy(n => n.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets error notifications.
		/// </summary>
		public List<NotificationDefinition> GetErrorNotifications()
		{
			return _registry.GetNotificationsByType(NotificationType.Error)
				.OrderBy(n => n.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets reward notifications.
		/// </summary>
		public List<NotificationDefinition> GetRewardNotifications()
		{
			return _registry.GetNotificationsByType(NotificationType.Reward)
				.OrderBy(n => n.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets event notifications.
		/// </summary>
		public List<NotificationDefinition> GetEventNotifications()
		{
			return _registry.GetNotificationsByType(NotificationType.Event)
				.OrderBy(n => n.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets challenge notifications.
		/// </summary>
		public List<NotificationDefinition> GetChallengeNotifications()
		{
			return _registry.GetNotificationsByType(NotificationType.Challenge)
				.OrderBy(n => n.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets social notifications.
		/// </summary>
		public List<NotificationDefinition> GetSocialNotifications()
		{
			return _registry.GetNotificationsByType(NotificationType.Social)
				.OrderBy(n => n.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets store notifications.
		/// </summary>
		public List<NotificationDefinition> GetStoreNotifications()
		{
			return _registry.GetNotificationsByType(NotificationType.Store)
				.OrderBy(n => n.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets update notifications.
		/// </summary>
		public List<NotificationDefinition> GetUpdateNotifications()
		{
			return _registry.GetNotificationsByType(NotificationType.Update)
				.OrderBy(n => n.Priority)
				.ToList();
		}
		
		/// <summary>
		/// Gets reminder notifications.
		/// </summary>
		public List<NotificationDefinition> GetReminderNotifications()
		{
			return _registry.GetNotificationsByType(NotificationType.Reminder)
				.OrderBy(n => n.Priority)
				.ToList();
		}
	}
}