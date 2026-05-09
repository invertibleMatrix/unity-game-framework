# Difficulty System Documentation

## Overview

The Difficulty System provides a comprehensive framework for defining and managing game difficulty settings. It follows the same architectural patterns as other metadata systems in your codebase, using ScriptableObjects for data-driven configuration and UID-based lookups.

## Architecture

### Core Components

1. **DifficultyType** - Enum defining difficulty categories
2. **DifficultyDefinition** - Main difficulty definition with gameplay parameters
3. **DifficultyRegistry** - UID-based registry for difficulty definitions
4. **DifficultyMeta** - Container with query methods for difficulties

### Difficulty Types

| Type | Description |
|------|-------------|
| `Tutorial` | Very easy, for teaching mechanics |
| `VeryEasy` | For beginners |
| `Easy` | For casual players |
| `Normal` | Standard gameplay |
| `Hard` | For experienced players |
| `VeryHard` | For expert players |
| `Expert` | For master players |
| `Master` | For the most skilled players |
| `Insane` | Extremely challenging |
| `Custom` | Custom difficulty type |

## DifficultyDefinition Structure

### Identification
- **UID**: Unique identifier for the difficulty
- **DisplayName**: Display name shown to players
- **InternalName**: Internal name for reference

### Classification
- **Type**: Difficulty category (DifficultyType enum)
- **DifficultyLevel**: Difficulty level (1-10, higher = harder)

### Level Range
- **MinLevel**: Minimum level this difficulty applies to
- **MaxLevel**: Maximum level this difficulty applies to

### Bubble Settings
- **BubbleColorCount**: Number of bubble colors available (2-10)
- **MinClusterSize**: Minimum bubble cluster size to pop (2-5)
- **MaxClusterSize**: Maximum bubble cluster size to pop (3-10)
- **BubbleSpawnRate**: Bubble spawn rate multiplier (0.5-2)

### Time Settings
- **TimeLimit**: Time limit for level (0 = no limit)
- **TimeBonusPerBubble**: Time bonus per bubble popped (0-10)
- **TimePenaltyPerShot**: Time penalty per shot (0-5)

### Shot Settings
- **MaxShots**: Maximum number of shots allowed (0 = unlimited)
- **RequiredAccuracy**: Shot accuracy required (0-1, higher = more accurate)
- **AimAssistStrength**: Aim assist strength (0 = none, 1 = full)

### Special Tiles
- **EnableSpecialTiles**: Enable special tiles
- **SpecialTileSpawnChance**: Special tile spawn chance (0-1)
- **MaxSpecialTiles**: Maximum special tiles on screen (0-20)

### Powerups
- **EnablePowerups**: Enable powerups
- **PowerupSpawnChance**: Powerup spawn chance (0-1)
- **MaxPowerups**: Maximum powerups on screen (0-10)

### AI/Enemy Settings
- **EnableEnemies**: Enable AI enemies
- **EnemySpawnRate**: Enemy spawn rate multiplier (0.5-2)
- **EnemySpeedMultiplier**: Enemy speed multiplier (0.5-2)
- **EnemyHealthMultiplier**: Enemy health multiplier (0.5-2)

### Scoring
- **ScoreMultiplier**: Score multiplier (0.5-3)
- **ComboMultiplier**: Combo multiplier (1-5)
- **StarScoreThresholds**: Star score thresholds (1, 2, 3 stars)

### Lives
- **Lives**: Number of lives (1-10)
- **LivesLostPerFailedShot**: Lives lost per failed shot (0-3)
- **LivesLostPerTimeLimit**: Lives lost per time limit (0-3)

### Visual Settings
- **ShowHints**: Show hints
- **HintDelay**: Hint delay in seconds (0-30)
- **ShowTrajectory**: Show trajectory preview
- **TrajectoryLength**: Trajectory length (0-10)

### Progression
- **ProgressionSpeed**: Level progression speed (0.5-2, higher = faster)
- **DifficultyIncreasePerLevel**: Difficulty increase per level (0-0.5)

