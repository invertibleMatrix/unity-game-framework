# Season/Event System Documentation

## Overview

The Season/Event System provides a flexible, data-driven framework for managing seasonal and special events with time limits, rewards, progression, and leaderboards. It supports multiple event types, event levels, special features, and time-based scheduling.

## Architecture

### Core Components

```
Seasons/
├── EventType.cs              # Event type enum
├── EventDefinition.cs        # Main event definition
├── SeasonsRegistry.cs          # UID-based registry
└── SeasonsMeta.cs              # Query methods and logic
```

### Design Principles

1. **Data-Driven**: All event data configured in Unity Editor
2. **UID-Based**: Uses AK.Utilities UID for unique identification
3. **Flexible**: Supports multiple event types and progression systems
4. **Reward Integration**: Seamless integration with Rewards system
5. **Scalable**: Easy to add new events and features

## Core Types

### EventType

Enum defining different types of events.

```csharp
public enum EventType
{
    Seasonal,        // Seasonal event (e.g., Halloween, Christmas)
    Special,          // Special event (e.g., Anniversary, Birthday)
    LimitedTime,      // Limited time event
    Community,        // Community event
    Competitive,      // Competitive event
    Collaboration,     // Collaboration event
    Update,           // Update event
    Challenge,        // Challenge event
    Tournament,       // Tournament event
    Custom            // Custom event
}
```

### EventDefinition

Represents a seasonal or special event with time limits, rewards, and progression.

```csharp
public class EventDefinition : MetaDataAsset
{
    // Identification
    public UID UID;
    public string EventID;
    
    // Display Information
    public string DisplayName;
    public string Description;
    public Sprite Icon;
    public Sprite BannerImage;
    
    // Event Type
    public EventType Type;
    
    // Time Schedule
    public long StartTime;
    public long EndTime;
    public bool IsActive;
    
    // Requirements
    public int MinimumLevel;
    public int MaximumLevel;
    public List<UID> RequiredAchievements;
    
    // Progression
    public List<EventLevel> Levels;
    public int CurrentLevel;
    public int CurrentXP;
    
    // Rewards
    public List<RewardDefinition> CompletionRewards;
    public List<EventLevelReward> LevelRewards;
    public List<RewardDefinition> EarlyCompletionBonus;
    
    // Special Features
    public List<UID> SpecialShopItems;
    public List<UID> SpecialChallenges;
    public List<UID> SpecialAchievements;
    
    // Leaderboard
    public bool HasLeaderboard;
    public LeaderboardType LeaderboardType;
    public LeaderboardResetInterval LeaderboardResetInterval;
    
    // Analytics
    public string StartEventID;
    public string EndEventID;
    public string CompletionEventID;
    
    // Additional Data
    public Dictionary<string, string> CustomData;
}
```

**Properties:**
- `UID`: Unique identifier for this event
- `EventID`: String ID for reference
- `DisplayName`: Display name shown to players
- `Description`: Description shown to players
- `Icon`: Icon displayed in UI
- `BannerImage`: Banner image displayed in UI
- `Type`: Type of event
- `StartTime`: Start time (Unix timestamp)
- `EndTime`: End time (Unix timestamp)
- `IsActive`: Whether this event is currently active
- `MinimumLevel`: Minimum level required to participate
- `MaximumLevel`: Maximum level for this event (0 = no limit)
- `RequiredAchievements`: Required achievements to participate
- `Levels`: Event progression levels
- `CurrentLevel`: Current event level
- `CurrentXP`: Current event XP
- `CompletionRewards`: Rewards granted on event completion
- `LevelRewards`: Rewards granted for reaching each level
- `EarlyCompletionBonus`: Bonus rewards for early completion
- `SpecialShopItems`: Special shop items available during event
- `SpecialChallenges`: Special challenges available during event
- `SpecialAchievements`: Special achievements available during event
- `HasLeaderboard`: Whether this event has a leaderboard
- `LeaderboardType`: Leaderboard scoring type
- `LeaderboardResetInterval`: Leaderboard reset interval
- `StartEventID`: Analytics event to track when event starts
- `EndEventID`: Analytics event to track when event ends
- `CompletionEventID`: Analytics event to track when event is completed
- `CustomData`: Additional data for custom event types

### EventLevel

Represents an event level with XP requirements and rewards.

