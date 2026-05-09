# Daily Challenges System Documentation

## Overview

The Daily Challenges System provides a flexible, data-driven framework for managing daily challenges with various completion criteria, rewards, and scheduling. It supports multiple challenge types, difficulty levels, day-based scheduling, and milestone rewards.

## Architecture

### Core Components

```
DailyChallenges/
├── ChallengeType.cs              # Challenge type enum
├── DailyChallengeDefinition.cs   # Main challenge definition
├── DailyChallengesRegistry.cs    # UID-based registry
└── DailyChallengesMeta.cs        # Query methods and logic
```

### Design Principles

1. **Data-Driven**: All challenge data configured in Unity Editor
2. **UID-Based**: Uses AK.Utilities UID for unique identification
3. **Flexible**: Supports multiple challenge types and criteria
4. **Reward Integration**: Seamless integration with Rewards system
5. **Scalable**: Easy to add new challenges and types

## Core Types

### ChallengeType

Enum defining different types of daily challenges based on completion criteria.

```csharp
public enum ChallengeType
{
    LevelComplete,    // Complete a specific number of levels
    StarEarn,         // Earn a specific number of stars on levels
    PowerupUse,       // Use a specific powerup a number of times
    BubblePop,        // Pop a specific number of bubbles
    TimeLimit,        // Complete levels within a time limit
    NoLivesLost,      // Complete levels without losing lives
    CoinEarn,         // Earn a specific amount of coins
    CoinSpend,        // Spend a specific amount of coins
    AdWatch,          // Watch a specific number of ads
    DailyLogin,       // Login on consecutive days
    ThemeComplete,    // Complete levels with specific themes
    BoosterUse,       // Use specific boosters
    SpecificLevel,    // Complete a specific level
    ScoreAchieve,     // Achieve a specific score
    Custom            // Custom criteria
}
```

### DailyChallengeDefinition

Represents a daily challenge with completion criteria and rewards.

```csharp
public class DailyChallengeDefinition : MetaDataAsset
{
    // Identification
    public UID UID;
    public string ChallengeID;
    
    // Display Information
    public string DisplayName;
    public string Description;
    public Sprite Icon;
    public ChallengeDifficulty Difficulty;
    
    // Challenge Type
    public ChallengeType Type;
    
    // Completion Criteria
    public int TargetValue;
    public int CurrentProgress;
    public bool IsActive;
    
    // Requirements
    public int MinimumLevel;
    public int MaximumLevel;
    public UID RequiredPowerupUID;
    public UID RequiredThemeUID;
    public UID RequiredBoosterUID;
    public int SpecificLevelNumber;
    
    // Rewards
    public List<RewardDefinition> Rewards;
    public List<RewardDefinition> EarlyCompletionBonus;
    public float EarlyCompletionTimeLimit;
    
    // Time Limits
    public bool HasTimeLimit;
    public float TimeLimit;
    
    // Scheduling
    public List<int> AvailableDays;
    public bool IsRecurring;
    public int RecurrenceInterval;
    
    // Progression
    public List<ChallengeMilestone> Milestones;
    
    // Analytics
    public string CompletionEventID;
    
    // Additional Data
    public Dictionary<string, string> CustomData;
}
```