### Rewards
- **RewardMultiplier**: Reward multiplier (0.5-3)
- **BonusRewards**: Bonus rewards for completion

## Usage Examples

### Creating a Difficulty Definition

```csharp
// Create a new DifficultyDefinition asset
DifficultyDefinition difficulty = ScriptableObject.CreateInstance<DifficultyDefinition>();
difficulty.UID = new UID("difficulty_normal");
difficulty.DisplayName = "Normal";
difficulty.InternalName = "normal_difficulty";
difficulty.Type = DifficultyType.Normal;
difficulty.DifficultyLevel = 5;
difficulty.MinLevel = 1;
difficulty.MaxLevel = 50;
difficulty.BubbleColorCount = 5;
difficulty.MinClusterSize = 3;
difficulty.MaxClusterSize = 5;
difficulty.BubbleSpawnRate = 1f;
difficulty.TimeLimit = 0;
difficulty.MaxShots = 0;
difficulty.RequiredAccuracy = 0f;
difficulty.AimAssistStrength = 0.5f;
difficulty.EnableSpecialTiles = true;
difficulty.SpecialTileSpawnChance = 0.1f;
difficulty.MaxSpecialTiles = 5;
difficulty.EnablePowerups = true;
difficulty.PowerupSpawnChance = 0.05f;
difficulty.MaxPowerups = 3;
difficulty.EnableEnemies = false;
difficulty.ScoreMultiplier = 1f;
difficulty.ComboMultiplier = 1.5f;
difficulty.StarScoreThresholds = new Vector3(1000, 2000, 3000);
difficulty.Lives = 3;
difficulty.ShowHints = true;
difficulty.HintDelay = 10f;
difficulty.ShowTrajectory = true;
difficulty.TrajectoryLength = 5;
difficulty.ProgressionSpeed = 1f;
difficulty.DifficultyIncreasePerLevel = 0.05f;
difficulty.RewardMultiplier = 1f;
```

### Querying Difficulties

```csharp
// Get difficulty by UID
DifficultyDefinition difficulty = metaDataRepository.DifficultyMeta.GetDifficulty(new UID("difficulty_normal"));

// Get all difficulties
IReadOnlyList<DifficultyDefinition> allDifficulties = metaDataRepository.DifficultyMeta.GetAllDifficulties();

// Get difficulties by type
List<DifficultyDefinition> normalDifficulties = metaDataRepository.DifficultyMeta.GetDifficultiesByType(DifficultyType.Normal);

// Get difficulties for a specific level
List<DifficultyDefinition> levelDifficulties = metaDataRepository.DifficultyMeta.GetDifficultiesForLevel(10);

// Get difficulties sorted by difficulty level
List<DifficultyDefinition> sortedDifficulties = metaDataRepository.DifficultyMeta.GetDifficultiesByLevel();

// Get difficulty by level
DifficultyDefinition level5Difficulty = metaDataRepository.DifficultyMeta.GetDifficultyByLevel(5);

// Get best matching difficulty for a level
DifficultyDefinition bestDifficulty = metaDataRepository.DifficultyMeta.GetDifficultyForLevel(10);
```

### Getting Specific Difficulty Types

```csharp
// Get tutorial difficulties
List<DifficultyDefinition> tutorialDifficulties = metaDataRepository.DifficultyMeta.GetTutorialDifficulties();

// Get easy difficulties
List<DifficultyDefinition> easyDifficulties = metaDataRepository.DifficultyMeta.GetEasyDifficulties();

// Get normal difficulties
List<DifficultyDefinition> normalDifficulties = metaDataRepository.DifficultyMeta.GetNormalDifficulties();

// Get hard difficulties
List<DifficultyDefinition> hardDifficulties = metaDataRepository.DifficultyMeta.GetHardDifficulties();

// Get expert difficulties
List<DifficultyDefinition> expertDifficulties = metaDataRepository.DifficultyMeta.GetExpertDifficulties();
```

### Getting Difficulties with Specific Features

