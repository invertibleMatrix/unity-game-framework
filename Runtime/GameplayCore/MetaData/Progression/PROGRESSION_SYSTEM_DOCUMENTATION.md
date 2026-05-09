# Progression System Documentation

## Overview

The Progression System provides a flexible, data-driven framework for managing player progression through levels and XP. It supports player levels, XP requirements, milestone rewards, unlocks, and prestige mechanics.

## Architecture

### Core Components

```
Progression/
├── ProgressionLevel.cs          # Player level definition
├── MilestoneDefinition.cs       # Milestone with rewards
├── ProgressionRegistry.cs       # UID-based registry
└── ProgressionMeta.cs           # Query methods and logic
```

### Design Principles

1. **Data-Driven**: All progression data configured in Unity Editor
2. **UID-Based**: Uses AK.Utilities UID for unique identification
3. **Flexible**: Supports multiple progression paths and milestones
4. **Reward Integration**: Seamless integration with Rewards system
5. **Scalable**: Easy to add new levels and milestones

## Core Types

### ProgressionLevel

Represents a player level with XP requirements and rewards.

```csharp
public class ProgressionLevel : MetaDataAsset
{
    public UID UID;
    public int LevelNumber;
    public int XPRequired;
    public List<RewardDefinition> Rewards;
    public List<UID> Unlocks;
    public bool IsPrestigeLevel;
    public int PrestigeMultiplier;
    public string DisplayName;
    [TextArea] public string Description;
}
```

**Properties:**
- `UID`: Unique identifier for this level
- `LevelNumber`: The level number (1, 2, 3, etc.)
- `XPRequired`: Total XP needed to reach this level
- `Rewards`: Rewards granted when reaching this level
- `Unlocks`: UIDs of items/features unlocked at this level
- `IsPrestigeLevel`: Whether this is a prestige milestone
- `PrestigeMultiplier`: XP multiplier for prestige players
- `DisplayName`: Display name for UI
- `Description`: Description text for UI

### MilestoneDefinition

Represents a milestone that can be achieved at specific levels or XP thresholds.

```csharp
public class MilestoneDefinition : MetaDataAsset
{
    public UID UID;
    public string MilestoneID;
    public string DisplayName;
    [TextArea] public string Description;
    public int RequiredLevel;
    public int RequiredXP;
    public List<RewardDefinition> Rewards;
    public bool IsRepeatable;
    public int RepeatInterval;
    public bool IsOneTime;
    public bool IsActive;
}
```

**Properties:**
- `UID`: Unique identifier
- `MilestoneID`: String ID for reference
- `DisplayName`: Display name for UI
- `Description`: Description text
- `RequiredLevel`: Minimum level to achieve
- `RequiredXP`: Minimum XP to achieve
- `Rewards`: Rewards granted on achievement
- `IsRepeatable`: Can be achieved multiple times
- `RepeatInterval`: Levels/XP between repeats
- `IsOneTime`: Can only be achieved once
- `IsActive`: Whether this milestone is currently active

### ProgressionRegistry

UID-based registry for managing progression levels.

```csharp
public class ProgressionRegistry : TypedUIDRegistryAsset<ProgressionLevel>
{
    // Inherits from TypedUIDRegistryAsset
    // Provides UID-based lookup and validation
}
```

### ProgressionMeta

Container for progression data with query methods.

```csharp
public class ProgressionMeta : MetaDataAsset
{
    public List<ProgressionLevel> Levels;
    public List<MilestoneDefinition> Milestones;
    
    // Query methods
    public ProgressionLevel GetLevel(int levelNumber);
    public List<ProgressionLevel> GetLevelsInRange(int start, int end);
    public List<MilestoneDefinition> GetMilestonesForLevel(int level);
    public List<MilestoneDefinition> GetMilestonesForXP(int xp);
    public int GetTotalXPForLevel(int level);
    public int GetMaxLevel();
}
```

## Usage Examples

### Basic Level Progression