**Properties:**
- `UID`: Unique identifier for this challenge
- `ChallengeID`: String ID for reference
- `DisplayName`: Display name shown to players
- `Description`: Description shown to players
- `Icon`: Icon displayed in UI
- `Difficulty`: Difficulty level (Easy, Medium, Hard, Expert, Master)
- `Type`: Type of challenge based on completion criteria
- `TargetValue`: Target value to complete the challenge
- `CurrentProgress`: Current progress (for tracking)
- `IsActive`: Whether this challenge is currently active
- `MinimumLevel`: Minimum level required to access this challenge
- `MaximumLevel`: Maximum level for this challenge (0 = no limit)
- `RequiredPowerupUID`: Required powerup type (for PowerupUse challenges)
- `RequiredThemeUID`: Required theme (for ThemeComplete challenges)
- `RequiredBoosterUID`: Required booster type (for BoosterUse challenges)
- `SpecificLevelNumber`: Specific level number (for SpecificLevel challenges)
- `Rewards`: Rewards granted on completion
- `EarlyCompletionBonus`: Bonus rewards for early completion
- `EarlyCompletionTimeLimit`: Time limit for early completion bonus (in hours)
- `HasTimeLimit`: Whether there's a time limit to complete
- `TimeLimit`: Time limit in seconds
- `AvailableDays`: Days of the week this challenge is available (0 = Sunday, 6 = Saturday)
- `IsRecurring`: Whether this is a recurring challenge
- `RecurrenceInterval`: Recurrence interval in days
- `Milestones`: Milestone rewards for partial progress
- `CompletionEventID`: Analytics event to track completion
- `CustomData`: Additional data for custom challenge types

### ChallengeDifficulty

Enum defining difficulty levels for daily challenges.

```csharp
public enum ChallengeDifficulty
{
    Easy,
    Medium,
    Hard,
    Expert,
    Master
}
```

### ChallengeMilestone

Represents a milestone reward for partial challenge progress.

```csharp
public class ChallengeMilestone
{
    public int ProgressThreshold;
    public List<RewardDefinition> Rewards;
    public bool Rewarded;
}
```

### DailyChallengesRegistry

UID-based registry for managing daily challenge definitions.

```csharp
public class DailyChallengesRegistry : TypedUIDRegistryAsset<DailyChallengeDefinition>
{
    // Inherits from TypedUIDRegistryAsset
    // Provides UID-based lookup and validation
}
```

### DailyChallengesMeta

Container for daily challenge data with query methods.

```csharp
public class DailyChallengesMeta : MetaDataAsset
{
    public List<DailyChallengeDefinition> Challenges;
    
    // Query methods
    public DailyChallengeDefinition GetChallengeByID(string challengeID);
    public DailyChallengeDefinition GetChallengeByUID(UID uid);
    public List<DailyChallengeDefinition> GetChallengesByType(ChallengeType type);
    public List<DailyChallengeDefinition> GetChallengesByDifficulty(ChallengeDifficulty difficulty);
    public List<DailyChallengeDefinition> GetActiveChallenges();
    public List<DailyChallengeDefinition> GetChallengesForDay(int dayOfWeek);
    public List<DailyChallengeDefinition> GetAvailableChallenges(int playerLevel, int currentDayOfWeek);
    public List<DailyChallengeDefinition> GetTimeLimitedChallenges();
    public List<DailyChallengeDefinition> GetRecurringChallenges();
    public List<DailyChallengeDefinition> GetChallengesForLevelRange(int minLevel, int maxLevel);
    public List<DailyChallengeDefinition> GetChallengesForPowerup(UID powerupUID);
    public List<DailyChallengeDefinition> GetChallengesForTheme(UID themeUID);
    public List<DailyChallengeDefinition> GetChallengesForBooster(UID boosterUID);
    public List<DailyChallengeDefinition> GetChallengesSortedByDifficulty();
    public List<DailyChallengeDefinition> GetChallengesSortedByTargetValue();
    public List<DailyChallengeDefinition> GetChallengesWithMilestones();
    public List<DailyChallengeDefinition> GetChallengesWithEarlyCompletionBonus();
    public List<DailyChallengeDefinition> GetChallengesWithAnalytics();
    public int GetTotalChallengeCount();
    public int GetChallengeCountByType(ChallengeType type);
    public int GetChallengeCountByDifficulty(ChallengeDifficulty difficulty);
    public float GetCompletionPercentage(List<UID> completedChallenges);
    public float GetCompletionPercentageByType(ChallengeType type, List<UID> completedChallenges);
    public List<DailyChallengeDefinition> GetRandomDailyChallenges(int count, int playerLevel, int currentDayOfWeek, System.Random random);
    public List<DailyChallengeDefinition> GetBalancedDailyChallenges(int count, int playerLevel, int currentDayOfWeek, System.Random random);
}
```

