# Achievements System Documentation

## Overview

The Achievements System provides a flexible, data-driven framework for managing player achievements with various completion criteria, rewards, and progression tracking. It supports multiple achievement types, rarity levels, prerequisites, and milestone rewards.

## Architecture

### Core Components

```
Achievements/
├── AchievementType.cs          # Achievement type enum
├── AchievementDefinition.cs    # Main achievement definition
├── AchievementsRegistry.cs     # UID-based registry
└── AchievementsMeta.cs         # Query methods and logic
```

### Design Principles

1. **Data-Driven**: All achievement data configured in Unity Editor
2. **UID-Based**: Uses AK.Utilities UID for unique identification
3. **Flexible**: Supports multiple achievement types and criteria
4. **Reward Integration**: Seamless integration with Rewards system
5. **Scalable**: Easy to add new achievements and types

## Core Types

### AchievementType

Enum defining different types of achievements based on completion criteria.

```csharp
public enum AchievementType
{
    LevelBased,      // Based on reaching a specific level
    Accumulation,    // Based on accumulating a specific amount
    CountBased,      // Based on completing a specific number of actions
    LevelComplete,   // Based on completing a specific level
    StarBased,       // Based on earning stars on levels
    Streak,          // Based on streak (consecutive days, wins, etc.)
    TimeBased,       // Based on time (play time, completion time, etc.)
    Social,          // Based on social actions
    Collection,      // Based on collecting items
    Special,         // Based on special events or conditions
    FeatureUsage,    // Based on using specific features
    ChallengeComplete, // Based on completing challenges
    Spending,        // Based on spending resources
    WinStreak,       // Based on winning streaks
    Custom           // Based on custom criteria
}
```

### AchievementDefinition

Represents an achievement with completion criteria and rewards.

```csharp
public class AchievementDefinition : MetaDataAsset
{
    // Identification
    public UID UID;
    public string AchievementID;
    
    // Display Information
    public string DisplayName;
    public string Description;
    public Sprite Icon;
    public AchievementRarity Rarity;
    
    // Achievement Type
    public AchievementType Type;
    
    // Completion Criteria
    public int TargetValue;
    public int CurrentProgress;
    public bool IsActive;
    public bool IsHidden;
    public bool IsRepeatable;
    public int MaxCompletions;
    public float CompletionCooldown;
    
    // Prerequisites
    public List<UID> PrerequisiteAchievements;
    public int MinimumLevel;
    
    // Rewards
    public List<RewardDefinition> Rewards;
    public List<RewardDefinition> FirstCompletionBonus;
    
    // Progression
    public List<AchievementMilestone> Milestones;
    
    // Time Limits
    public bool HasTimeLimit;
    public float TimeLimit;
    public bool ExpiresAfterTimeLimit;
    
    // Analytics
    public string CompletionEventID;
    
    // Additional Data
    public Dictionary<string, string> CustomData;
}
```

**Properties:**
- `UID`: Unique identifier for this achievement
- `AchievementID`: String ID for reference
- `DisplayName`: Display name shown to players
- `Description`: Description shown to players
- `Icon`: Icon displayed in UI
- `Rarity`: Rarity level (Common, Uncommon, Rare, Epic, Legendary)
- `Type`: Type of achievement based on completion criteria
- `TargetValue`: Target value to complete the achievement
- `CurrentProgress`: Current progress (for tracking)
- `IsActive`: Whether this achievement is currently active
- `IsHidden`: Whether this achievement is hidden until discovered
- `IsRepeatable`: Whether this achievement can be completed multiple times
- `MaxCompletions`: Maximum number of completions (0 = unlimited)
- `CompletionCooldown`: Cooldown between completions in seconds
- `PrerequisiteAchievements`: Achievements that must be completed first
- `MinimumLevel`: Minimum level required to unlock this achievement
- `Rewards`: Rewards granted on completion
- `FirstCompletionBonus`: Bonus rewards for first completion
- `Milestones`: Milestone rewards for partial progress
- `HasTimeLimit`: Whether there's a time limit to complete
- `TimeLimit`: Time limit in seconds
- `ExpiresAfterTimeLimit`: Whether achievement expires after time limit
- `CompletionEventID`: Analytics event to track completion
- `CustomData`: Additional data for custom achievement types

### AchievementRarity

Enum defining rarity levels for achievements.

```csharp
public enum AchievementRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}
```

### AchievementMilestone

Represents a milestone reward for partial achievement progress.

```csharp
public class AchievementMilestone
{
    public int ProgressThreshold;
    public List<RewardDefinition> Rewards;
    public bool Rewarded;
}
```

### AchievementsRegistry

UID-based registry for managing achievement definitions.

