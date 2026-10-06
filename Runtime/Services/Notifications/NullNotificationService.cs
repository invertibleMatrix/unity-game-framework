using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Notifications;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// No-op <see cref="INotificationService"/>, which <see cref="NotificationServiceFactory"/> makes
	/// when com.unity.mobile.notifications 2.4 or later isn't installed, or in a player for a
	/// platform other than Android and iOS. Lets consumer code hold an INotificationService
	/// reference without #if guards: everything degrades gracefully.
	/// </summary>
	public class NullNotificationService : INotificationService
	{
		public event Action<NotificationData> OnNotificationReceived
		{
			add { }
			remove { }
		}

		public event Action<NotificationData> OnNotificationTapped
		{
			add { }
			remove { }
		}

		public event Action<NotificationPermissionStatus, NotificationPermissionStatus> OnPermissionStatusChanged
		{
			add { }
			remove { }
		}

		public event Action OnPermissionGranted
		{
			add { }
			remove { }
		}

		public void Initialize(NotificationsMeta notificationsMeta)
		{
			Debug.Log("[NullNotificationService] Notifications unavailable (package missing or unsupported platform).");
		}

		public void RequestPermission(Action<NotificationPermissionStatus> callback)
		{
			callback?.Invoke(NotificationPermissionStatus.Denied);
		}

		public NotificationPermissionStatus GetPermissionStatus() => NotificationPermissionStatus.Denied;

		public void OpenAppSettings(string channelId = null) { }

		public bool ShouldShowPermissionRationale() => false;

		public bool IsPermissionPermanentlyDenied() => false;

		public void ScheduleNotification(Uid<NotificationDefinition> notificationId, DateTime fireTime) => WarnNoBackend();

		public void ScheduleNotification(Uid<NotificationDefinition> notificationId, int delaySeconds) => WarnNoBackend();

		public void ScheduleNotification(NotificationDefinition definition, DateTime fireTime) => WarnNoBackend();

		public void ScheduleNotification(NotificationDefinition definition, int delaySeconds) => WarnNoBackend();

		private static void WarnNoBackend()
		{
			Debug.LogWarning("[NullNotificationService] ScheduleNotification called with no notifications backend.");
		}

		public void ScheduleNotification(string title, string message, DateTime fireTime, string identifier = null,
		                                 Dictionary<string, string> data = null, TimeSpan? repeatInterval = null)
		{
			Debug.LogWarning("[NullNotificationService] ScheduleNotification called with no notifications backend.");
		}

		public void CancelNotification(string identifier) { }

		public void CancelAllNotifications() { }

		public List<string> GetScheduledNotifications() => new();

		public void ClearAllNotifications() { }

		public void SetApplicationBadgeNumber(int badgeNumber) { }

		public int GetApplicationBadgeNumber() => 0;
	}
}
