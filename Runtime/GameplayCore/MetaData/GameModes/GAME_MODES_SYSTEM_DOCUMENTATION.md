# Game Modes System Documentation

## Overview

The Game Modes System provides a comprehensive framework for defining and managing different game modes. It follows the same architectural patterns as other metadata systems in your codebase, using ScriptableObjects for data-driven configuration and UID-based lookups.

## Architecture

### Core Components

1. **GameModeType** - Enum defining game mode categories
2. **GameModeDefinition** - Main game mode definition with rules, objectives, and settings
3. **GameModesRegistry** - UID-based registry for game mode definitions
4. **GameModesMeta** - Container with query methods for game modes

### Game Mode Types

| Type | Description |
|------|-------------|
| `Campaign` | Standard campaign mode with level progression |
| `Endless` | Endless mode with increasing difficulty |
| `TimeAttack` | Time-limited challenge mode |
| `ShotLimit` | Limited shots challenge mode |
| `Survival` | Survival mode with limited lives |
| `Puzzle` | Puzzle mode with specific objectives |
| `Versus` | Multiplayer competitive mode |
| `Cooperative` | Cooperative multiplayer mode |
| `DailyChallenge` | Daily challenge mode |
| `Event` | Special event mode |
| `Practice` | Practice mode for learning |
| `Custom` | Custom game mode type |

## GameModeDefinition Structure

### Identification
- **UID**: Unique identifier for the game mode
- **DisplayName**: Display name shown to players
- **InternalName**: Internal name for reference

### Classification
- **Type**: Game mode category (GameModeType enum)
- **Priority**: Display priority (0-100, lower = higher priority)

### Availability
- **MinPlayerLevel**: Minimum player level required to unlock
- **AlwaysAvailable**: Is this mode always available
- **IsFeatured**: Is this mode featured/highlighted
- **IsActive**: Is this mode currently active

### Description
- **ShortDescription**: Short description of the game mode
- **FullDescription**: Full description of the game mode

### Objectives
- **PrimaryObjective**: Primary objective description
- **SecondaryObjectives**: List of secondary objectives

### Rules
- **Rules**: Game rules description
- **WinConditions**: List of win conditions
- **LoseConditions**: List of lose conditions

### Time Settings
- **HasTimeLimit**: Has time limit
- **TimeLimit**: Time limit in seconds (0 = unlimited)
- **TimeBonusPerObjective**: Time bonus per objective completed (0-60)
- **TimePenaltyPerMistake**: Time penalty per mistake (0-30)

### Shot Settings
- **HasShotLimit**: Has shot limit
- **MaxShots**: Maximum shots allowed (0 = unlimited)
- **ShotsBonusPerObjective**: Shots bonus per objective completed (0-10)

### Lives Settings
- **HasLives**: Has lives
- **Lives**: Number of lives (1-10)
- **LivesLostPerMistake**: Lives lost per mistake (0-3)

### Scoring
- **ScoreMultiplier**: Score multiplier (0.5-3)
- **ComboMultiplier**: Combo multiplier (1-5)
- **CompletionBonus**: Bonus score for completion

### Difficulty
- **DifficultyLevel**: Difficulty level (1-10)
- **DifficultyIncreases**: Difficulty increases over time
- **DifficultyIncreaseRate**: Difficulty increase rate (0-1)

### Progression
- **HasLevelProgression**: Has level progression
- **StartingLevel**: Starting level
- **MaxLevel**: Maximum level (0 = unlimited)
- **AutoAdvanceLevel**: Auto-advance to next level on completion

### Special Features
- **EnableSpecialTiles**: Enable special tiles
- **EnablePowerups**: Enable powerups
- **EnableBoosters**: Enable boosters
- **EnableEnemies**: Enable enemies
- **EnableObstacles**: Enable obstacles

### Multiplayer
- **IsMultiplayer**: Is multiplayer mode
- **MaxPlayers**: Maximum players (2-10)
- **IsCooperative**: Is cooperative
- **IsCompetitive**: Is competitive

### Leaderboard
- **HasLeaderboard**: Has leaderboard
- **LeaderboardType**: Leaderboard type (Global, Friends, Regional, Country, Local)
- **LeaderboardResetFrequency**: Leaderboard reset frequency (Never, Daily, Weekly, Monthly, Seasonal)