```csharp
public class AchievementsRegistry : TypedUIDRegistryAsset<AchievementDefinition>
{
    // Inherits from TypedUIDRegistryAsset
    // Provides UID-based lookup and validation
}
```

### AchievementsMeta

Container for achievement data with query methods.

```csharp
public class AchievementsMeta : MetaDataAsset
{
    public List<AchievementDefinition> Achievements;
    
    // Query methods
    public AchievementDefinition GetAchievementByID(string achievementID);
    public AchievementDefinition GetAchievementByUID(UID uid);
    public List<AchievementDefinition> GetAchievementsByType(AchievementType type);
    public List<AchievementDefinition> GetAchievementsByRarity(AchievementRarity rarity);
    public List<AchievementDefinition> GetActiveAchievements();
    public List<AchievementDefinition> GetHiddenAchievements();
    public List<AchievementDefinition> GetVisibleAchievements();
    public List<AchievementDefinition> GetRepeatableAchievements();
    public List<AchievementDefinition> GetAvailableAchievements(int playerLevel, List<UID> completedAchievements);
    public List<AchievementDefinition> GetTimeLimitedAchievements();
    public List<AchievementDefinition> GetAchievementsForLevel(int level);
    public List<AchievementDefinition> GetAchievementsUnlockedAtLevel(int level);
    public List<AchievementDefinition> GetAchievementsWithPrerequisites();
    public List<AchievementDefinition> GetPrerequisiteAchievements(AchievementDefinition achievement);
    public List<AchievementDefinition> GetDependentAchievements(AchievementDefinition achievement);
    public List<AchievementDefinition> GetAchievementsSortedByRarity();
    public List<AchievementDefinition> GetAchievementsSortedByDifficulty();
    public List<AchievementDefinition> GetAchievementsWithMilestones();
    public List<AchievementDefinition> GetAchievementsWithAnalytics();
    public int GetTotalAchievementCount();
    public int GetAchievementCountByType(AchievementType type);
    public int GetAchievementCountByRarity(AchievementRarity rarity);
    public float GetCompletionPercentage(List<UID> completedAchievements);
    public float GetCompletionPercentageByType(AchievementType type, List<UID> completedAchievements);
}
```

## Usage Examples

### Basic Achievement Tracking

```csharp
// Get achievement by ID
var achievement = metaDataRepository.GetAchievementByID("first_win");

// Add progress
achievement.AddProgress(1);

// Check if completed
if (achievement.IsCompleted)
{
    // Grant rewards
    foreach (var reward in achievement.Rewards)
    {
        rewardSystem.GrantReward(reward);
    }
    
    // Track analytics
    if (!string.IsNullOrEmpty(achievement.CompletionEventID))
    {
        analyticsSystem.TrackEvent(achievement.CompletionEventID);
    }
}
```

### Level-Based Achievement

```csharp
// Check for level-based achievements
var levelAchievements = metaDataRepository.GetAchievementsByType(AchievementType.LevelBased);

foreach (var achievement in levelAchievements)
{
    if (achievement.TargetValue <= playerData.Level && !playerData.HasCompletedAchievement(achievement.AchievementID))
    {
        // Complete achievement
        achievement.CurrentProgress = achievement.TargetValue;
        GrantAchievementRewards(achievement);
        playerData.MarkAchievementCompleted(achievement.AchievementID);
    }
}
```

### Count-Based Achievement

```csharp
// Track bubble pops
public void OnBubblePopped()
{
    var popAchievement = metaDataRepository.GetAchievementByID("pop_1000_bubbles");
    
    if (popAchievement != null && !playerData.HasCompletedAchievement(popAchievement.AchievementID))
    {
        popAchievement.AddProgress(1);
        
        // Check for milestone rewards
        var milestoneRewards = popAchievement.GetMilestoneRewards();
        foreach (var reward in milestoneRewards)
        {
            rewardSystem.GrantReward(reward);
        }
        
        // Check if completed
        if (popAchievement.IsCompleted)
        {
            GrantAchievementRewards(popAchievement);
            playerData.MarkAchievementCompleted(popAchievement.AchievementID);
        }
    }
}
```

### Star-Based Achievement

```csharp
// Check for star-based achievements
public void OnLevelComplete(int level, int stars)
{
    var starAchievements = metaDataRepository.GetAchievementsByType(AchievementType.StarBased);
    
    foreach (var achievement in starAchievements)
    {
        if (!playerData.HasCompletedAchievement(achievement.AchievementID))
        {
            // Check custom data for star requirements
            if (achievement.CustomData.TryGetValue("required_stars", out string requiredStarsStr))
            {
                int requiredStars = int.Parse(requiredStarsStr);
                if (stars >= requiredStars)
                {
                    achievement.AddProgress(1);
                    
                    if (achievement.IsCompleted)
                    {
                        GrantAchievementRewards(achievement);
                        playerData.MarkAchievementCompleted(achievement.AchievementID);
                    }
                }
            }
        }
    }
}
```