```csharp
// Get difficulties with special tiles
List<DifficultyDefinition> withSpecialTiles = metaDataRepository.DifficultyMeta.GetDifficultiesWithSpecialTiles();

// Get difficulties with powerups
List<DifficultyDefinition> withPowerups = metaDataRepository.DifficultyMeta.GetDifficultiesWithPowerups();

// Get difficulties with enemies
List<DifficultyDefinition> withEnemies = metaDataRepository.DifficultyMeta.GetDifficultiesWithEnemies();

// Get difficulties with time limits
List<DifficultyDefinition> withTimeLimit = metaDataRepository.DifficultyMeta.GetDifficultiesWithTimeLimit();

// Get difficulties with shot limits
List<DifficultyDefinition> withShotLimit = metaDataRepository.DifficultyMeta.GetDifficultiesWithShotLimit();
```

### Using Difficulty Settings in Gameplay

```csharp
public class LevelManager : MonoBehaviour
{
    private IMetaDataRepository _metaDataRepository;
    private DifficultyDefinition _currentDifficulty;
    
    public void SetupLevel(int level)
    {
        // Get difficulty for this level
        _currentDifficulty = _metaDataRepository.DifficultyMeta.GetDifficultyForLevel(level);
        
        if (_currentDifficulty == null)
        {
            Debug.LogWarning($"No difficulty found for level {level}");
            return;
        }
        
        // Apply difficulty settings
        ApplyDifficultySettings();
    }
    
    private void ApplyDifficultySettings()
    {
        // Apply bubble settings
        BubbleManager.SetColorCount(_currentDifficulty.BubbleColorCount);
        BubbleManager.SetClusterSizeRange(_currentDifficulty.MinClusterSize, _currentDifficulty.MaxClusterSize);
        BubbleManager.SetSpawnRate(_currentDifficulty.BubbleSpawnRate);
        
        // Apply time settings
        if (_currentDifficulty.TimeLimit > 0)
        {
            TimerManager.SetTimeLimit(_currentDifficulty.TimeLimit);
            TimerManager.SetTimeBonusPerBubble(_currentDifficulty.TimeBonusPerBubble);
            TimerManager.SetTimePenaltyPerShot(_currentDifficulty.TimePenaltyPerShot);
        }
        
        // Apply shot settings
        if (_currentDifficulty.MaxShots > 0)
        {
            ShotManager.SetMaxShots(_currentDifficulty.MaxShots);
        }
        ShotManager.SetRequiredAccuracy(_currentDifficulty.RequiredAccuracy);
        ShotManager.SetAimAssistStrength(_currentDifficulty.AimAssistStrength);
        
        // Apply special tiles
        SpecialTileManager.SetEnabled(_currentDifficulty.EnableSpecialTiles);
        SpecialTileManager.SetSpawnChance(_currentDifficulty.SpecialTileSpawnChance);
        SpecialTileManager.SetMaxCount(_currentDifficulty.MaxSpecialTiles);
        
        // Apply powerups
        PowerupManager.SetEnabled(_currentDifficulty.EnablePowerups);
        PowerupManager.SetSpawnChance(_currentDifficulty.PowerupSpawnChance);
        PowerupManager.SetMaxCount(_currentDifficulty.MaxPowerups);
        
        // Apply enemies
        EnemyManager.SetEnabled(_currentDifficulty.EnableEnemies);
        EnemyManager.SetSpawnRate(_currentDifficulty.EnemySpawnRate);
        EnemyManager.SetSpeedMultiplier(_currentDifficulty.EnemySpeedMultiplier);
        EnemyManager.SetHealthMultiplier(_currentDifficulty.EnemyHealthMultiplier);
        
        // Apply scoring
        ScoreManager.SetScoreMultiplier(_currentDifficulty.ScoreMultiplier);
        ScoreManager.SetComboMultiplier(_currentDifficulty.ComboMultiplier);
        
        // Apply lives
        LivesManager.SetLives(_currentDifficulty.Lives);
        LivesManager.SetLivesLostPerFailedShot(_currentDifficulty.LivesLostPerFailedShot);
        LivesManager.SetLivesLostPerTimeLimit(_currentDifficulty.LivesLostPerTimeLimit);
        
        // Apply visual settings
        HintManager.SetEnabled(_currentDifficulty.ShowHints);
        HintManager.SetDelay(_currentDifficulty.HintDelay);
        TrajectoryManager.SetEnabled(_currentDifficulty.ShowTrajectory);
        TrajectoryManager.SetLength(_currentDifficulty.TrajectoryLength);
        
        // Apply progression
        ProgressionManager.SetSpeed(_currentDifficulty.ProgressionSpeed);
        ProgressionManager.SetDifficultyIncrease(_currentDifficulty.DifficultyIncreasePerLevel);
        
        // Apply rewards
        RewardManager.SetMultiplier(_currentDifficulty.RewardMultiplier);
    }
    
    public int GetStarScoreThreshold(int stars)
    {
        return _currentDifficulty.GetStarScoreThreshold(stars);
    }
    
    public float GetAdjustedDifficulty(int level)
    {
        return _currentDifficulty.GetAdjustedDifficulty(level);
    }
}
```