### Rewards
- **CompletionRewards**: Rewards for completing the mode
- **RewardMultiplier**: Reward multiplier (0.5-3)
- **HighScoreRewards**: Bonus rewards for high scores

### Visual Settings
- **Icon**: Icon for the game mode
- **BackgroundImage**: Background image
- **ThemeColor**: Theme color

### Analytics
- **StartEvent**: Analytics event for mode start
- **CompleteEvent**: Analytics event for mode complete
- **FailEvent**: Analytics event for mode fail

## Usage Examples

### Creating a Game Mode Definition

```csharp
// Create a new GameModeDefinition asset
GameModeDefinition gameMode = ScriptableObject.CreateInstance<GameModeDefinition>();
gameMode.UID = new UID("gamemode_campaign");
gameMode.DisplayName = "Campaign";
gameMode.InternalName = "campaign_mode";
gameMode.Type = GameModeType.Campaign;
gameMode.Priority = 0;
gameMode.MinPlayerLevel = 1;
gameMode.AlwaysAvailable = true;
gameMode.IsFeatured = false;
gameMode.IsActive = true;
gameMode.ShortDescription = "Play through levels and progress through the story.";
gameMode.FullDescription = "Experience the main campaign with increasing difficulty and unlock new features as you progress.";
gameMode.PrimaryObjective = "Complete all levels with 3 stars.";
gameMode.SecondaryObjectives.Add("Collect all powerups");
gameMode.SecondaryObjectives.Add("Unlock all achievements");
gameMode.Rules = "Match 3 or more bubbles of the same color to pop them. Clear all bubbles to complete the level.";
gameMode.WinConditions.Add("Clear all bubbles");
gameMode.WinConditions.Add("Reach the target score");
gameMode.LoseConditions.Add("Run out of shots");
gameMode.LoseConditions.Add("Run out of time");
gameMode.HasTimeLimit = false;
gameMode.HasShotLimit = false;
gameMode.HasLives = false;
gameMode.ScoreMultiplier = 1f;
gameMode.ComboMultiplier = 1.5f;
gameMode.CompletionBonus = 0;
gameMode.DifficultyLevel = 5;
gameMode.DifficultyIncreases = true;
gameMode.DifficultyIncreaseRate = 0.1f;
gameMode.HasLevelProgression = true;
gameMode.StartingLevel = 1;
gameMode.MaxLevel = 0;
gameMode.AutoAdvanceLevel = true;
gameMode.EnableSpecialTiles = true;
gameMode.EnablePowerups = true;
gameMode.EnableBoosters = true;
gameMode.EnableEnemies = false;
gameMode.EnableObstacles = false;
gameMode.IsMultiplayer = false;
gameMode.HasLeaderboard = false;
gameMode.RewardMultiplier = 1f;
```

### Querying Game Modes

```csharp
// Get game mode by UID
GameModeDefinition gameMode = metaDataRepository.GameModesMeta.GetGameMode(new UID("gamemode_campaign"));

// Get all game modes
IReadOnlyList<GameModeDefinition> allGameModes = metaDataRepository.GameModesMeta.GetAllGameModes();

// Get game modes by type
List<GameModeDefinition> campaignModes = metaDataRepository.GameModesMeta.GetGameModesByType(GameModeType.Campaign);

// Get game modes for a specific player level
List<GameModeDefinition> availableModes = metaDataRepository.GameModesMeta.GetGameModesForPlayerLevel(5);

// Get game modes sorted by priority
List<GameModeDefinition> priorityModes = metaDataRepository.GameModesMeta.GetGameModesByPriority();

// Get featured game modes
List<GameModeDefinition> featuredModes = metaDataRepository.GameModesMeta.GetFeaturedGameModes();

// Get active game modes
List<GameModeDefinition> activeModes = metaDataRepository.GameModesMeta.GetActiveGameModes();
```

### Getting Specific Game Mode Types

