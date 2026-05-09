# Notification System Documentation

## Overview

The Notification System provides a comprehensive framework for defining and managing in-game notifications. It follows the same architectural patterns as other metadata systems in your codebase, using ScriptableObjects for data-driven configuration and UID-based lookups.

## Architecture

### Core Components

1. **NotificationType** - Enum defining notification categories
2. **NotificationDefinition** - Main notification definition with content, timing, and behavior
3. **NotificationsRegistry** - UID-based registry for notification definitions
4. **NotificationsMeta** - Container with query methods for notifications

### Notification Types

| Type | Description |
|------|-------------|
| `Info` | General information notification |
| `Success` | Success/achievement notification |
| `Warning` | Warning notification |
| `Error` | Error notification |
| `Reward` | Reward notification |
| `Event` | Event notification |
| `Challenge` | Challenge notification |
| `Social` | Social notification (friend request, etc.) |
| `Store` | Store/IAP notification |
| `Update` | Update notification |
| `Reminder` | Reminder notification |
| `Custom` | Custom notification type |

## NotificationDefinition Structure

### Identification
- **UID**: Unique identifier for the notification
- **DisplayName**: Display name shown to players
- **InternalName**: Internal name for reference

### Classification
- **Type**: Notification category (NotificationType enum)
- **Priority**: Display priority (0-100, lower = higher priority)

### Content
- **Title**: Title text shown to player
- **Message**: Message text shown to player
- **Icon**: Icon for the notification
- **BackgroundImage**: Background image for the notification
- **ThemeColor**: Theme color for the notification

### Timing
- **IsScheduled**: Is this notification scheduled
- **ShowDelay**: Delay before showing notification (seconds)
- **AutoDismissTime**: Auto-dismiss after this time (0 = manual)
- **CooldownTime**: Minimum time between showing again (seconds)

### Trigger Conditions
- **MinPlayerLevel**: Minimum player level required
- **MaxPlayerLevel**: Maximum player level (0 = unlimited)
- **ShowOnce**: Show only once per player
- **CanDismiss**: Can be dismissed by player
- **CanSnooze**: Can be snoozed (shown again later)
- **SnoozeDuration**: Snooze duration in seconds (60+)

### Actions
- **HasAction**: Has action button
- **ActionButtonText**: Action button text
- **ActionTarget**: Action button target (UID of screen or action)
- **HasSecondaryAction**: Has secondary action button
- **SecondaryActionButtonText**: Secondary action button text
- **SecondaryActionTarget**: Secondary action button target

### Rewards
- **Rewards**: Rewards granted when action is clicked
- **RewardMultiplier**: Reward multiplier (0.5-3)

### Sound
- **PlaySound**: Play sound when notification shows
- **SoundId**: Sound to play
- **SoundVolume**: Sound volume (0-1)

### Vibration
- **Vibrate**: Vibrate when notification shows
- **VibrationPattern**: Vibration pattern (0-3: default, light, medium, heavy)

### Analytics
- **ShowEvent**: Analytics event when notification shows
- **ActionEvent**: Analytics event when action is clicked
- **DismissEvent**: Analytics event when notification is dismissed
- **SnoozeEvent**: Analytics event when notification is snoozed

## Usage Examples

### Creating a Notification Definition

```csharp
// Create a new NotificationDefinition asset
NotificationDefinition notification = ScriptableObject.CreateInstance<NotificationDefinition>();
notification.UID = new UID("notification_level_complete");
notification.DisplayName = "Level Complete";
notification.InternalName = "level_complete_notification";
notification.Type = NotificationType.Success;
notification.Priority = 10;
notification.MinPlayerLevel = 1;
notification.MaxPlayerLevel = 0;
notification.ShowOnce = false;
notification.CanDismiss = true;
notification.CanSnooze = false;
notification.Title = "Level Complete!";
notification.Message = "Congratulations! You've completed the level.";
notification.Icon = levelCompleteIcon;
notification.BackgroundImage = levelCompleteBackground;
notification.ThemeColor = Color.green;
notification.ShowDelay = 0.5f;
notification.AutoDismissTime = 5f;
notification.CooldownTime = 0f;
notification.HasAction = true;
notification.ActionButtonText = "Claim Rewards";
notification.ActionTarget = new UID("screen_rewards");
notification.HasSecondaryAction = true;
notification.SecondaryActionButtonText = "Next Level";
notification.SecondaryActionTarget = new UID("screen_next_level");
notification.Rewards.Add(rewardDefinition);
notification.RewardMultiplier = 1f;
notification.PlaySound = true;
notification.SoundId = new UID("sfx_level_complete");
notification.SoundVolume = 1f;
notification.Vibrate = true;
notification.VibrationPattern = 1;
notification.ShowEvent = new UID("event_notification_show");
notification.ActionEvent = new UID("event_notification_action");
notification.DismissEvent = new UID("event_notification_dismiss");
```