```csharp
// Get the current level definition
var currentLevel = metaDataRepository.GetProgressionLevel(playerLevel);

// Get rewards for reaching this level
var rewards = currentLevel.Rewards;

// Grant rewards
foreach (var reward in rewards)
{
    rewardSystem.GrantReward(reward);
}

// Check for unlocks
foreach (var unlockUID in currentLevel.Unlocks)
{
    var unlockedItem = metaDataRepository.GetObjectByUID<ScriptableObject>(unlockUID);
    // Enable the unlocked item
}
```

### Milestone Tracking

```csharp
// Get milestones for current level
var milestones = metaDataRepository.GetMilestonesForLevel(playerLevel);

foreach (var milestone in milestones)
{
    if (!milestone.IsActive) continue;
    
    // Check if player has already achieved this milestone
    if (!playerData.HasAchievedMilestone(milestone.MilestoneID))
    {
        // Grant milestone rewards
        foreach (var reward in milestone.Rewards)
        {
            rewardSystem.GrantReward(reward);
        }
        
        // Mark as achieved
        playerData.MarkMilestoneAchieved(milestone.MilestoneID);
    }
}
```

### XP Calculation

```csharp
// Calculate total XP needed for a level
int totalXPNeeded = metaDataRepository.GetTotalXPForLevel(targetLevel);

// Calculate XP progress
int currentXP = playerData.TotalXP;
int progress = currentXP - metaDataRepository.GetTotalXPForLevel(playerLevel);
int needed = metaDataRepository.GetTotalXPForLevel(playerLevel + 1) - 
             metaDataRepository.GetTotalXPForLevel(playerLevel);
float progressPercent = (float)progress / needed;
```

### Level Up Logic

```csharp
public void AddXP(int xpAmount)
{
    playerData.TotalXP += xpAmount;
    
    // Check for level up
    int newLevel = CalculateLevelFromXP(playerData.TotalXP);
    
    while (newLevel > playerData.Level)
    {
        LevelUp();
        playerData.Level++;
        newLevel = CalculateLevelFromXP(playerData.TotalXP);
    }
}

private void LevelUp()
{
    var levelDef = metaDataRepository.GetProgressionLevel(playerData.Level + 1);
    
    // Grant level rewards
    foreach (var reward in levelDef.Rewards)
    {
        rewardSystem.GrantReward(reward);
    }
    
    // Process unlocks
    foreach (var unlockUID in levelDef.Unlocks)
    {
        UnlockFeature(unlockUID);
    }
    
    // Check for milestones
    var milestones = metaDataRepository.GetMilestonesForLevel(playerData.Level + 1);
    foreach (var milestone in milestones)
    {
        if (milestone.IsActive && !playerData.HasAchievedMilestone(milestone.MilestoneID))
        {
            GrantMilestoneRewards(milestone);
        }
    }
}
```

### Prestige System

```csharp
public void Prestige()
{
    var prestigeLevel = metaDataRepository.GetProgressionLevel(playerData.Level);
    
    if (!prestigeLevel.IsPrestigeLevel)
    {
        Debug.LogWarning("Cannot prestige at current level");
        return;
    }
    
    // Reset progress
    playerData.Level = 1;
    playerData.TotalXP = 0;
    
    // Apply prestige multiplier
    playerData.PrestigeLevel++;
    playerData.XPMultiplier = prestigeLevel.PrestigeMultiplier;
    
    // Grant prestige rewards
    foreach (var reward in prestigeLevel.Rewards)
    {
        rewardSystem.GrantReward(reward);
    }
}
```

## Integration with MetaDataRepository

The Progression System is fully integrated with the MetaDataRepository:

```csharp
// Access progression data
var level = metaDataRepository.GetProgressionLevel(5);
var levels = metaDataRepository.GetProgressionLevelsInRange(1, 10);
var milestones = metaDataRepository.GetMilestonesForLevel(5);
var xpMilestones = metaDataRepository.GetMilestonesForXP(1000);
int totalXP = metaDataRepository.GetTotalXPForLevel(10);
int maxLevel = metaDataRepository.GetMaxLevel();

// Get object by UID
var levelByUID = metaDataRepository.GetObjectByUID<ProgressionLevel>(uid);
```