### Checking Difficulty Conditions

```csharp
DifficultyDefinition difficulty = metaDataRepository.DifficultyMeta.GetDifficulty(new UID("difficulty_normal"));

// Check if difficulty applies to level
bool applies = difficulty.AppliesToLevel(currentLevel);

// Get star score threshold
int oneStarThreshold = difficulty.GetStarScoreThreshold(1);
int twoStarThreshold = difficulty.GetStarScoreThreshold(2);
int threeStarThreshold = difficulty.GetStarScoreThreshold(3);

// Get adjusted difficulty for level
float adjustedDifficulty = difficulty.GetAdjustedDifficulty(currentLevel);
```

### Getting Difficulty Statistics

```csharp
// Get minimum difficulty level
int minLevel = metaDataRepository.DifficultyMeta.GetMinDifficultyLevel();

// Get maximum difficulty level
int maxLevel = metaDataRepository.DifficultyMeta.GetMaxDifficultyLevel();

// Get average difficulty level
float avgLevel = metaDataRepository.DifficultyMeta.GetAverageDifficultyLevel();
```

## Integration with MetaDataRepository

The Difficulty System is integrated into the existing [`IMetaDataRepository`](../IMetaDataRepository.cs) and [`MetaDataRepository`](../MetaDataRepository.cs):

```csharp
// In IMetaDataRepository
public DifficultyRegistry DifficultyRegistry { get; }
public DifficultyMeta DifficultyMeta { get; }

// In MetaDataRepository
[SerializeField] private DifficultyRegistry _difficultyRegistry;
[SerializeField] private DifficultyMeta _difficultyMeta;
public DifficultyRegistry DifficultyRegistry => _difficultyRegistry;
public DifficultyMeta DifficultyMeta => _difficultyMeta;

// In GetObjectByUID
if (typeof(T) == typeof(DifficultyDefinition))
{
    return _difficultyMeta.Registry.Definitions.FirstOrDefault(d => d.UID == uid) as T;
}
```

## Best Practices

1. **Use Clear Difficulty Names**: Give difficulties clear, descriptive display names
2. **Set Appropriate Level Ranges**: Use MinLevel and MaxLevel to control when difficulties apply
3. **Balance Parameters**: Carefully balance all parameters to create fair difficulty curves
4. **Test Thoroughly**: Test difficulties at different levels and with different player skills
5. **Use Progression**: Leverage DifficultyIncreasePerLevel to create smooth difficulty curves
6. **Provide Visual Feedback**: Use hints and trajectory to help players understand the game
7. **Reward Appropriately**: Adjust rewards based on difficulty to incentivize challenge
8. **Consider Player Skill**: Offer multiple difficulty options to accommodate different skill levels
9. **Monitor Metrics**: Track player performance to fine-tune difficulty settings
10. **Iterate and Refine**: Continuously adjust difficulty based on player feedback and data

## Common Use Cases

### Tutorial Difficulty