### Repeatable Achievement

```csharp
// Handle repeatable achievements
public void OnDailyLogin()
{
    var dailyAchievement = metaDataRepository.GetAchievementByID("daily_login_streak");
    
    if (dailyAchievement != null && dailyAchievement.IsRepeatable)
    {
        // Check cooldown
        if (Time.time - playerData.GetLastCompletionTime(dailyAchievement.AchievementID) >= dailyAchievement.CompletionCooldown)
        {
            dailyAchievement.AddProgress(1);
            
            if (dailyAchievement.IsCompleted)
            {
                // Check if first completion
                int completionCount = playerData.GetCompletionCount(dailyAchievement.AchievementID);
                if (completionCount == 0)
                {
                    // Grant first completion bonus
                    foreach (var reward in dailyAchievement.FirstCompletionBonus)
                    {
                        rewardSystem.GrantReward(reward);
                    }
                }
                
                // Grant regular rewards
                foreach (var reward in dailyAchievement.Rewards)
                {
                    rewardSystem.GrantReward(reward);
                }
                
                // Reset progress for next completion
                dailyAchievement.ResetProgress();
                playerData.IncrementCompletionCount(dailyAchievement.AchievementID);
                playerData.SetLastCompletionTime(dailyAchievement.AchievementID, Time.time);
            }
        }
    }
}
```

### Achievement Prerequisites

```csharp
// Check if achievement is available
public bool IsAchievementAvailable(AchievementDefinition achievement)
{
    // Check minimum level
    if (playerData.Level < achievement.MinimumLevel)
    {
        return false;
    }
    
    // Check prerequisites
    foreach (var prereqUID in achievement.PrerequisiteAchievements)
    {
        if (!playerData.HasCompletedAchievement(prereqUID))
        {
            return false;
        }
    }
    
    return true;
}
```

### Time-Limited Achievement

```csharp
// Handle time-limited achievements
public void UpdateTimeLimitedAchievements()
{
    var timeLimitedAchievements = metaDataRepository.GetTimeLimitedAchievements();
    
    foreach (var achievement in timeLimitedAchievements)
    {
        if (achievement.HasTimeLimit && achievement.IsActive)
        {
            float elapsedTime = Time.time - achievement.StartTime;
            
            if (elapsedTime >= achievement.TimeLimit)
            {
                if (achievement.ExpiresAfterTimeLimit)
                {
                    achievement.IsActive = false;
                }
                else
                {
                    // Achievement is still available but time limit has passed
                    // Handle accordingly
                }
            }
        }
    }
}
```

### Achievement Completion Percentage

```csharp
// Get overall completion percentage
float completionPercentage = metaDataRepository.GetAchievementCompletionPercentage(playerData.CompletedAchievements);

// Get completion percentage by type
float levelCompletion = metaDataRepository.GetAchievementCompletionPercentageByType(
    AchievementType.LevelBased, 
    playerData.CompletedAchievements
);

// Display in UI
completionText.text = $"Achievements: {completionPercentage:F1}%";
```

## Integration with MetaDataRepository

The Achievements System is fully integrated with the MetaDataRepository:

```csharp
// Access achievement data
var achievement = metaDataRepository.GetAchievementByID("first_win");
var typeAchievements = metaDataRepository.GetAchievementsByType(AchievementType.LevelBased);
var rarityAchievements = metaDataRepository.GetAchievementsByRarity(AchievementRarity.Legendary);
var activeAchievements = metaDataRepository.GetActiveAchievements();
var availableAchievements = metaDataRepository.GetAvailableAchievements(playerData.Level, playerData.CompletedAchievements);
var levelAchievements = metaDataRepository.GetAchievementsForLevel(playerData.Level);
float completionPercentage = metaDataRepository.GetAchievementCompletionPercentage(playerData.CompletedAchievements);

// Get object by UID
var achievementByUID = metaDataRepository.GetObjectByUID<AchievementDefinition>(uid);
```

## Best Practices

### 1. Achievement Design

- **Clear Goals**: Make achievement requirements clear and understandable
- **Varied Difficulty**: Mix easy, medium, and hard achievements
- **Meaningful Rewards**: Provide rewards that match achievement difficulty
- **Progressive**: Design achievements that guide players through the game
- **Fun**: Make achievements enjoyable to complete

### 2. Rarity Distribution

- **Common (40%)**: Easy achievements for early engagement
- **Uncommon (30%)**: Medium difficulty achievements
- **Rare (20%)**: Hard achievements for dedicated players
- **Epic (8%)**: Very hard achievements
- **Legendary (2%)**: Extremely rare achievements

### 3. Progress Tracking