### Querying Notifications

```csharp
// Get notification by UID
NotificationDefinition notification = metaDataRepository.NotificationsMeta.GetNotification(new UID("notification_level_complete"));

// Get all notifications
IReadOnlyList<NotificationDefinition> allNotifications = metaDataRepository.NotificationsMeta.GetAllNotifications();

// Get notifications by type
List<NotificationDefinition> successNotifications = metaDataRepository.NotificationsMeta.GetNotificationsByType(NotificationType.Success);

// Get notifications for a specific player level
List<NotificationDefinition> availableNotifications = metaDataRepository.NotificationsMeta.GetNotificationsForPlayerLevel(5);

// Get notifications sorted by priority
List<NotificationDefinition> priorityNotifications = metaDataRepository.NotificationsMeta.GetNotificationsByPriority();

// Get dismissible notifications
List<NotificationDefinition> dismissibleNotifications = metaDataRepository.NotificationsMeta.GetDismissibleNotifications();

// Get snoozable notifications
List<NotificationDefinition> snoozableNotifications = metaDataRepository.NotificationsMeta.GetSnoozableNotifications();

// Get notifications with actions
List<NotificationDefinition> actionNotifications = metaDataRepository.NotificationsMeta.GetNotificationsWithActions();

// Get notifications with rewards
List<NotificationDefinition> rewardNotifications = metaDataRepository.NotificationsMeta.GetNotificationsWithRewards();

// Get one-time notifications
List<NotificationDefinition> oneTimeNotifications = metaDataRepository.NotificationsMeta.GetOneTimeNotifications();

// Get scheduled notifications
List<NotificationDefinition> scheduledNotifications = metaDataRepository.NotificationsMeta.GetScheduledNotifications();
```

### Getting Specific Notification Types

```csharp
// Get info notifications
List<NotificationDefinition> infoNotifications = metaDataRepository.NotificationsMeta.GetInfoNotifications();

// Get success notifications
List<NotificationDefinition> successNotifications = metaDataRepository.NotificationsMeta.GetSuccessNotifications();

// Get warning notifications
List<NotificationDefinition> warningNotifications = metaDataRepository.NotificationsMeta.GetWarningNotifications();

// Get error notifications
List<NotificationDefinition> errorNotifications = metaDataRepository.NotificationsMeta.GetErrorNotifications();

// Get reward notifications
List<NotificationDefinition> rewardNotifications = metaDataRepository.NotificationsMeta.GetRewardNotifications();

// Get event notifications
List<NotificationDefinition> eventNotifications = metaDataRepository.NotificationsMeta.GetEventNotifications();

// Get challenge notifications
List<NotificationDefinition> challengeNotifications = metaDataRepository.NotificationsMeta.GetChallengeNotifications();

// Get social notifications
List<NotificationDefinition> socialNotifications = metaDataRepository.NotificationsMeta.GetSocialNotifications();

// Get store notifications
List<NotificationDefinition> storeNotifications = metaDataRepository.NotificationsMeta.GetStoreNotifications();

// Get update notifications
List<NotificationDefinition> updateNotifications = metaDataRepository.NotificationsMeta.GetUpdateNotifications();

// Get reminder notifications
List<NotificationDefinition> reminderNotifications = metaDataRepository.NotificationsMeta.GetReminderNotifications();
```

### Using Notifications in Gameplay