```csharp
// Get campaign game modes
List<GameModeDefinition> campaignModes = metaDataRepository.GameModesMeta.GetCampaignGameModes();

// Get endless game modes
List<GameModeDefinition> endlessModes = metaDataRepository.GameModesMeta.GetEndlessGameModes();

// Get time attack game modes
List<GameModeDefinition> timeAttackModes = metaDataRepository.GameModesMeta.GetTimeAttackGameModes();

// Get shot limit game modes
List<GameModeDefinition> shotLimitModes = metaDataRepository.GameModesMeta.GetShotLimitGameModes();

// Get survival game modes
List<GameModeDefinition> survivalModes = metaDataRepository.GameModesMeta.GetSurvivalGameModes();

// Get puzzle game modes
List<GameModeDefinition> puzzleModes = metaDataRepository.GameModesMeta.GetPuzzleGameModes();

// Get daily challenge game modes
List<GameModeDefinition> dailyChallengeModes = metaDataRepository.GameModesMeta.GetDailyChallengeGameModes();

// Get event game modes
List<GameModeDefinition> eventModes = metaDataRepository.GameModesMeta.GetEventGameModes();

// Get practice game modes
List<GameModeDefinition> practiceModes = metaDataRepository.GameModesMeta.GetPracticeGameModes();
```

### Getting Game Modes with Specific Features

```csharp
// Get multiplayer game modes
List<GameModeDefinition> multiplayerModes = metaDataRepository.GameModesMeta.GetMultiplayerGameModes();

// Get cooperative game modes
List<GameModeDefinition> cooperativeModes = metaDataRepository.GameModesMeta.GetCooperativeGameModes();

// Get competitive game modes
List<GameModeDefinition> competitiveModes = metaDataRepository.GameModesMeta.GetCompetitiveGameModes();

// Get game modes with leaderboards
List<GameModeDefinition> leaderboardModes = metaDataRepository.GameModesMeta.GetGameModesWithLeaderboards();

// Get game modes with time limits
List<GameModeDefinition> timeLimitModes = metaDataRepository.GameModesMeta.GetGameModesWithTimeLimit();

// Get game modes with shot limits
List<GameModeDefinition> shotLimitModes = metaDataRepository.GameModesMeta.GetGameModesWithShotLimit();

// Get game modes with lives
List<GameModeDefinition> livesModes = metaDataRepository.GameModesMeta.GetGameModesWithLives();

// Get game modes with level progression
List<GameModeDefinition> progressionModes = metaDataRepository.GameModesMeta.GetGameModesWithLevelProgression();
```

### Using Game Mode Settings in Gameplay

```csharp
public class GameModeManager : MonoBehaviour
{
    private IMetaDataRepository _metaDataRepository;
    private GameModeDefinition _currentGameMode;
    
    public void StartGameMode(UID gameModeUID)
    {
        // Get game mode definition
        _currentGameMode = _metaDataRepository.GameModesMeta.GetGameMode(gameModeUID);
        
        if (_currentGameMode == null)
        {
            Debug.LogError($"Game mode not found: {gameModeUID}");
            return;
        }
        
        // Apply game mode settings
        ApplyGameModeSettings();
        
        // Track analytics
        if (_currentGameMode.StartEvent != null && !_currentGameMode.StartEvent.IsEmpty())
        {
            AnalyticsManager.LogEvent(_currentGameMode.StartEvent);
        }
    }
    
    private void ApplyGameModeSettings()
    {
        // Apply time settings
        if (_currentGameMode.HasTimeLimit)
        {
            TimerManager.SetTimeLimit(_currentGameMode.TimeLimit);
            TimerManager.SetTimeBonusPerObjective(_currentGameMode.TimeBonusPerObjective);
            TimerManager.SetTimePenaltyPerMistake(_currentGameMode.TimePenaltyPerMistake);
        }
        
        // Apply shot settings
        if (_currentGameMode.HasShotLimit)
        {
            ShotManager.SetMaxShots(_currentGameMode.MaxShots);
            ShotManager.SetShotsBonusPerObjective(_currentGameMode.ShotsBonusPerObjective);
        }
        
        // Apply lives settings
        if (_currentGameMode.HasLives)
        {
            LivesManager.SetLives(_currentGameMode.Lives);
            LivesManager.SetLivesLostPerMistake(_currentGameMode.LivesLostPerMistake);
        }
        
        // Apply scoring
        ScoreManager.SetScoreMultiplier(_currentGameMode.ScoreMultiplier);
        ScoreManager.SetComboMultiplier(_currentGameMode.ComboMultiplier);
        ScoreManager.SetCompletionBonus(_currentGameMode.CompletionBonus);
        
        // Apply special features
        SpecialTileManager.SetEnabled(_currentGameMode.EnableSpecialTiles);
        PowerupManager.SetEnabled(_currentGameMode.EnablePowerups);
        BoosterManager.SetEnabled(_currentGameMode.EnableBoosters);
        EnemyManager.SetEnabled(_currentGameMode.EnableEnemies);
        ObstacleManager.SetEnabled(_currentGameMode.EnableObstacles);
        
        // Apply difficulty
        DifficultyManager.SetDifficultyLevel(_currentGameMode.DifficultyLevel);
        DifficultyManager.SetDifficultyIncreases(_currentGameMode.DifficultyIncreases);
        DifficultyManager.SetDifficultyIncreaseRate(_currentGameMode.DifficultyIncreaseRate);
        
        // Apply progression
        if (_currentGameMode.HasLevelProgression)
        {
            LevelManager.SetStartingLevel(_currentGameMode.StartingLevel);
            LevelManager.SetMaxLevel(_currentGameMode.MaxLevel);
            LevelManager.SetAutoAdvance(_currentGameMode.AutoAdvanceLevel);
        }
    }
    
    public void CompleteGameMode()
    {
        // Grant rewards
        List<RewardDefinition> rewards = _currentGameMode.GetAllRewards();
        foreach (var reward in rewards)
        {
            RewardManager.GrantReward(reward, _currentGameMode.RewardMultiplier);
        }
        
        // Track analytics
        if (_currentGameMode.CompleteEvent != null && !_currentGameMode.CompleteEvent.IsEmpty())
        {
            AnalyticsManager.LogEvent(_currentGameMode.CompleteEvent);
        }
    }
    
    public void FailGameMode()
    {
        // Track analytics
        if (_currentGameMode.FailEvent != null && !_currentGameMode.FailEvent.IsEmpty())
        {
            AnalyticsManager.LogEvent(_currentGameMode.FailEvent);
        }
    }
}
```

