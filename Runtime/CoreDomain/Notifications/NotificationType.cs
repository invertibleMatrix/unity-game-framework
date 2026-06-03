using System;

namespace AK.CoreDomain.Notifications
{
	/// <summary>
	/// Defines the type of notification for categorization and filtering.
	/// </summary>
	[Serializable]
	public enum NotificationType
	{
		/// <summary>
	/// General information notification.
		/// </summary>
		Info,
		
		/// <summary>
	/// Success/achievement notification.
		/// </summary>
		Success,
		
		/// <summary>
	/// Warning notification.
		/// </summary>
		Warning,
		
		/// <summary>
	/// Error notification.
		/// </summary>
		Error,
		
		/// <summary>
	/// Reward notification.
		/// </summary>
		Reward,
		
		/// <summary>
	/// Event notification.
		/// </summary>
		Event,
		
		/// <summary>
	/// Challenge notification.
		/// </summary>
		Challenge,
		
		/// <summary>
	/// Social notification (friend request, etc.).
		/// </summary>
		Social,
		
		/// <summary>
	/// Store/IAP notification.
		/// </summary>
		Store,
		
		/// <summary>
	/// Update notification.
	/// </summary>
		Update,
		
		/// <summary>
	/// Reminder notification.
		/// </summary>
		Reminder,
		
		/// <summary>
	/// Custom notification type.
		/// </summary>
		Custom
	}
}