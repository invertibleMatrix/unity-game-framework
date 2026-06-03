using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Rewards;
using UnityEngine;

namespace AK.CoreDomain.Notifications
{
	/// <summary>
	/// Defines a notification with content, timing, and behavior.
	/// Notifications are used to inform players about game events, rewards, and updates.
	/// </summary>
	[CreateAssetMenu(fileName = "NotificationDefinition", menuName = "Gameplay/MetaData/Notifications/NotificationDefinition")]
	public class NotificationDefinition : MetaDataAsset
	{
		[Tooltip("Internal name for reference")]
		public string InternalName;

		[Header("Classification")] [Tooltip("Type of notification")]
		public NotificationType Type;

		[Tooltip("Priority for displaying notifications (lower = higher priority)")] [Range(0, 100)]
		public int Priority = 50;

		[Header("Content")] [Tooltip("Title text shown to the player")] [TextArea(1, 2)]
		public string Title;

		[Tooltip("Message text shown to the player")] [TextArea(2, 5)]
		public string Message;

		[Tooltip("Background image for the notification")]
		public Sprite BackgroundImage;

		[Tooltip("Theme color for the notification")]
		public Color ThemeColor = Color.white;

		[Header("Timing")] [Tooltip("Is this notification scheduled")]
		public bool IsScheduled = false;

		[Tooltip("Is this notification enabled for native push notifications")]
		public bool IsEnabled = true;

		public bool IsRepeating;
		public int  RepeatIntervalSeconds;

		[Tooltip("Delay before showing notification (in seconds)")] [Min(0)]
		public float ShowDelay = 0f;

		[Tooltip("Auto-dismiss after this time (0 = manual)")] [Min(0)]
		public float AutoDismissTime = 0f;

		[Tooltip("Minimum time between showing again (in seconds)")] [Min(0)]
		public float CooldownTime = 0f;

		[Header("Trigger Conditions")] [Tooltip("Minimum player level required")] [Min(1)]
		public int MinPlayerLevel = 1;

		[Tooltip("Maximum player level (0 = unlimited)")] [Min(0)]
		public int MaxPlayerLevel = 0;

		[Tooltip("Show only once per player")]
		public bool ShowOnce = true;

		[Tooltip("Can be dismissed by the player")]
		public bool CanDismiss = true;

		[Tooltip("Can be snoozed (shown again later)")]
		public bool CanSnooze = false;

		[Tooltip("Snooze duration (in seconds)")] [Min(60)]
		public int SnoozeDuration = 300;

		[Header("Actions")] [Tooltip("Has action button")]
		public bool HasAction = false;

		[Tooltip("Action button text")]
		public string ActionButtonText = "Claim";

		[Tooltip("Action button target (UID of screen or action)")]
		public UID ActionTarget;

		[Tooltip("Has secondary action button")]
		public bool HasSecondaryAction = false;

		[Tooltip("Secondary action button text")]
		public string SecondaryActionButtonText = "Dismiss";

		[Tooltip("Secondary action button target")]
		public UID SecondaryActionTarget;

		[Header("Rewards")] [Tooltip("Rewards granted when action is clicked")]
		public List<RewardDefinition> Rewards = new();

		[Tooltip("Reward multiplier")] [Range(0.5f, 3f)]
		public float RewardMultiplier = 1f;

		[Header("Sound")] [Tooltip("Play sound when notification shows")]
		public bool PlaySound = true;

		[Tooltip("Sound to play")]
		public UID SoundId;

		[Tooltip("Sound volume")] [Range(0f, 1f)]
		public float SoundVolume = 1f;

		[Header("Vibration")] [Tooltip("Vibrate when notification shows")]
		public bool Vibrate = false;

		[Tooltip("Vibration pattern (0 = default, 1 = light, 2 = medium, 3 = heavy)")] [Range(0, 3)]
		public int VibrationPattern = 0;

		[Header("Analytics")] [Tooltip("Analytics event when notification shows")]
		public UID ShowEvent;

		[Tooltip("Analytics event when action is clicked")]
		public UID ActionEvent;

		[Tooltip("Analytics event when notification is dismissed")]
		public UID DismissEvent;

		[Tooltip("Analytics event when notification is snoozed")]
		public UID SnoozeEvent;

		public UID UniqueID => this;

		/// <summary>
		/// Gets all rewards from this notification definition.
		/// </summary>
		public List<RewardDefinition> GetAllRewards()
		{
			List<RewardDefinition> allRewards = new List<RewardDefinition>();

			foreach (var reward in Rewards)
			{
				if (reward != null)
				{
					allRewards.Add(reward);
				}
			}

			return allRewards;
		}

		/// <summary>
		/// Checks if this notification should be shown based on player level.
		/// </summary>
		public bool ShouldShowForPlayerLevel(int playerLevel)
		{
			if (playerLevel < MinPlayerLevel) return false;
			if (MaxPlayerLevel > 0 && playerLevel > MaxPlayerLevel) return false;
			return true;
		}
	}
}