## Usage Examples

### Basic Challenge Tracking

```csharp
// Get challenge by ID
var challenge = metaDataRepository.GetChallengeByID("complete_5_levels");

// Add progress
challenge.AddProgress(1);

// Check if completed
if (challenge.IsCompleted)
{
    // Grant rewards
    foreach (var reward in challenge.Rewards)
    {
        rewardSystem.GrantReward(reward);
    }
    
    // Track analytics
    if (!string.IsNullOrEmpty(challenge.CompletionEventID))
    {
        analyticsSystem.TrackEvent(challenge.CompletionEventID);
    }
}
```

### Level Complete Challenge

```csharp
// Track level completions
public void OnLevelComplete(int level)
{
    var levelChallenges = metaDataRepository.GetChallengesByType(ChallengeType.LevelComplete);
    
    foreach (var challenge in levelChallenges)
    {
        if (!playerData.HasCompletedChallenge(challenge.ChallengeID))
        {
            challenge.AddProgress(1);
            
            // Check for milestone rewards
            var milestoneRewards = challenge.GetMilestoneRewards();
            foreach (var reward in milestoneRewards)
            {
                rewardSystem.GrantReward(reward);
            }
            
            // Check if completed
            if (challenge.IsCompleted)
            {
                GrantChallengeRewards(challenge);
                playerData.MarkChallengeCompleted(challenge.ChallengeID);
            }
        }
    }
}
```

### Powerup Use Challenge

```csharp
// Track powerup usage
public void OnPowerupUsed(UID powerupUID)
{
    var powerupChallenges = metaDataRepository.GetChallengesForPowerup(powerupUID);
    
    foreach (var challenge in powerupChallenges)
    {
        if (!playerData.HasCompletedChallenge(challenge.ChallengeID))
        {
            challenge.AddProgress(1);
            
            if (challenge.IsCompleted)
            {
                GrantChallengeRewards(challenge);
                playerData.MarkChallengeCompleted(challenge.ChallengeID);
            }
        }
    }
}
```

### Daily Challenge Generation

```csharp
// Generate daily challenges
public void GenerateDailyChallenges()
{
    int currentDayOfWeek = (int)System.DateTime.Now.DayOfWeek;
    int playerLevel = playerData.Level;
    
    // Get balanced challenges (mix of difficulties)
    var dailyChallenges = metaDataRepository.DailyChallengesMeta.GetBalancedDailyChallenges(
        3, // Number of challenges
        playerLevel,
        currentDayOfWeek
    );
    
    // Assign to player
    playerData.DailyChallenges = dailyChallenges;
    playerData.DailyChallengeDate = System.DateTime.Today;
}
```

### Early Completion Bonus

```csharp
// Check for early completion bonus
public void OnChallengeComplete(DailyChallengeDefinition challenge, float elapsedTime)
{
    // Grant regular rewards
    foreach (var reward in challenge.Rewards)
    {
        rewardSystem.GrantReward(reward);
    }
    
    // Check for early completion bonus
    if (challenge.IsEligibleForEarlyCompletion(elapsedTime))
    {
        foreach (var reward in challenge.EarlyCompletionBonus)
        {
            rewardSystem.GrantReward(reward);
        }
        
        // Show early completion notification
        uiSystem.ShowEarlyCompletionBonus();
    }
}
```

### Time-Limited Challenge

```csharp
// Handle time-limited challenges
public void UpdateTimeLimitedChallenges()
{
    var timeLimitedChallenges = metaDataRepository.GetTimeLimitedChallenges();
    
    foreach (var challenge in timeLimitedChallenges)
    {
        if (challenge.HasTimeLimit && challenge.IsActive)
        {
            float elapsedTime = Time.time - challenge.StartTime;
            
            if (elapsedTime >= challenge.TimeLimit)
            {
                // Challenge expired
                challenge.IsActive = false;
                uiSystem.ShowChallengeExpired(challenge);
            }
        }
    }
}
```