```csharp
public class EventLevel
{
    public int LevelNumber;
    public int RequiredXP;
    public List<RewardDefinition> Rewards;
    public List<UID> Unlocks;
}
```

### EventLevelReward

Represents a reward granted for reaching a specific event level.

```csharp
public class EventLevelReward
{
    public int LevelNumber;
    public List<RewardDefinition> Rewards;
}
```

### LeaderboardType

Enum defining types of leaderboards.

```csharp
public enum LeaderboardType
{
    Score,
    XP,
    Level,
    Custom
}
```

### LeaderboardResetInterval

Enum defining leaderboard reset intervals.

```csharp
public enum LeaderboardResetInterval
{
    Never,
    Daily,
    Weekly,
    Monthly,
    EventEnd
}
```

### SeasonsRegistry

UID-based registry for managing event definitions.

```csharp
public class SeasonsRegistry : TypedUIDRegistryAsset<EventDefinition>
{
    // Inherits from TypedUIDRegistryAsset
    // Provides UID-based lookup and validation
}
```

### SeasonsMeta

Container for event data with query methods.

```csharp
public class SeasonsMeta : MetaDataAsset
{
    public List<EventDefinition> Events;
    
    // Query methods
    public EventDefinition GetEventByID(string eventID);
    public EventDefinition GetEventByUID(UID uid);
    public List<EventDefinition> GetEventsByType(EventType type);
    public List<EventDefinition> GetActiveEvents();
    public List<EventDefinition> GetAvailableEvents(int playerLevel, List<UID> completedAchievements);
    public List<EventDefinition> GetEventsWithLeaderboards();
    public List<EventDefinition> GetEventsStartingSoon(int hours);
    public List<EventDefinition> GetEventsEndingSoon(int hours);
    public List<EventDefinition> GetEventsForLevelRange(int minLevel, int maxLevel);
    public List<EventDefinition> GetEventsSortedByStartTime();
    public List<EventDefinition> GetEventsSortedByEndTime();
    public List<EventDefinition> GetEventsWithAnalytics();
    public int GetTotalEventCount();
    public int GetEventCountByType(EventType type);
    public float GetCompletionPercentage(List<UID> completedEvents);
    public float GetCompletionPercentageByType(EventType type, List<UID> completedEvents);
}
```

## Usage Examples

### Basic Event Participation

```csharp
// Get event by ID
var eventDef = metaDataRepository.GetEventByID("halloween_event_2024");

// Check if event is active
if (eventDef.IsCurrentlyActive())
{
    // Check if player can participate
    if (eventDef.IsAvailableToPlayer(playerData.Level, playerData.CompletedAchievements))
    {
        // Add XP to event
        eventDef.AddXP(100);
        
        // Check for level up
        var currentLevel = eventDef.GetCurrentEventLevel();
        if (eventDef.CurrentLevel > playerData.EventLevel)
        {
            // Grant level rewards
            foreach (var reward in currentLevel.Rewards)
            {
                rewardSystem.GrantReward(reward);
            }
            
            // Process unlocks
            foreach (var unlockUID in currentLevel.Unlocks)
            {
                UnlockFeature(unlockUID);
            }
            
            playerData.EventLevel = eventDef.CurrentLevel;
        }
    }
}
```

### Event Completion

```csharp
// Check if event is completed
if (eventDef.IsCompleted())
{
    // Grant completion rewards
    foreach (var reward in eventDef.CompletionRewards)
    {
        rewardSystem.GrantReward(reward);
    }
    
    // Track analytics
    if (!string.IsNullOrEmpty(eventDef.CompletionEventID))
    {
        analyticsSystem.TrackEvent(eventDef.CompletionEventID);
    }
    
    // Mark event as completed
    playerData.CompletedEvents.Add(eventDef.UID);
}
```

### Event Progression Display

```csharp
// Display event progress
var currentLevel = eventDef.GetCurrentEventLevel();
var nextLevel = eventDef.GetNextEventLevel();

// Display current level info
uiSystem.DisplayEventLevel(currentLevel.LevelNumber, currentLevel.RequiredXP, eventDef.CurrentXP);

// Display next level info
if (nextLevel != null)
{
    uiSystem.DisplayNextEventLevel(nextLevel.LevelNumber, nextLevel.RequiredXP);
}

// Display completion percentage
float completionPercentage = eventDef.GetCompletionPercentage();
uiSystem.DisplayEventProgress(completionPercentage);
```