```csharp
public class NotificationManager : MonoBehaviour
{
    private IMetaDataRepository _metaDataRepository;
    private PlayerData _playerData;
    
    public void ShowNotification(UID notificationUID)
    {
        // Get notification definition
        NotificationDefinition notification = _metaDataRepository.NotificationsMeta.GetNotification(notificationUID);
        
        if (notification == null)
        {
            Debug.LogError($"Notification not found: {notificationUID}");
            return;
        }
        
        // Check if notification should be shown
        if (!notification.ShouldShowForPlayerLevel(_playerData.PlayerLevel))
        {
            return;
        }
        
        // Check if notification has been shown before
        if (notification.ShowOnce && _playerData.ShownNotifications.Contains(notification.UID))
        {
            return;
        }
        
        // Show notification after delay
        StartCoroutine(ShowNotificationWithDelay(notification));
    }
    
    private IEnumerator ShowNotificationWithDelay(NotificationDefinition notification)
    {
        yield return new WaitForSeconds(notification.ShowDelay);
        
        // Show notification UI
        ShowNotificationUI(notification);
        
        // Mark as shown
        if (notification.ShowOnce)
        {
            _playerData.ShownNotifications.Add(notification.UID);
        }
        
        // Track analytics
        if (notification.ShowEvent != null && !notification.ShowEvent.IsEmpty())
        {
            AnalyticsManager.LogEvent(notification.ShowEvent);
        }
        
        // Play sound
        if (notification.PlaySound && notification.SoundId != null && !notification.SoundId.IsEmpty())
        {
            AudioManager.PlaySound(notification.SoundId, notification.SoundVolume);
        }
        
        // Vibrate
        if (notification.Vibrate)
        {
            VibrationManager.Vibrate(notification.VibrationPattern);
        }
        
        // Auto-dismiss if configured
        if (notification.AutoDismissTime > 0)
        {
            yield return new WaitForSeconds(notification.AutoDismissTime);
            DismissNotification(notification);
        }
    }
    
    public void OnNotificationAction(NotificationDefinition notification)
    {
        // Grant rewards
        List<RewardDefinition> rewards = notification.GetAllRewards();
        foreach (var reward in rewards)
        {
            RewardManager.GrantReward(reward, notification.RewardMultiplier);
        }
        
        // Track analytics
        if (notification.ActionEvent != null && !notification.ActionEvent.IsEmpty())
        {
            AnalyticsManager.LogEvent(notification.ActionEvent);
        }
        
        // Navigate to action target
        if (notification.ActionTarget != null && !notification.ActionTarget.IsEmpty())
        {
            NavigationManager.NavigateTo(notification.ActionTarget);
        }
        
        // Dismiss notification
        DismissNotification(notification);
    }
    
    public void OnNotificationDismiss(NotificationDefinition notification)
    {
        // Track analytics
        if (notification.DismissEvent != null && !notification.DismissEvent.IsEmpty())
        {
            AnalyticsManager.LogEvent(notification.DismissEvent);
        }
        
        // Dismiss notification UI
        DismissNotificationUI(notification);
    }
    
    public void OnNotificationSnooze(NotificationDefinition notification)
    {
        // Track analytics
        if (notification.SnoozeEvent != null && !notification.SnoozeEvent.IsEmpty())
        {
            AnalyticsManager.LogEvent(notification.SnoozeEvent);
        }
        
        // Schedule notification to show again after snooze duration
        StartCoroutine(ScheduleNotificationAfterDelay(notification, notification.SnoozeDuration));
    }
    
    private IEnumerator ScheduleNotificationAfterDelay(NotificationDefinition notification, float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowNotification(notification.UID);
    }
    
    private void ShowNotificationUI(NotificationDefinition notification)
    {
        // Create and show notification UI
        NotificationUI notificationUI = NotificationUI.Create(notification);
        notificationUI.Show();
    }
    
    private void DismissNotification(NotificationDefinition notification)
    {
        // Dismiss notification UI
        NotificationUI.Dismiss(notification.UID);
    }
    
    private void DismissNotificationUI(NotificationDefinition notification)
    {
        // Dismiss notification UI
        NotificationUI.DismissAll();
    }
}
```

### Checking Notification Conditions

```csharp
NotificationDefinition notification = metaDataRepository.NotificationsMeta.GetNotification(new UID("notification_level_complete"));

// Check if notification should be shown for player level
bool shouldShow = notification.ShouldShowForPlayerLevel(playerLevel);

// Get all rewards from notification
List<RewardDefinition> rewards = notification.GetAllRewards();
```

## Integration with MetaDataRepository

The Notification System is integrated into the existing [`IMetaDataRepository`](../IMetaDataRepository.cs) and [`MetaDataRepository`](../MetaDataRepository.cs):

```csharp
// In IMetaDataRepository
public NotificationsRegistry NotificationsRegistry { get; }
public NotificationsMeta NotificationsMeta { get; }

// In MetaDataRepository
[SerializeField] private NotificationsRegistry _notificationsRegistry;
[SerializeField] private NotificationsMeta _notificationsMeta;
public NotificationsRegistry NotificationsRegistry => _notificationsRegistry;
public NotificationsMeta NotificationsMeta => _notificationsMeta;

// In GetObjectByUID
if (typeof(T) == typeof(NotificationDefinition))
{
    return _notificationsMeta.Registry.Definitions.FirstOrDefault(n => n.UID == uid) as T;
}
```

## Best Practices