### Recurring Challenge

```csharp
// Handle recurring challenges
public void OnDailyReset()
{
    var recurringChallenges = metaDataRepository.GetRecurringChallenges();
    
    foreach (var challenge in recurringChallenges)
    {
        // Check if it's time to reset
        int daysSinceLastReset = (System.DateTime.Today - playerData.GetLastResetDate(challenge.ChallengeID)).Days;
        
        if (daysSinceLastReset >= challenge.RecurrenceInterval)
        {
            // Reset progress
            challenge.ResetProgress();
            playerData.SetLastResetDate(challenge.ChallengeID, System.DateTime.Today);
            
            // Mark as not completed
            playerData.MarkChallengeNotCompleted(challenge.ChallengeID);
        }
    }
}
```

### Challenge Completion Percentage

```csharp
// Get overall completion percentage
float completionPercentage = metaDataRepository.GetChallengeCompletionPercentage(playerData.CompletedChallenges);

// Get completion percentage by type
float levelCompletion = metaDataRepository.GetChallengeCompletionPercentageByType(
    ChallengeType.LevelComplete, 
    playerData.CompletedChallenges
);

// Display in UI
completionText.text = $"Daily Challenges: {completionPercentage:F1}%";
```

## Integration with MetaDataRepository

The Daily Challenges System is fully integrated with the MetaDataRepository:

```csharp
// Access challenge data
var challenge = metaDataRepository.GetChallengeByID("complete_5_levels");
var typeChallenges = metaDataRepository.GetChallengesByType(ChallengeType.LevelComplete);
var difficultyChallenges = metaDataRepository.GetChallengesByDifficulty(ChallengeDifficulty.Hard);
var activeChallenges = metaDataRepository.GetActiveChallenges();
var availableChallenges = metaDataRepository.GetAvailableChallenges(playerData.Level, currentDayOfWeek);
var dayChallenges = metaDataRepository.GetChallengesForDay(currentDayOfWeek);
float completionPercentage = metaDataRepository.GetChallengeCompletionPercentage(playerData.CompletedChallenges);

// Get object by UID
var challengeByUID = metaDataRepository.GetObjectByUID<DailyChallengeDefinition>(uid);
```

## Best Practices

### 1. Challenge Design

- **Clear Goals**: Make challenge requirements clear and understandable
- **Varied Difficulty**: Mix easy, medium, and hard challenges
- **Meaningful Rewards**: Provide rewards that match challenge difficulty
- **Achievable**: Ensure challenges are achievable within a day
- **Fun**: Make challenges enjoyable to complete

### 2. Difficulty Distribution

- **Easy (40%)**: Quick challenges for casual players
- **Medium (35%)**: Moderate challenges for engagement
- **Hard (20%)**: Challenging tasks for dedicated players
- **Expert (4%)**: Very hard challenges
- **Master (1%)**: Extremely rare challenges

### 3. Scheduling

- **Daily Rotation**: Rotate challenges daily for variety
- **Day-Based**: Assign specific challenges to specific days
- **Balanced Mix**: Provide a balanced mix of challenge types
- **Player Level**: Scale challenges based on player level

### 4. Progress Tracking

- **Frequent Updates**: Update progress frequently for better feedback
- **Visual Feedback**: Show progress bars and milestones
- **Notifications**: Notify players of progress and completion
- **Persistence**: Save progress and completion status

### 5. Time Limits

- **Reasonable Duration**: Set achievable time limits (24 hours)
- **Clear Communication**: Inform players of time limits
- **Fair Warning**: Warn players before time expires
- **Daily Reset**: Reset challenges at the same time each day

## Advanced Features

### Custom Challenge Types

