using System;
using System.Collections;
using System.Collections.Generic;
using AK.Core;
using GameplayCore.MetaData;
using GameplayCore.MetaData.Notifications;
using UnityEngine;

#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace AK.Services
{
	/// <summary>
	/// Main Notification Service implementation using Unity's Mobile Notifications package.
	/// Provides a unified API for scheduling and managing native notifications across Android and iOS.
	/// </summary>
	public class NotificationService : MonoBehaviour, INotificationService
	{
		private bool                         _isInitialized              = false;
		private NotificationPermissionStatus _cachedPermissionStatus     = NotificationPermissionStatus.NotDetermined;
		private NotificationPermissionStatus _lastKnownPermissionStatus  = NotificationPermissionStatus.NotDetermined;
		private bool                         _isWaitingForSettingsReturn = false;

		private NotificationsMeta _notificationsMeta;
		
		public event Action<NotificationData> OnNotificationReceived;
		public event Action<NotificationData> OnNotificationTapped;

		/// <summary>
		/// Event fired when permission status changes (e.g., after returning from system settings).
		/// First parameter is old status, second is new status.
		/// </summary>
		public event Action<NotificationPermissionStatus, NotificationPermissionStatus> OnPermissionStatusChanged;

		/// <summary>
		/// Event fired specifically when permission becomes granted.
		/// </summary>
		public event Action OnPermissionGranted;

		/// <summary>
		/// Initializes the notification service.
		/// </summary>
		public void Initialize(NotificationsMeta notificationsMeta)
		{
			if (_isInitialized)
			{
				Debug.LogWarning("[NotificationService] Already initialized");
				return;
			}

			_notificationsMeta = notificationsMeta;

#if UNITY_ANDROID
			InitializeAndroid();
#elif UNITY_IOS
            InitializeIOS();
#else
            Debug.Log("[NotificationService] Notifications not supported on this platform");
#endif

			_isInitialized = true;
			_lastKnownPermissionStatus = GetPermissionStatus();
			Debug.Log("[NotificationService] Initialization complete");
		}

#if UNITY_ANDROID
		private void InitializeAndroid()
		{
			// Register default notification channel (required for Android 8.0+)
			var channel = new AndroidNotificationChannel
			{
				Id = "default_channel",
				Name = "Default Channel",
				Importance = Importance.Default,
				Description = "Default notifications",
				EnableVibration = true,
				EnableLights = true
			};
			AndroidNotificationCenter.RegisterNotificationChannel(channel);

			// Subscribe to notification events
			AndroidNotificationCenter.OnNotificationReceived += OnAndroidNotificationReceived;

			// Check initial permission status
			_cachedPermissionStatus = GetAndroidPermissionStatus();

			Debug.Log($"[NotificationService] Android initialized with permission status: {_cachedPermissionStatus}");
		}

		private NotificationPermissionStatus GetAndroidPermissionStatus()
		{
			var status = AndroidNotificationCenter.UserPermissionToPost;
			return status switch
			{
				PermissionStatus.Allowed                    => NotificationPermissionStatus.Authorized,
				PermissionStatus.Denied                     => NotificationPermissionStatus.Denied,
				PermissionStatus.DeniedDontAskAgain         => NotificationPermissionStatus.Denied,
				PermissionStatus.NotificationsBlockedForApp => NotificationPermissionStatus.Denied,
				PermissionStatus.NotRequested               => NotificationPermissionStatus.NotDetermined,
				PermissionStatus.RequestPending             => NotificationPermissionStatus.NotDetermined,
				_                                           => NotificationPermissionStatus.NotDetermined
			};
		}

		private void OnAndroidNotificationReceived(AndroidNotificationIntentData data)
		{
			var notificationData = new NotificationData
			{
				Identifier = data.Id.ToString(),
				Title = data.Notification.Title,
				Message = data.Notification.Text,
				Data = DeserializeDictionary(data.Notification.IntentData)
			};
			OnNotificationReceived?.Invoke(notificationData);
		}
#endif

#if UNITY_IOS
        private void InitializeIOS()
        {
            // Subscribe to notification received while app is running
            iOSNotificationCenter.OnNotificationReceived += OnIOSNotificationReceived;

            // Check if app was opened via a notification (cold-start / background tap)
            StartCoroutine(CheckIOSLastRespondedNotification());

            Debug.Log("[NotificationService] iOS initialized");
        }

        private IEnumerator CheckIOSLastRespondedNotification()
        {
            // Must wait at least one frame before querying on cold app start
            yield return null;

            var op = iOSNotificationCenter.QueryLastRespondedNotification();
            yield return op;

            if (op.Notification != null)
            {
                var notification = op.Notification;
                var dict = new Dictionary<string, string>(notification.UserInfo);
                if (!string.IsNullOrEmpty(notification.Data))
                    dict["data"] = notification.Data;

                var notificationData = new NotificationData
                {
                    Identifier = notification.Identifier,
                    Title = notification.Title,
                    Message = notification.Body,
                    Data = dict
                };
                OnNotificationTapped?.Invoke(notificationData);
            }
        }

        private void OnIOSNotificationReceived(iOSNotification notification)
        {
            var notificationData = new NotificationData
            {
                Identifier = notification.Identifier,
                Title = notification.Title,
                Message = notification.Body,
                Data = new Dictionary<string, string>(notification.UserInfo)
            };
            OnNotificationReceived?.Invoke(notificationData);
        }
#endif

		public void RequestPermission(Action<NotificationPermissionStatus> callback)
		{
			if (!_isInitialized)
			{
				Debug.LogWarning("[NotificationService] Service not initialized");
				callback?.Invoke(NotificationPermissionStatus.NotDetermined);
				return;
			}

			// Check if permission is already permanently denied
			// On iOS, the OS itself will prevent showing the dialog again if already denied
			// On Android, the OS tracks "Don't ask again" state
			if (IsPermissionPermanentlyDenied())
			{
				Debug.LogWarning("[NotificationService] Permission is permanently denied. Use OpenAppSettings to guide user to settings.");
				callback?.Invoke(GetPermissionStatus());
				return;
			}

#if UNITY_ANDROID
			StartCoroutine(RequestAndroidPermission(callback));
#elif UNITY_IOS
            StartCoroutine(RequestIOSPermission(callback));
#else
            callback?.Invoke(NotificationPermissionStatus.Denied);
#endif
		}

#if UNITY_ANDROID
		private IEnumerator RequestAndroidPermission(Action<NotificationPermissionStatus> callback)
		{
			var oldStatus = _lastKnownPermissionStatus;
			var request = new PermissionRequest();

			while (request.Status == PermissionStatus.RequestPending)
			{
				yield return null;
			}

			// Map Android PermissionStatus to NotificationPermissionStatus
			_cachedPermissionStatus = request.Status switch
			{
				PermissionStatus.Allowed                    => NotificationPermissionStatus.Authorized,
				PermissionStatus.Denied                     => NotificationPermissionStatus.Denied,
				PermissionStatus.NotificationsBlockedForApp => NotificationPermissionStatus.Denied,
				PermissionStatus.DeniedDontAskAgain         => NotificationPermissionStatus.Denied,
				_                                           => NotificationPermissionStatus.NotDetermined
			};

			Debug.Log($"[NotificationService] Android permission request completed. Status: {_cachedPermissionStatus}");

			// Fire events if status changed
			FirePermissionEvents(oldStatus, _cachedPermissionStatus);

			callback?.Invoke(_cachedPermissionStatus);
		}
#endif

#if UNITY_IOS
        private IEnumerator RequestIOSPermission(Action<NotificationPermissionStatus> callback)
        {
            var oldStatus = _lastKnownPermissionStatus;

            var authorizationOptions = AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound;
            using (var request = new AuthorizationRequest(authorizationOptions, false))
            {
                while (!request.IsFinished)
                    yield return null;

                if (!string.IsNullOrEmpty(request.Error))
                {
                    Debug.LogError($"[NotificationService] Failed to request notification permission: {request.Error}");
                    _cachedPermissionStatus = NotificationPermissionStatus.Denied;
                    callback?.Invoke(NotificationPermissionStatus.Denied);
                    yield break;
                }

                _cachedPermissionStatus = request.Granted ? NotificationPermissionStatus.Authorized : NotificationPermissionStatus.Denied;
                Debug.Log($"[NotificationService] iOS permission request completed. Status: {_cachedPermissionStatus}");

                // Fire events if status changed
                FirePermissionEvents(oldStatus, _cachedPermissionStatus);

                callback?.Invoke(_cachedPermissionStatus);
            }
        }
#endif

		private void FirePermissionEvents(NotificationPermissionStatus oldStatus, NotificationPermissionStatus newStatus)
		{
			if (oldStatus != newStatus)
			{
				Debug.Log($"[NotificationService] Permission status changed: {oldStatus} -> {newStatus}");
				OnPermissionStatusChanged?.Invoke(oldStatus, newStatus);

				if (newStatus == NotificationPermissionStatus.Authorized)
				{
					OnPermissionGranted?.Invoke();
				}
			}

			_lastKnownPermissionStatus = newStatus;
		}

		public NotificationPermissionStatus GetPermissionStatus()
		{
			if (!_isInitialized)
			{
				return NotificationPermissionStatus.NotDetermined;
			}
#if UNITY_EDITOR
			return _cachedPermissionStatus;
#elif UNITY_ANDROID
			_cachedPermissionStatus = GetAndroidPermissionStatus();
#elif UNITY_IOS
            var settings = iOSNotificationCenter.GetNotificationSettings();
            _cachedPermissionStatus = settings.AuthorizationStatus switch
            {
                AuthorizationStatus.Authorized => NotificationPermissionStatus.Authorized,
                AuthorizationStatus.Provisional => NotificationPermissionStatus.Provisional,
                AuthorizationStatus.Denied => NotificationPermissionStatus.Denied,
                _ => NotificationPermissionStatus.NotDetermined
            };
#endif

			return _cachedPermissionStatus;
		}

		private void OnApplicationPause(bool pauseStatus)
		{
			if (!pauseStatus && _isWaitingForSettingsReturn)
			{
				// App resumed from background (returned from settings)
				// Small delay to allow OS to update permission status
				Invoke(nameof(CheckPermissionStatusChange), 0.3f);
			}
		}

		private void CheckPermissionStatusChange()
		{
			var oldStatus = _lastKnownPermissionStatus;
			var newStatus = GetPermissionStatus();

			if (oldStatus != newStatus)
			{
				Debug.Log($"[NotificationService] Permission status changed: {oldStatus} -> {newStatus}");
				OnPermissionStatusChanged?.Invoke(oldStatus, newStatus);

				if (newStatus == NotificationPermissionStatus.Authorized)
				{
					OnPermissionGranted?.Invoke();
				}
			}

			_lastKnownPermissionStatus = newStatus;
			_isWaitingForSettingsReturn = false;
		}

		public void OpenAppSettings(string channelId = null)
		{
			// Track that we're waiting for user to return from settings
			_isWaitingForSettingsReturn = true;
			_lastKnownPermissionStatus = GetPermissionStatus();

#if UNITY_EDITOR
			// In Unity Editor, simulate permission grant after a short delay
			Debug.Log("[NotificationService] Unity Editor: Simulating permission grant from settings");
			Invoke(nameof(SimulatePermissionGrantedInEditor), 1f);
#elif UNITY_ANDROID
            OpenAndroidNotificationSettings(channelId);
#elif UNITY_IOS
            OpenIOSNotificationSettings();
#else
            Debug.LogWarning("[NotificationService] Opening app settings not supported on this platform");
            _isWaitingForSettingsReturn = false;
#endif
		}

#if UNITY_EDITOR
		private void SimulatePermissionGrantedInEditor()
		{
			// Simulate user granting permission in settings
			_cachedPermissionStatus = NotificationPermissionStatus.Authorized;
			_lastKnownPermissionStatus = NotificationPermissionStatus.Authorized;
			_isWaitingForSettingsReturn = false;

			Debug.Log("[NotificationService] Unity Editor: Permission simulated as granted");

			// Fire the permission events
			OnPermissionStatusChanged?.Invoke(NotificationPermissionStatus.NotDetermined, NotificationPermissionStatus.Authorized);
			OnPermissionGranted?.Invoke();
		}
#endif

		public bool ShouldShowPermissionRationale()
		{
#if UNITY_ANDROID
			return AndroidNotificationCenter.ShouldShowPermissionToPostRationale;
#else
            // iOS doesn't have a rationale mechanism
            return false;
#endif
		}

		public bool IsPermissionPermanentlyDenied()
		{
			var status = GetPermissionStatus();

#if UNITY_ANDROID
			// On Android, permission is permanently denied if:
			// 1. User denied and system indicates they don't want to be asked again
			// 2. Notifications are blocked for the app in settings
			// We also check ShouldShowPermissionRationale - if it returns false AND status is Denied,
			// it means user has permanently denied (either via "Don't ask again" or in settings)
			if (status == NotificationPermissionStatus.Denied || status == NotificationPermissionStatus.Restricted)
			{
				// If rationale should not be shown and status is denied, it's permanent
				return !ShouldShowPermissionRationale();
			}

			return false;
#elif UNITY_IOS
            // On iOS, permission is permanently denied if user has denied the request
            // iOS only allows one permission prompt, so any denial is permanent
            return status == NotificationPermissionStatus.Denied ||
                   status == NotificationPermissionStatus.Restricted;
#else
            return false;
#endif
		}

#if UNITY_ANDROID
		private void OpenAndroidNotificationSettings(string channelId)
		{
			AndroidNotificationCenter.OpenNotificationSettings(channelId);
			Debug.Log("[NotificationService] Opening Android notification settings");
		}
#endif

#if UNITY_IOS
        private void OpenIOSNotificationSettings()
        {
            iOSNotificationCenter.OpenNotificationSettings();
            Debug.Log("[NotificationService] Opening iOS notification settings");
        }
#endif

		public void ScheduleNotification(UID notificationUID, DateTime fireTime)
		{
			if (!_isInitialized)
			{
				return;
			}

			// Check permission status first
			var permissionStatus = GetPermissionStatus();
			if (permissionStatus != NotificationPermissionStatus.Authorized)
			{
				Debug.LogWarning($"[NotificationService] Cannot schedule notification - permission not granted. Current status: {permissionStatus}");
				return;
			}

			// Get notification definition from MetaData
			NotificationDefinition notificationDefinition = _notificationsMeta.GetNotification(notificationUID);

			if (notificationDefinition == null)
			{
				Debug.LogWarning($"[NotificationService] Notification definition not found: {notificationUID}");
				return;
			}

			if (!notificationDefinition.IsEnabled)
			{
				Debug.Log($"[NotificationService] Notification is disabled: {notificationUID}");
				return;
			}

			var identifier = notificationUID.ToString();
			var data = new Dictionary<string, string>
			{
				{ "channel_id", notificationUID },
				{ "type", notificationDefinition.Type.ToString() }
			};

			ScheduleNotification(
				notificationDefinition.Title,
				notificationDefinition.Message,
				fireTime,
				identifier,
				data,
				notificationDefinition.IsRepeating && notificationDefinition.RepeatIntervalSeconds > 0
					? TimeSpan.FromSeconds(notificationDefinition.RepeatIntervalSeconds)
					: null
			);

			if (Debug.isDebugBuild)
			{
				var delay = fireTime - DateTime.Now;
				Debug.Log($"[NotificationService] Scheduled notification '{notificationDefinition.DisplayName}' in {delay.TotalMinutes:F1} minutes");
			}
		}

		public void ScheduleNotification(UID notificationUID, int delaySeconds)
		{
			var fireTime = DateTime.Now.AddSeconds(delaySeconds);
			ScheduleNotification(notificationUID, fireTime);
		}

		public void ScheduleNotification(string title, string message, DateTime fireTime, string identifier = null,
		                                 Dictionary<string, string> data = null, TimeSpan? repeatInterval = null)
		{
			if (!_isInitialized)
			{
				return;
			}

			// Check permission status first
			var permissionStatus = GetPermissionStatus();
			if (permissionStatus != NotificationPermissionStatus.Authorized)
			{
				Debug.LogWarning($"[NotificationService] Cannot schedule notification - permission not granted. Current status: {permissionStatus}");
				return;
			}

			if (string.IsNullOrEmpty(identifier))
			{
				identifier = Guid.NewGuid().ToString();
			}

			if (fireTime <= DateTime.Now)
			{
				Debug.LogWarning($"[NotificationService] Fire time is in the past: {fireTime}");
				return;
			}

			// Validate repeat interval (Android has minimum of 1 minute)
			if (repeatInterval.HasValue && repeatInterval.Value.TotalMinutes < 1)
			{
				Debug.LogWarning($"[NotificationService] Repeat interval must be at least 1 minute. Setting to 1 minute.");
				repeatInterval = TimeSpan.FromMinutes(1);
			}

#if UNITY_ANDROID
			ScheduleAndroidNotification(identifier, title, message, fireTime, data, repeatInterval);
#elif UNITY_IOS
            ScheduleIOSNotification(identifier, title, message, fireTime, data, repeatInterval);
#endif

			if (Debug.isDebugBuild)
			{
				var delay = fireTime - DateTime.Now;
				var repeatText = repeatInterval.HasValue ? $" (repeating every {repeatInterval.Value.TotalMinutes:F0} min)" : "";
				Debug.Log($"[NotificationService] Scheduled custom notification '{title}' in {delay.TotalMinutes:F1} minutes{repeatText}");
			}
		}

#if UNITY_ANDROID
		private void ScheduleAndroidNotification(string identifier, string title, string message, DateTime fireTime, Dictionary<string, string> data,
		                                         TimeSpan? repeatInterval)
		{
			var notification = new AndroidNotification
			{
				Title = title,
				Text = message,
				SmallIcon = "icon_0",
				LargeIcon = "app_icon",
				FireTime = fireTime,
				ShowInForeground = true
			};

			// Set repeat interval if provided
			if (repeatInterval.HasValue)
			{
				notification.RepeatInterval = repeatInterval.Value;
			}

			if (data != null && data.Count > 0)
			{
				notification.IntentData = SerializeDictionary(data);
			}

			// Use the provided identifier to prevent duplicate notifications
			// Cancel any existing notification with the same identifier first
			if (!string.IsNullOrEmpty(identifier))
			{
				int notificationId = GetStableNotificationId(identifier);

				// Cancel both scheduled and displayed notifications with this ID
				AndroidNotificationCenter.CancelScheduledNotification(notificationId);
				AndroidNotificationCenter.CancelDisplayedNotification(notificationId);

				// Small delay to ensure cancellation is processed
				AndroidNotificationCenter.SendNotificationWithExplicitID(notification, "default_channel", notificationId);

				if (Debug.isDebugBuild)
				{
					Debug.Log($"[NotificationService] Scheduled Android notification ID: {notificationId} for {fireTime}");
				}
			}
			else
			{
				// Fallback to auto-generated ID if no identifier provided
				AndroidNotificationCenter.SendNotification(notification, "default_channel");
			}
		}
#endif

		/// <summary>
		/// Generates a stable positive integer ID from a string identifier (e.g., GUID).
		/// Android requires int IDs for explicit notification scheduling.
		/// </summary>
		private int GetStableNotificationId(string identifier)
		{
			// Use a stable hash algorithm to ensure consistent IDs across app restarts
			// and guarantee positive values for Android notification IDs
			uint hash = 2166136261u; // FNV-1a 32-bit offset basis
			foreach (char c in identifier)
			{
				hash ^= c;
				hash *= 16777619u; // FNV-1a 32-bit prime
			}

			// Ensure positive int (Android notification IDs must be positive)
			return (int)(hash & 0x7FFFFFFF);
		}

#if UNITY_IOS
        private void ScheduleIOSNotification(string identifier, string title, string message, DateTime fireTime, Dictionary<string, string> data, TimeSpan? repeatInterval)
        {
            var timeTrigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = fireTime - DateTime.Now,
                Repeats = repeatInterval.HasValue
            };

            var notification = new iOSNotification
            {
                Identifier = identifier,
                Title = title,
                Body = message,
                ShowInForeground = true,
                ForegroundPresentationOption = PresentationOption.Alert | PresentationOption.Sound,
                Trigger = timeTrigger
            };

            if (data != null)
            {
                foreach (var kvp in data)
                {
                    notification.UserInfo[kvp.Key] = kvp.Value;
                }
            }

            iOSNotificationCenter.ScheduleNotification(notification);
        }
#endif

		public void CancelNotification(string identifier)
		{
			if (!_isInitialized)
			{
				return;
			}

#if UNITY_ANDROID
			// Cancel notification by identifier using the same ID generation as scheduling
			if (!string.IsNullOrEmpty(identifier))
			{
				int notificationId = GetStableNotificationId(identifier);
				AndroidNotificationCenter.CancelScheduledNotification(notificationId);
				AndroidNotificationCenter.CancelDisplayedNotification(notificationId);
			}
#elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(identifier);
            iOSNotificationCenter.RemoveDeliveredNotification(identifier);
#endif

			if (Debug.isDebugBuild)
			{
				Debug.Log($"[NotificationService] Cancelled notification: {identifier}");
			}
		}

		public void CancelAllNotifications()
		{
			if (!_isInitialized)
			{
				return;
			}

#if UNITY_ANDROID
			AndroidNotificationCenter.CancelAllScheduledNotifications();
			AndroidNotificationCenter.CancelAllDisplayedNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
#endif

			if (Debug.isDebugBuild)
			{
				Debug.Log("[NotificationService] Cancelled all notifications");
			}
		}

		public List<string> GetScheduledNotifications()
		{
			var scheduled = new List<string>();

			if (!_isInitialized)
			{
				return scheduled;
			}

#if UNITY_IOS
            var notifications = iOSNotificationCenter.GetScheduledNotifications();
            foreach (var notification in notifications)
            {
                scheduled.Add(notification.Identifier);
            }
#elif UNITY_ANDROID
			Debug.LogWarning("[NotificationService] GetScheduledNotifications not supported on Android");
#endif

			return scheduled;
		}

		public void ClearAllNotifications()
		{
			if (!_isInitialized)
			{
				return;
			}

#if UNITY_ANDROID
			AndroidNotificationCenter.CancelAllScheduledNotifications();
			AndroidNotificationCenter.CancelAllDisplayedNotifications();
#elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
#endif

			if (Debug.isDebugBuild)
			{
				Debug.Log("[NotificationService] Cleared all scheduled and displayed notifications");
			}
		}

		public void SetApplicationBadgeNumber(int badgeNumber)
		{
			if (!_isInitialized)
			{
				return;
			}

#if UNITY_IOS
            iOSNotificationCenter.ApplicationBadge = badgeNumber;
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[NotificationService] Set application badge to: {badgeNumber}");
            }
#elif UNITY_ANDROID
			Debug.LogWarning("[NotificationService] Application badges not supported on Android");
#endif
		}

		public int GetApplicationBadgeNumber()
		{
			if (!_isInitialized)
			{
				return 0;
			}

#if UNITY_IOS
            return iOSNotificationCenter.ApplicationBadge;
#else
			return 0;
#endif
		}

		/// <summary>
		/// Serializes a dictionary to a string for Android IntentData.
		/// </summary>
		private string SerializeDictionary(Dictionary<string, string> data)
		{
			if (data == null || data.Count == 0)
				return string.Empty;

			var pairs = new List<string>();
			foreach (var kvp in data)
			{
				pairs.Add($"{kvp.Key}={kvp.Value}");
			}

			return string.Join("&", pairs);
		}

		/// <summary>
		/// Deserializes a string to a dictionary.
		/// </summary>
		private Dictionary<string, string> DeserializeDictionary(string data)
		{
			var result = new Dictionary<string, string>();
			if (string.IsNullOrEmpty(data))
				return result;

			var pairs = data.Split('&');
			foreach (var pair in pairs)
			{
				var parts = pair.Split('=');
				if (parts.Length == 2)
				{
					result[parts[0]] = parts[1];
				}
			}

			return result;
		}

		private void OnDestroy()
		{
#if UNITY_ANDROID
			AndroidNotificationCenter.OnNotificationReceived -= OnAndroidNotificationReceived;
#elif UNITY_IOS
            iOSNotificationCenter.OnNotificationReceived -= OnIOSNotificationReceived;
#endif
		}
	}
}