```csharp
DifficultyDefinition tutorial = new DifficultyDefinition
{
    Type = DifficultyType.Tutorial,
    DifficultyLevel = 1,
    MinLevel = 1,
    MaxLevel = 1,
    BubbleColorCount = 2,
    MinClusterSize = 2,
    MaxClusterSize = 3,
    BubbleSpawnRate = 0.5f,
    TimeLimit = 0,
    MaxShots = 0,
    RequiredAccuracy = 0f,
    AimAssistStrength = 1f,
    EnableSpecialTiles = false,
    EnablePowerups = false,
    EnableEnemies = false,
    ScoreMultiplier = 0.5f,
    ComboMultiplier = 1f,
    StarScoreThresholds = new Vector3(500, 1000, 1500),
    Lives = 5,
    ShowHints = true,
    HintDelay = 5f,
    ShowTrajectory = true,
    TrajectoryLength = 10,
    ProgressionSpeed = 0.5f,
    DifficultyIncreasePerLevel = 0f,
    RewardMultiplier = 0.5f
};
```

### Normal Difficulty

```csharp
DifficultyDefinition normal = new DifficultyDefinition
{
    Type = DifficultyType.Normal,
    DifficultyLevel = 5,
    MinLevel = 1,
    MaxLevel = 50,
    BubbleColorCount = 5,
    MinClusterSize = 3,
    MaxClusterSize = 5,
    BubbleSpawnRate = 1f,
    TimeLimit = 0,
    MaxShots = 0,
    RequiredAccuracy = 0f,
    AimAssistStrength = 0.5f,
    EnableSpecialTiles = true,
    SpecialTileSpawnChance = 0.1f,
    MaxSpecialTiles = 5,
    EnablePowerups = true,
    PowerupSpawnChance = 0.05f,
    MaxPowerups = 3,
    EnableEnemies = false,
    ScoreMultiplier = 1f,
    ComboMultiplier = 1.5f,
    StarScoreThresholds = new Vector3(1000, 2000, 3000),
    Lives = 3,
    ShowHints = true,
    HintDelay = 10f,
    ShowTrajectory = true,
    TrajectoryLength = 5,
    ProgressionSpeed = 1f,
    DifficultyIncreasePerLevel = 0.05f,
    RewardMultiplier = 1f
};
```

### Hard Difficulty

```csharp
DifficultyDefinition hard = new DifficultyDefinition
{
    Type = DifficultyType.Hard,
    DifficultyLevel = 7,
    MinLevel = 50,
    MaxLevel = 100,
    BubbleColorCount = 7,
    MinClusterSize = 3,
    MaxClusterSize = 4,
    BubbleSpawnRate = 1.5f,
    TimeLimit = 120,
    MaxShots = 0,
    RequiredAccuracy = 0.5f,
    AimAssistStrength = 0.2f,
    EnableSpecialTiles = true,
    SpecialTileSpawnChance = 0.2f,
    MaxSpecialTiles = 10,
    EnablePowerups = true,
    PowerupSpawnChance = 0.03f,
    MaxPowerups = 2,
    EnableEnemies = true,
    EnemySpawnRate = 1.2f,
    EnemySpeedMultiplier = 1.2f,
    EnemyHealthMultiplier = 1.2f,
    ScoreMultiplier = 1.5f,
    ComboMultiplier = 2f,
    StarScoreThresholds = new Vector3(2000, 4000, 6000),
    Lives = 2,
    ShowHints = false,
    HintDelay = 15f,
    ShowTrajectory = false,
    TrajectoryLength = 3,
    ProgressionSpeed = 1.2f,
    DifficultyIncreasePerLevel = 0.1f,
    RewardMultiplier = 1.5f
};
```

## Summary

The Difficulty System provides a flexible, data-driven framework for creating and managing game difficulty settings. It integrates seamlessly with your existing metadata architecture and supports:

- Multiple difficulty types and categories
- Comprehensive gameplay parameter control
- Level-based difficulty progression
- Dynamic difficulty adjustment
- Visual and accessibility settings
- Reward scaling based on difficulty
- Enemy and special tile configuration
- Time and shot limit options

This system is designed to be reusable across future games in your codebase, following the same patterns as your IAP, Ads, and other metadata systems.