1. **Use Clear Messages**: Write clear, concise notification messages
2. **Set Appropriate Priorities**: Lower priority notifications are shown first
3. **Use Appropriate Types**: Choose the right notification type for the context
4. **Set Reasonable Delays**: Use delays to avoid overwhelming players
5. **Provide Clear Actions**: Make action buttons clear and actionable
6. **Use Visual Cues**: Use icons and colors to convey notification type
7. **Track Analytics**: Use analytics to measure notification effectiveness
8. **Respect Player Preferences**: Allow players to dismiss or snooze notifications
9. **Test Thoroughly**: Test notifications at different player levels
10. **Avoid Spam**: Use cooldowns to prevent notification spam

## Common Use Cases

### Level Complete Notification

```csharp
NotificationDefinition levelComplete = new NotificationDefinition
{
    Type = NotificationType.Success,
    Priority = 10,
    MinPlayerLevel = 1,
    ShowOnce = false,
    CanDismiss = true,
    CanSnooze = false,
    Title = "Level Complete!",
    Message = "Congratulations! You've completed the level.",
    ShowDelay = 0.5f,
    AutoDismissTime = 5f,
    HasAction = true,
    ActionButtonText = "Claim Rewards",
    ActionTarget = new UID("screen_rewards"),
    HasSecondaryAction = true,
    SecondaryActionButtonText = "Next Level",
    SecondaryActionTarget = new UID("screen_next_level"),
    RewardMultiplier = 1f,
    PlaySound = true,
    SoundId = new UID("sfx_level_complete"),
    SoundVolume = 1f,
    Vibrate = true,
    VibrationPattern = 1
};
```

### Reward Notification

```csharp
NotificationDefinition rewardNotification = new NotificationDefinition
{
    Type = NotificationType.Reward,
    Priority = 5,
    MinPlayerLevel = 1,
    ShowOnce = false,
    CanDismiss = true,
    CanSnooze = true,
    Title = "Reward Available!",
    Message = "You have a reward waiting to be claimed!",
    ShowDelay = 0f,
    AutoDismissTime = 0f,
    HasAction = true,
    ActionButtonText = "Claim",
    ActionTarget = new UID("screen_rewards"),
    RewardMultiplier = 1f,
    PlaySound = true,
    SoundId = new UID("sfx_reward"),
    SoundVolume = 1f,
    Vibrate = true,
    VibrationPattern = 0
};
```

### Event Notification

```csharp
NotificationDefinition eventNotification = new NotificationDefinition
{
    Type = NotificationType.Event,
    Priority = 15,
    MinPlayerLevel = 5,
    ShowOnce = true,
    CanDismiss = true,
    CanSnooze = false,
    Title = "New Event Available!",
    Message = "A new event has started. Participate to earn exclusive rewards!",
    ShowDelay = 0f,
    AutoDismissTime = 0f,
    HasAction = true,
    ActionButtonText = "Go to Event",
    ActionTarget = new UID("screen_event"),
    RewardMultiplier = 1.5f,
    PlaySound = true,
    SoundId = new UID("sfx_event"),
    SoundVolume = 1f,
    Vibrate = true,
    VibrationPattern = 2
};
```

### Reminder Notification

```csharp
NotificationDefinition reminderNotification = new NotificationDefinition
{
    Type = NotificationType.Reminder,
    Priority = 20,
    MinPlayerLevel = 1,
    ShowOnce = false,
    CanDismiss = true,
    CanSnooze = true,
    Title = "Daily Challenge Available!",
    Message = "Your daily challenge is ready. Complete it to earn bonus rewards!",
    ShowDelay = 0f,
    AutoDismissTime = 0f,
    HasAction = true,
    ActionButtonText = "Play Now",
    ActionTarget = new UID("screen_daily_challenge"),
    SnoozeDuration = 300,
    RewardMultiplier = 1f,
    PlaySound = true,
    SoundId = new UID("sfx_reminder"),
    SoundVolume = 0.8f,
    Vibrate = true,
    VibrationPattern = 0
};
```

## Summary

The Notification System provides a flexible, data-driven framework for creating and managing in-game notifications. It integrates seamlessly with your existing metadata architecture and supports:

- Multiple notification types and categories
- Comprehensive content and timing configuration
- Action buttons with navigation targets
- Reward integration for notification actions
- Sound and vibration feedback
- Analytics tracking for notification engagement
- Dismiss and snooze functionality
- Visual customization with icons and colors
- Priority-based display ordering
- Player level-based availability

This system is designed to be reusable across future games in your codebase, following the same patterns as your IAP, Ads, and other metadata systems.