## Best Practices

### 1. Level Design

- **Progressive Difficulty**: Increase XP requirements gradually
- **Meaningful Rewards**: Each level should provide value
- **Clear Unlocks**: Unlock features at appropriate progression points
- **Prestige Points**: Design prestige levels as major milestones

### 2. Milestone Design

- **Varied Requirements**: Mix level-based and XP-based milestones
- **Achievable Goals**: Make milestones challenging but reachable
- **Clear Rewards**: Provide meaningful rewards for achievements
- **Repeatable Content**: Use repeatable milestones for engagement

### 3. XP Balancing

- **Early Game**: Fast progression to hook players
- **Mid Game**: Moderate progression to maintain engagement
- **Late Game**: Slower progression with meaningful rewards
- **Prestige**: Reset with permanent bonuses for replayability

### 4. Data Organization

- **Group by Tiers**: Organize levels into tiers (1-10, 11-20, etc.)
- **Consistent Naming**: Use clear, descriptive names
- **Documentation**: Add descriptions for each level and milestone
- **Testing**: Test progression curve thoroughly

## Advanced Features

### Custom Progression Curves

You can implement custom XP curves by modifying the `XPRequired` values:

```csharp
// Linear progression
level1.XPRequired = 100;
level2.XPRequired = 200;
level3.XPRequired = 300;

// Exponential progression
level1.XPRequired = 100;
level2.XPRequired = 250;
level3.XPRequired = 500;

// Custom curve
level1.XPRequired = 100;
level2.XPRequired = 150;
level3.XPRequired = 300;
level4.XPRequired = 600;
```

### Conditional Milestones

Use the `IsActive` flag to enable/disable milestones based on events:

```csharp
// Enable seasonal milestones
public void OnSeasonStart(Season season)
{
    foreach (var milestone in metaDataRepository.ProgressionMeta.Milestones)
    {
        if (milestone.MilestoneID.StartsWith(season.Name))
        {
            milestone.IsActive = true;
        }
    }
}
```

### Dynamic Rewards

Combine with other systems for dynamic rewards:

```csharp
// Grant IAP products as level rewards
var level = metaDataRepository.GetProgressionLevel(10);
var iapProduct = metaDataRepository.GetIAPProductByID("starter_pack");
level.Rewards.Add(new RewardDefinition
{
    RewardType = RewardType.IAPProduct,
    ProductID = iapProduct.ProductID
});
```

## Performance Considerations

1. **Caching**: Cache frequently accessed level definitions
2. **Lazy Loading**: Load milestone data only when needed
3. **Batch Operations**: Process multiple milestones in batches
4. **UID Lookups**: Use UID-based lookups for performance

## Troubleshooting

### Common Issues

**Issue**: Level not found
- **Solution**: Ensure level numbers are sequential and start from 1

**Issue**: Milestones not triggering
- **Solution**: Check `IsActive` flag and `RequiredLevel`/`RequiredXP` values

**Issue**: XP calculation incorrect
- **Solution**: Verify `XPRequired` values are cumulative, not incremental

**Issue**: Unlocks not working
- **Solution**: Ensure unlock UIDs are valid and registered in UIDRegistry

## Future Enhancements

Potential improvements for the Progression System:

1. **Multiple Progression Paths**: Support for different progression tracks
2. **Seasonal Progression**: Time-limited progression systems
3. **Guild Progression**: Shared progression for groups
4. **Achievement Integration**: Link with achievement system
5. **Dynamic Difficulty**: Adjust progression based on player behavior

## Conclusion

The Progression System provides a solid foundation for managing player progression in your game. Its data-driven approach and integration with other metadata systems make it flexible and maintainable across multiple projects.