### Time-Limited Events

```csharp
// Get events ending soon (within 24 hours)
var endingSoon = metaDataRepository.GetEventsEndingSoon(24);

// Display countdown
foreach (var eventDef in endingSoon)
{
    long timeRemaining = eventDef.GetTimeRemaining();
    uiSystem.DisplayEventCountdown(eventDef, timeRemaining);
}
```

### Event Leaderboard

```csharp
// Get events with leaderboards
var leaderboardEvents = metaDataRepository.GetEventsWithLeaderboards();

foreach (var eventDef in leaderboardEvents)
{
    if (eventDef.IsCurrentlyActive())
    {
        // Get leaderboard data
        var leaderboard = leaderboardSystem.GetLeaderboard(eventDef.EventID);
        
        // Display leaderboard
        uiSystem.DisplayEventLeaderboard(eventDef, leaderboard);
    }
}
```

### Special Shop Items

```csharp
// Get special shop items for event
var eventDef = metaDataRepository.GetEventByID("halloween_event_2024");

foreach (var itemUID in eventDef.SpecialShopItems)
{
    var shopItem = metaDataRepository.GetObjectByUID<ShopItemDefinition>(itemUID);
    if (shopItem != null)
    {
        // Make item available in shop during event
        shopItem.IsAvailable = true;
        shopItem.StartTime = eventDef.StartTime;
        shopItem.EndTime = eventDef.EndTime;
    }
}
```

### Special Challenges

```csharp
// Get special challenges for event
var eventDef = metaDataRepository.GetEventByID("halloween_event_2024");

foreach (var challengeUID in eventDef.SpecialChallenges)
{
    var challenge = metaDataRepository.GetObjectByUID<DailyChallengeDefinition>(challengeUID);
    if (challenge != null)
    {
        // Make challenge available during event
        challenge.IsActive = true;
        challenge.StartTime = eventDef.StartTime;
        challenge.EndTime = eventDef.EndTime;
    }
}
```

### Event Analytics

```csharp
// Track event start
public void OnEventStart(EventDefinition eventDef)
{
    if (!string.IsNullOrEmpty(eventDef.StartEventID))
    {
        analyticsSystem.TrackEvent(eventDef.StartEventID);
    }
}

// Track event end
public void OnEventEnd(EventDefinition eventDef)
{
    if (!string.IsNullOrEmpty(eventDef.EndEventID))
    {
        analyticsSystem.TrackEvent(eventDef.EndEventID);
    }
}

// Track event completion
public void OnEventComplete(EventDefinition eventDef)
{
    if (!string.IsNullOrEmpty(eventDef.CompletionEventID))
    {
        analyticsSystem.TrackEvent(eventDef.CompletionEventID);
    }
}
```

## Integration with MetaDataRepository

The Season/Event System is fully integrated with the MetaDataRepository:

```csharp
// Access event data
var eventDef = metaDataRepository.GetEventByID("halloween_event_2024");
var typeEvents = metaDataRepository.GetEventsByType(EventType.Seasonal);
var activeEvents = metaDataRepository.GetActiveEvents();
var availableEvents = metaDataRepository.GetAvailableEvents(playerData.Level, playerData.CompletedAchievements);
var leaderboardEvents = metaDataRepository.GetEventsWithLeaderboards();
float completionPercentage = metaDataRepository.GetEventCompletionPercentage(playerData.CompletedEvents);

// Get object by UID
var eventByUID = metaDataRepository.GetObjectByUID<EventDefinition>(uid);
```

## Best Practices

### 1. Event Design

- **Clear Theme**: Make event theme clear and engaging
- **Meaningful Rewards**: Provide rewards that match event theme
- **Balanced Progression**: Design achievable progression curves
- **Time Limits**: Set reasonable time limits for events
- **Unique Features**: Include unique features for each event

### 2. Event Scheduling

- **Regular Rotation**: Rotate events regularly for variety
- **Seasonal Alignment**: Align events with real-world seasons
- **Avoid Overlap**: Don't overlap similar events
- **Clear Communication**: Inform players of upcoming events
- **Fair Duration**: Set appropriate event durations

### 3. Progression Design

- **Early Game**: Easy progression to hook players
- **Mid Game**: Moderate progression to maintain engagement
- **Late Game**: Challenging progression for dedicated players
- **Meaningful Unlocks**: Unlock features at appropriate points
- **Reward Milestones**: Provide rewards at key progression points