Use the `Custom` type with `CustomData` for unique challenge criteria:

```csharp
// Custom challenge for completing levels without using powerups
var challenge = metaDataRepository.GetChallengeByID("no_powerup_master");
challenge.CustomData = new Dictionary<string, string>
{
    { "max_powerups", "0" },
    { "min_levels", "5" }
};

// Check in game logic
public void OnLevelComplete(int level, int powerupsUsed)
{
    if (challenge.CustomData.TryGetValue("max_powerups", out string maxPowerupsStr))
    {
        int maxPowerups = int.Parse(maxPowerupsStr);
        if (powerupsUsed <= maxPowerups)
        {
            challenge.AddProgress(1);
        }
    }
}
```

### Challenge Chains

Create chains of challenges where completing one unlocks the next:

```csharp
// Challenge 1: Complete 3 levels
challenge1.TargetValue = 3;

// Challenge 2: Complete 5 levels (requires Challenge 1)
challenge2.TargetValue = 5;
challenge2.MinimumLevel = 5;

// Challenge 3: Complete 10 levels (requires Challenge 2)
challenge3.TargetValue = 10;
challenge3.MinimumLevel = 10;
```

### Milestone Rewards

Provide rewards at partial progress to keep players engaged:

```csharp
// Challenge with milestones
challenge.Milestones = new List<ChallengeMilestone>
{
    new ChallengeMilestone
    {
        ProgressThreshold = 25,
        Rewards = new List<RewardDefinition> { smallReward }
    },
    new ChallengeMilestone
    {
        ProgressThreshold = 50,
        Rewards = new List<RewardDefinition> { mediumReward }
    },
    new ChallengeMilestone
    {
        ProgressThreshold = 75,
        Rewards = new List<RewardDefinition> { largeReward }
    }
};
```

### Seasonal Challenges

Use day-based scheduling for seasonal content:

```csharp
// Halloween challenge (available on October 31st)
var halloweenChallenge = metaDataRepository.GetChallengeByID("halloween_special");
halloweenChallenge.AvailableDays = new List<int> { (int)DayOfWeek.Wednesday }; // Oct 31, 2024 is Wednesday
halloweenChallenge.IsActive = false;

// Activate during Halloween event
public void OnHalloweenEventStart()
{
    halloweenChallenge.IsActive = true;
}
```

## Performance Considerations

1. **Caching**: Cache frequently accessed challenge definitions
2. **Lazy Loading**: Load challenge data only when needed
3. **Batch Operations**: Process multiple challenges in batches
4. **UID Lookups**: Use UID-based lookups for performance
5. **Progress Updates**: Batch progress updates when possible

## Troubleshooting

### Common Issues

**Issue**: Challenge not triggering
- **Solution**: Check `IsActive` flag and `MinimumLevel` requirement

**Issue**: Challenges not available on specific days
- **Solution**: Check `AvailableDays` list and current day of week

**Issue**: Progress not saving
- **Solution**: Ensure challenge progress is persisted in player data

**Issue**: Milestones not rewarding
- **Solution**: Check `Rewarded` flag and `ProgressThreshold` values

**Issue**: Recurring challenges not resetting
- **Solution**: Check `RecurrenceInterval` and reset logic

## Future Enhancements

Potential improvements for the Daily Challenges System:

1. **Challenge Categories**: Group challenges by category
2. **Challenge Sets**: Collections of related challenges
3. **Leaderboards**: Compare challenge completion with friends
4. **Challenge Sharing**: Share challenges on social media
5. **Dynamic Challenges**: Generate challenges based on player behavior
6. **Challenge Guides**: In-game hints for difficult challenges
7. **Challenge Notifications**: Push notifications for new challenges
8. **Challenge Badges**: Visual badges for completed challenges

## Conclusion

The Daily Challenges System provides a solid foundation for managing daily challenges in your game. Its data-driven approach and integration with other metadata systems make it flexible and maintainable across multiple projects.