### Checking Game Mode Conditions

```csharp
GameModeDefinition gameMode = metaDataRepository.GameModesMeta.GetGameMode(new UID("gamemode_campaign"));

// Check if game mode is available for player level
bool isAvailable = gameMode.IsAvailableForPlayerLevel(playerLevel);

// Get adjusted difficulty for level
float adjustedDifficulty = gameMode.GetAdjustedDifficulty(currentLevel);

// Get all rewards from game mode
List<RewardDefinition> rewards = gameMode.GetAllRewards();
```

## Integration with MetaDataRepository

The Game Modes System is integrated into the existing [`IMetaDataRepository`](../IMetaDataRepository.cs) and [`MetaDataRepository`](../MetaDataRepository.cs):

```csharp
// In IMetaDataRepository
public GameModesRegistry GameModesRegistry { get; }
public GameModesMeta GameModesMeta { get; }

// In MetaDataRepository
[SerializeField] private GameModesRegistry _gameModesRegistry;
[SerializeField] private GameModesMeta _gameModesMeta;
public GameModesRegistry GameModesRegistry => _gameModesRegistry;
public GameModesMeta GameModesMeta => _gameModesMeta;

// In GetObjectByUID
if (typeof(T) == typeof(GameModeDefinition))
{
    return _gameModesMeta.Registry.Definitions.FirstOrDefault(m => m.UID == uid) as T;
}
```

## Best Practices

1. **Use Clear Names**: Give game modes clear, descriptive display names
2. **Set Appropriate Priorities**: Lower priority modes are shown first
3. **Define Clear Objectives**: Make objectives clear and achievable
4. **Balance Difficulty**: Carefully balance difficulty across game modes
5. **Provide Variety**: Offer different game modes to cater to different playstyles
6. **Use Leaderboards**: Add leaderboards to competitive modes for engagement
7. **Reward Appropriately**: Adjust rewards based on game mode difficulty
8. **Test Thoroughly**: Test game modes at different player levels
9. **Monitor Metrics**: Track player engagement with different game modes
10. **Iterate and Refine**: Continuously adjust game modes based on player feedback

## Common Use Cases

### Campaign Mode