### 4. Leaderboard Design

- **Fair Scoring**: Design fair and balanced scoring systems
- **Clear Categories**: Use clear scoring categories
- **Regular Resets**: Reset leaderboards at appropriate intervals
- **Anti-Cheat**: Implement anti-cheat measures
- **Rewards**: Provide leaderboard rewards for top players

### 5. Special Features

- **Unique Items**: Include unique items for each event
- **Special Challenges**: Create event-specific challenges
- **Limited Offers**: Create time-limited offers
- **Exclusive Content**: Include exclusive content for events
- **Social Features**: Include social features for engagement

## Advanced Features

### Dynamic Event Activation

Use time-based activation for automatic event management:

```csharp
// Check for event activation
public void UpdateEventStatus()
{
    var allEvents = metaDataRepository.GetAllEvents();
    
    foreach (var eventDef in allEvents)
    {
        if (eventDef.IsCurrentlyActive())
        {
            eventDef.IsActive = true;
        }
        else
        {
            eventDef.IsActive = false;
        }
    }
}
```

### Event Chains

Create chains of events where completing one unlocks the next:

```csharp
// Event 1: Halloween 2024
event1.StartTime = new DateTime(2024, 10, 1).ToUniversalTime().Ticks / TimeSpan.TicksPerSecond;
event1.EndTime = new DateTime(2024, 11, 1).ToUniversalTime().Ticks / TimeSpan.TicksPerSecond;

// Event 2: Christmas 2024
event2.StartTime = new DateTime(2024, 12, 1).ToUniversalTime().Ticks / TimeSpan.TicksPerSecond;
event2.EndTime = new DateTime(2025, 1, 1).ToUniversalTime().Ticks / TimeSpan.TicksPerSecond;
```

### Event Milestones

Provide rewards at key progression points:

```csharp
// Event with milestone rewards
eventDef.LevelRewards = new List<EventLevelReward>
{
    new EventLevelReward
    {
        LevelNumber = 1,
        Rewards = new List<RewardDefinition> { smallReward }
    },
    new EventLevelReward
    {
        LevelNumber = 3,
        Rewards = new List<RewardDefinition> { mediumReward }
    },
    new EventLevelReward
    {
        LevelNumber = 5,
        Rewards = new List<RewardDefinition> { largeReward }
    }
};
```

### Cross-Event Integration

Connect events with other systems:

```csharp
// Event unlocks special achievements
eventDef.SpecialAchievements = new List<UID>
{
    achievementUID1,
    achievementUID2,
    achievementUID3
};

// Event unlocks special shop items
eventDef.SpecialShopItems = new List<UID>
{
    shopItemUID1,
    shopItemUID2,
    shopItemUID3
};

// Event unlocks special challenges
eventDef.SpecialChallenges = new List<UID>
{
    challengeUID1,
    challengeUID2,
    challengeUID3
};
```

## Performance Considerations

1. **Caching**: Cache frequently accessed event definitions
2. **Lazy Loading**: Load event data only when needed
3. **Batch Operations**: Process multiple events in batches
4. **UID Lookups**: Use UID-based lookups for performance
5. **Time Checks**: Optimize time-based checks

## Troubleshooting

### Common Issues

**Issue**: Event not activating
- **Solution**: Check `IsActive` flag and time schedule

**Issue**: Progress not saving
- **Solution**: Ensure event progress is persisted in player data

**Issue**: Leaderboard not updating
- **Solution**: Check `HasLeaderboard` flag and reset interval

**Issue**: Special items not available
- **Solution**: Check event time limits and item availability

**Issue**: Event not ending
- **Solution**: Check `EndTime` value and activation logic

## Future Enhancements

Potential improvements for the Season/Event System:

1. **Dynamic Events**: AI-based event generation
2. **Player-Driven Events**: Community-created events
3. **Cross-Game Events**: Events spanning multiple games
4. **Live Events**: Real-time event management
5. **Event Tiers**: Multiple difficulty tiers for events
6. **Event Branching**: Multiple event paths
7. **Event Persistence**: Save event progress across sessions
8. **Event Analytics**: Advanced event analytics and insights

## Conclusion

The Season/Event System provides a solid foundation for managing seasonal and special events in your game. Its data-driven approach and integration with other metadata systems make it flexible and maintainable across multiple projects.