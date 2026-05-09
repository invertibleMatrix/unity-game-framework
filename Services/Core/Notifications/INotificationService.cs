using System;
using System.Collections.Generic;
using AK.Core;
using GameplayCore.MetaData.Notifications;

namespace AK.Services
{
	/// <summary>
	/// Main interface for the Notification Service.
	/// Provides a unified API for scheduling and managing native notifications across Android and iOS.
	/// </summary>
	public interface INotificationService
	{
		/// <summary>
		/// Initializes the notification service with all configured providers.
		/// </summary>
		void Initialize(NotificationsMeta notificationsMeta);

		/// <summary>
		/// Requests notification permission from the user.
		/// </summary>
		/// <param name="callback">Callback with the permission status result</param>
		void RequestPermission(Action<NotificationPermissionStatus> callback);

		/// <summary>
		/// Checks the current notification permission status.
		/// </summary>
		/// <returns>The current permission status</returns>
		NotificationPermissionStatus GetPermissionStatus();

		/// <summary>
		/// Opens the application settings page where users can enable notifications.
		/// Use this when user has denied permission and you need to guide them to settings.
		/// On Android, opens notification settings for the app (or specific channel if channelId is provided).
		/// On iOS 15.4+, opens notification settings directly. On earlier iOS versions, opens app settings.
		/// Note: This will suspend your application and switch to the Settings app.
		/// </summary>
		/// <param name="channelId">Optional channel ID for Android-specific channel settings (Android 8.0+)</param>
		void OpenAppSettings(string channelId = null);

		/// <summary>
		/// Checks if the app should show a permission rationale dialog before requesting permission.
		/// On Android, returns true if the user previously denied the permission but didn't select "Don't ask again".
		/// On iOS, always returns false as iOS doesn't provide this mechanism.
		/// </summary>
		/// <returns>True if a rationale should be shown before requesting permission</returns>
		bool ShouldShowPermissionRationale();

		/// <summary>
		/// Checks if the user has permanently denied notification permission or if notifications are blocked.
		/// This is useful to determine if you should show a dialog prompting the user to open settings.
		/// </summary>
		/// <returns>True if permission is permanently denied or blocked</returns>
		bool IsPermissionPermanentlyDenied();

		/// <summary>
		/// Schedules a notification by its UID.
		/// </summary>
		/// <param name="notificationUID">The UID of the notification definition</param>
		/// <param name="fireTime">When to fire the notification</param>
		void ScheduleNotification(UID notificationUID, DateTime fireTime);

		/// <summary>
		/// Schedules a notification by its UID with a delay.
		/// </summary>
		/// <param name="notificationUID">The UID of the notification definition</param>
		/// <param name="delaySeconds">Delay in seconds before firing</param>
		void ScheduleNotification(UID notificationUID, int delaySeconds);

		/// <summary>
		/// Schedules a notification with custom content.
		/// </summary>
		/// <param name="title">Notification title</param>
		/// <param name="message">Notification message</param>
		/// <param name="fireTime">When to fire the notification</param>
		/// <param name="identifier">Optional unique identifier for the notification</param>
		/// <param name="data">Optional custom data dictionary</param>
		/// <param name="repeatInterval">Optional repeat interval for recurring notifications (minimum 1 minute on Android)</param>
		void ScheduleNotification(string title, string message, DateTime fireTime, string identifier = null, Dictionary<string, string> data = null, TimeSpan? repeatInterval = null);

		/// <summary>
		/// Cancels a scheduled notification by its identifier.
		/// </summary>
		/// <param name="identifier">The notification identifier</param>
		void CancelNotification(string identifier);

		/// <summary>
		/// Cancels all scheduled notifications.
		/// </summary>
		void CancelAllNotifications();

		/// <summary>
		/// Gets all scheduled notifications.
		/// </summary>
		/// <returns>List of scheduled notification identifiers</returns>
		List<string> GetScheduledNotifications();

		/// <summary>
		/// Clears all delivered notifications from the notification center.
		/// </summary>
		void ClearAllNotifications();

		/// <summary>
		/// Sets the application badge number.
		/// </summary>
		/// <param name="badgeNumber">The badge number to set</param>
		void SetApplicationBadgeNumber(int badgeNumber);

		/// <summary>
		/// Gets the current application badge number.
		/// </summary>
		/// <returns>The current badge number</returns>
		int GetApplicationBadgeNumber();

		/// <summary>
		/// Event fired when a notification is received while the app is in the foreground.
		/// </summary>
		event Action<NotificationData> OnNotificationReceived;

		/// <summary>
		/// Event fired when a notification is tapped by the user.
		/// </summary>
		event Action<NotificationData> OnNotificationTapped;

		/// <summary>
		/// Event fired when permission status changes (e.g., after returning from system settings).
		/// First parameter is old status, second is new status.
		/// </summary>
		event Action<NotificationPermissionStatus, NotificationPermissionStatus> OnPermissionStatusChanged;

		/// <summary>
		/// Event fired specifically when permission becomes granted.
		/// </summary>
		event Action OnPermissionGranted;
	}

	/// <summary>
	/// Represents the permission status for notifications.
	/// </summary>
	public enum NotificationPermissionStatus
	{
		/// <summary>
		/// Permission has not been requested yet.
		/// </summary>
		NotDetermined,

		/// <summary>
		/// Permission has been denied.
		/// </summary>
		Denied,

		/// <summary>
		/// Permission has been granted.
		/// </summary>
		Authorized,

		/// <summary>
		/// Permission is provisionally authorized (iOS only).
		/// </summary>
		Provisional,

		/// <summary>
		/// Permission is restricted (e.g., parental controls).
		/// </summary>
		Restricted
	}

	/// <summary>
	/// Data structure for notification events.
	/// </summary>
	public class NotificationData
	{
		/// <summary>
		/// The notification identifier.
		/// </summary>
		public string Identifier { get; set; }

		/// <summary>
		/// The notification title.
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// The notification message/body.
		/// </summary>
		public string Message { get; set; }

		/// <summary>
		/// The notification UID from metadata (if applicable).
		/// </summary>
		public UID NotificationUID { get; set; }

		/// <summary>
		/// Additional data associated with the notification.
		/// </summary>
		public Dictionary<string, string> Data { get; set; }
	}
}