```csharp
GameModeDefinition campaign = new GameModeDefinition
{
    Type = GameModeType.Campaign,
    Priority = 0,
    MinPlayerLevel = 1,
    AlwaysAvailable = true,
    IsFeatured = true,
    IsActive = true,
    ShortDescription = "Play through levels and progress through the story.",
    FullDescription = "Experience the main campaign with increasing difficulty and unlock new features as you progress.",
    PrimaryObjective = "Complete all levels with 3 stars.",
    HasTimeLimit = false,
    HasShotLimit = false,
    HasLives = false,
    ScoreMultiplier = 1f,
    ComboMultiplier = 1.5f,
    DifficultyLevel = 5,
    DifficultyIncreases = true,
    DifficultyIncreaseRate = 0.1f,
    HasLevelProgression = true,
    StartingLevel = 1,
    MaxLevel = 0,
    AutoAdvanceLevel = true,
    EnableSpecialTiles = true,
    EnablePowerups = true,
    EnableBoosters = true,
    EnableEnemies = false,
    EnableObstacles = false,
    IsMultiplayer = false,
    HasLeaderboard = false,
    RewardMultiplier = 1f
};
```

### Time Attack Mode

```csharp
GameModeDefinition timeAttack = new GameModeDefinition
{
    Type = GameModeType.TimeAttack,
    Priority = 10,
    MinPlayerLevel = 5,
    AlwaysAvailable = true,
    IsFeatured = false,
    IsActive = true,
    ShortDescription = "Complete levels as fast as possible!",
    FullDescription = "Race against the clock to complete levels. Earn time bonuses for objectives and penalties for mistakes.",
    PrimaryObjective = "Complete the level before time runs out.",
    HasTimeLimit = true,
    TimeLimit = 120,
    TimeBonusPerObjective = 10,
    TimePenaltyPerMistake = 5,
    HasShotLimit = false,
    HasLives = false,
    ScoreMultiplier = 1.5f,
    ComboMultiplier = 2f,
    CompletionBonus = 500,
    DifficultyLevel = 6,
    DifficultyIncreases = false,
    HasLevelProgression = false,
    EnableSpecialTiles = true,
    EnablePowerups = true,
    EnableBoosters = true,
    EnableEnemies = false,
    EnableObstacles = false,
    IsMultiplayer = false,
    HasLeaderboard = true,
    LeaderboardType = LeaderboardType.Global,
    LeaderboardResetFrequency = LeaderboardResetFrequency.Weekly,
    RewardMultiplier = 1.5f
};
```

### Endless Mode

```csharp
GameModeDefinition endless = new GameModeDefinition
{
    Type = GameModeType.Endless,
    Priority = 20,
    MinPlayerLevel = 10,
    AlwaysAvailable = true,
    IsFeatured = false,
    IsActive = true,
    ShortDescription = "Play forever with increasing difficulty!",
    FullDescription = "Test your skills in endless mode. Difficulty increases as you progress. How far can you go?",
    PrimaryObjective = "Survive as long as possible.",
    HasTimeLimit = false,
    HasShotLimit = false,
    HasLives = true,
    Lives = 3,
    LivesLostPerMistake = 1,
    ScoreMultiplier = 2f,
    ComboMultiplier = 2.5f,
    CompletionBonus = 0,
    DifficultyLevel = 5,
    DifficultyIncreases = true,
    DifficultyIncreaseRate = 0.2f,
    HasLevelProgression = false,
    EnableSpecialTiles = true,
    EnablePowerups = true,
    EnableBoosters = true,
    EnableEnemies = true,
    EnableObstacles = true,
    IsMultiplayer = false,
    HasLeaderboard = true,
    LeaderboardType = LeaderboardType.Global,
    LeaderboardResetFrequency = LeaderboardResetFrequency.Weekly,
    RewardMultiplier = 2f
};
```

## Summary

The Game Modes System provides a flexible, data-driven framework for creating and managing different game modes. It integrates seamlessly with your existing metadata architecture and supports:

- Multiple game mode types and categories
- Comprehensive rule and objective configuration
- Time, shot, and life limit options
- Difficulty progression and scaling
- Multiplayer support (cooperative and competitive)
- Leaderboard integration
- Reward scaling based on game mode
- Visual customization options
- Analytics tracking for game mode engagement

This system is designed to be reusable across future games in your codebase, following the same patterns as your IAP, Ads, and other metadata systems.