using System;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// Creates the notification service. The Mobile Notifications adapter
	/// (AK.Services.MobileNotifications) is compiled only when com.unity.mobile.notifications 2.4
	/// or later is installed, and only for Android, iOS and the editor. It registers itself here,
	/// so games create the service without referencing it or guarding with #if.
	/// </summary>
	public static class NotificationServiceFactory
	{
		/// <summary>
		/// Adds a Mobile Notifications service to a host object. Set by
		/// AK.Services.MobileNotifications at startup; null without it.
		/// </summary>
		public static Func<GameObject, INotificationService> MobileNotifications { get; set; }

		/// <summary>
		/// A Mobile Notifications service on <paramref name="host"/>, which owns it, or a
		/// <see cref="NullNotificationService"/> when the package isn't installed or the platform
		/// isn't Android or iOS.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="host"/> is null or destroyed.</exception>
		public static INotificationService Create(GameObject host)
		{
			if (host == null)
			{
				throw new ArgumentNullException(nameof(host));
			}

			return MobileNotifications?.Invoke(host) ?? new NullNotificationService();
		}
	}
}