- **Frequent Updates**: Update progress frequently for better feedback
- **Visual Feedback**: Show progress bars and milestones
- **Notifications**: Notify players of progress and completion
- **Persistence**: Save progress and completion status

### 4. Prerequisites

- **Logical Flow**: Create logical progression chains
- **Avoid Deadlocks**: Ensure all achievements are achievable
- **Clear Dependencies**: Make prerequisite relationships clear
- **Multiple Paths**: Allow different ways to unlock achievements

### 5. Time Limits

- **Reasonable Duration**: Set achievable time limits
- **Clear Communication**: Inform players of time limits
- **Fair Warning**: Warn players before time expires
- **Replayability**: Allow time-limited achievements to return

## Advanced Features

### Custom Achievement Types

Use the `Custom` type with `CustomData` for unique achievement criteria:

```csharp
// Custom achievement for completing levels without using powerups
var achievement = metaDataRepository.GetAchievementByID("no_powerup_master");
achievement.CustomData = new Dictionary<string, string>
{
    { "max_powerups", "0" },
    { "min_levels", "10" }
};

// Check in game logic
public void OnLevelComplete(int level, int powerupsUsed)
{
    if (achievement.CustomData.TryGetValue("max_powerups", out string maxPowerupsStr))
    {
        int maxPowerups = int.Parse(maxPowerupsStr);
        if (powerupsUsed <= maxPowerups)
        {
            achievement.AddProgress(1);
        }
    }
}
```

### Achievement Chains

Create chains of achievements where completing one unlocks the next:

```csharp
// Achievement 1: Complete 10 levels
achievement1.PrerequisiteAchievements = new List<UID>();
achievement1.TargetValue = 10;

// Achievement 2: Complete 50 levels (requires Achievement 1)
achievement2.PrerequisiteAchievements = new List<UID> { achievement1.UID };
achievement2.TargetValue = 50;

// Achievement 3: Complete 100 levels (requires Achievement 2)
achievement3.PrerequisiteAchievements = new List<UID> { achievement2.UID };
achievement3.TargetValue = 100;
```

### Milestone Rewards

Provide rewards at partial progress to keep players engaged:

```csharp
// Achievement with milestones
achievement.Milestones = new List<AchievementMilestone>
{
    new AchievementMilestone
    {
        ProgressThreshold = 25,
        Rewards = new List<RewardDefinition> { smallReward }
    },
    new AchievementMilestone
    {
        ProgressThreshold = 50,
        Rewards = new List<RewardDefinition> { mediumReward }
    },
    new AchievementMilestone
    {
        ProgressThreshold = 75,
        Rewards = new List<RewardDefinition> { largeReward }
    }
};
```

### Seasonal Achievements

Use time limits and activation for seasonal content:

```csharp
// Halloween achievement
var halloweenAchievement = metaDataRepository.GetAchievementByID("halloween_master");
halloweenAchievement.IsActive = false;
halloweenAchievement.HasTimeLimit = true;
halloweenAchievement.TimeLimit = 7 * 24 * 60 * 60; // 7 days

// Activate during Halloween event
public void OnHalloweenEventStart()
{
    halloweenAchievement.IsActive = true;
    halloweenAchievement.StartTime = Time.time;
}
```

## Performance Considerations

1. **Caching**: Cache frequently accessed achievement definitions
2. **Lazy Loading**: Load achievement data only when needed
3. **Batch Operations**: Process multiple achievements in batches
4. **UID Lookups**: Use UID-based lookups for performance
5. **Progress Updates**: Batch progress updates when possible

## Troubleshooting

### Common Issues

**Issue**: Achievement not triggering
- **Solution**: Check `IsActive` flag and `MinimumLevel` requirement

**Issue**: Prerequisites not working
- **Solution**: Ensure prerequisite UIDs are valid and completed

**Issue**: Progress not saving
- **Solution**: Ensure achievement progress is persisted in player data

**Issue**: Milestones not rewarding
- **Solution**: Check `Rewarded` flag and `ProgressThreshold` values

**Issue**: Repeatable achievements not resetting
- **Solution**: Call `ResetProgress()` after completion

## Future Enhancements

Potential improvements for the Achievements System:

1. **Achievement Categories**: Group achievements by category
2. **Achievement Sets**: Collections of related achievements
3. **Leaderboards**: Compare achievements with friends
4. **Achievement Sharing**: Share achievements on social media
5. **Dynamic Achievements**: Generate achievements based on player behavior
6. **Achievement Guides**: In-game hints for difficult achievements
7. **Achievement Notifications**: Push notifications for achievements
8. **Achievement Badges**: Visual badges for completed achievements

## Conclusion

The Achievements System provides a solid foundation for managing player achievements in your game. Its data-driven approach and integration with other metadata systems make it flexible and maintainable across multiple projects.