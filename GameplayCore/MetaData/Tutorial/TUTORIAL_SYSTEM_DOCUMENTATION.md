# Tutorial System Documentation

## Overview

The Tutorial System provides a comprehensive framework for creating, managing, and displaying in-game tutorials. It follows the same architectural patterns as other metadata systems in your codebase, using ScriptableObjects for data-driven configuration and UID-based lookups.

## Architecture

### Core Components

1. **TutorialType** - Enum defining tutorial categories
2. **TutorialDefinition** - Main tutorial definition with steps, triggers, and rewards
3. **TutorialsRegistry** - UID-based registry for tutorial definitions
4. **TutorialsMeta** - Container with query methods for tutorials

### Tutorial Types

| Type | Description |
|------|-------------|
| `Onboarding` | First-time player tutorials |
| `GameplayBasics` | Basic gameplay mechanics |
| `Powerup` | Powerup usage and mechanics |
| `Booster` | Booster usage and mechanics |
| `SpecialTiles` | Special tile types and interactions |
| `Advanced` | Advanced strategies and techniques |
| `UI` | UI features and navigation |
| `Store` | Store and IAP features |
| `Event` | Event-specific features |
| `Custom` | Custom tutorial type |

## TutorialDefinition Structure

### Identification
- **UID**: Unique identifier for the tutorial
- **DisplayName**: Display name shown to players
- **InternalName**: Internal name for reference

### Classification
- **Type**: Tutorial category (TutorialType enum)
- **Priority**: Display priority (0-100, lower = higher priority)

### Trigger Conditions
- **MinLevel**: Minimum level required to show tutorial
- **MaxLevel**: Maximum level after which tutorial won't show
- **MinPlayerLevel**: Minimum player level required
- **ShowOnce**: Show only once per player
- **CanSkip**: Allow player to skip tutorial
- **CanReplay**: Allow replay from settings

### Tutorial Content
- **Title**: Title text shown to player
- **Description**: Description text shown to player
- **Steps**: List of tutorial steps in sequence

### Rewards
- **CompletionRewards**: Rewards granted upon completion

### Timing
- **ShowDelay**: Delay before showing tutorial (seconds)
- **CooldownTime**: Minimum time between showing again (seconds)
- **AutoAdvanceTime**: Auto-advance to next step (0 = manual)

### Visual Settings
- **HighlightElements**: UI elements to highlight
- **BlockGameplay**: Block gameplay while tutorial is active
- **ShowOverlay**: Show dim overlay behind tutorial
- **Position**: Tutorial panel position (TutorialPosition enum)

### Prerequisites
- **PrerequisiteTutorials**: Tutorials that must be completed first
- **RequiredAchievements**: Achievements that must be unlocked

### Analytics
- **StartEvent**: Analytics event when tutorial starts
- **CompleteEvent**: Analytics event when tutorial completes
- **SkipEvent**: Analytics event when tutorial is skipped

## TutorialStep Structure

Each tutorial consists of one or more steps:

- **StepNumber**: Step number in sequence
- **Title**: Step title
- **Description**: Step description
- **InteractionType**: Type of interaction required
- **TargetElement**: Target element to interact with
- **HighlightTarget**: Highlight the target element
- **ShowPointer**: Show hand pointer animation
- **AutoAdvanceTime**: Auto-advance after this time (0 = wait for interaction)
- **ShowContinueButton**: Show continue button
- **ContinueButtonText**: Continue button text
- **SkipButtonText**: Skip button text

### Interaction Types

| Type | Description |
|------|-------------|
| `None` | No interaction required |
| `Tap` | Player must tap/click a specific element |
| `Drag` | Player must drag an element |
| `Swipe` | Player must swipe in a direction |
| `Hold` | Player must press and hold |
| `Action` | Player must complete a specific action |
| `Navigate` | Player must navigate to a specific screen |
| `Purchase` | Player must purchase something |
| `WatchAd` | Player must watch an ad |
| `Custom` | Custom interaction type |

### Tutorial Positions

| Position | Description |
|----------|-------------|
| `Center` | Center of the screen |
| `Top` | Top of the screen |
| `Bottom` | Bottom of the screen |
| `Left` | Left side of the screen |
| `Right` | Right side of the screen |
| `TopLeft` | Top-left corner |
| `TopRight` | Top-right corner |
| `BottomLeft` | Bottom-left corner |
| `BottomRight` | Bottom-right corner |

## Usage Examples

### Creating a Tutorial Definition

```csharp
// Create a new TutorialDefinition asset
TutorialDefinition tutorial = ScriptableObject.CreateInstance<TutorialDefinition>();
tutorial.UID = new UID("tutorial_bomb_powerup");
tutorial.DisplayName = "Bomb Powerup Tutorial";
tutorial.InternalName = "bomb_powerup_tutorial";
tutorial.Type = TutorialType.Powerup;
tutorial.Priority = 10;
tutorial.MinLevel = 5;
tutorial.MaxLevel = 10;
tutorial.MinPlayerLevel = 1;
tutorial.ShowOnce = true;
tutorial.CanSkip = true;
tutorial.CanReplay = false;
tutorial.Title = "Bomb Powerup";
tutorial.Description = "Learn how to use the Bomb Powerup to clear bubbles!";
tutorial.BlockGameplay = true;
tutorial.ShowOverlay = true;
tutorial.Position = TutorialPosition.Center;

// Add tutorial steps
TutorialStep step1 = new TutorialStep
{
    StepNumber = 1,
    Title = "Bomb Powerup",
    Description = "Tap the Bomb Powerup to activate it.",
    InteractionType = TutorialInteractionType.Tap,
    TargetElement = new UID("bomb_powerup_button"),
    HighlightTarget = true,
    ShowPointer = true,
    ShowContinueButton = true,
    ContinueButtonText = "Got it!"
};

tutorial.Steps.Add(step1);

// Add completion rewards
tutorial.CompletionRewards.Add(rewardDefinition);
```

### Querying Tutorials

```csharp
// Get tutorial by UID
TutorialDefinition tutorial = metaDataRepository.TutorialsMeta.GetTutorial(new UID("tutorial_bomb_powerup"));

// Get all tutorials
IReadOnlyList<TutorialDefinition> allTutorials = metaDataRepository.TutorialsMeta.GetAllTutorials();

// Get tutorials by type
List<TutorialDefinition> powerupTutorials = metaDataRepository.TutorialsMeta.GetTutorialsByType(TutorialType.Powerup);

// Get tutorials for a specific level
List<TutorialDefinition> levelTutorials = metaDataRepository.TutorialsMeta.GetTutorialsForLevel(5);

// Get tutorials for a specific player level
List<TutorialDefinition> playerLevelTutorials = metaDataRepository.TutorialsMeta.GetTutorialsForPlayerLevel(3);

// Get tutorials sorted by priority
List<TutorialDefinition> priorityTutorials = metaDataRepository.TutorialsMeta.GetTutorialsByPriority();

// Get skippable tutorials
List<TutorialDefinition> skippableTutorials = metaDataRepository.TutorialsMeta.GetSkippableTutorials();

// Get replayable tutorials
List<TutorialDefinition> replayableTutorials = metaDataRepository.TutorialsMeta.GetReplayableTutorials();

// Get one-time tutorials
List<TutorialDefinition> oneTimeTutorials = metaDataRepository.TutorialsMeta.GetOneTimeTutorials();
```

### Getting Next Tutorial for a Level

```csharp
// Get completed tutorials from player data
HashSet<UID> completedTutorials = playerData.CompletedTutorials;

// Get next tutorial for level 5
TutorialDefinition nextTutorial = metaDataRepository.TutorialsMeta.GetNextTutorialForLevel(5, completedTutorials);

if (nextTutorial != null)
{
    // Show the tutorial
    ShowTutorial(nextTutorial);
}
```

### Getting All Available Tutorials for a Level

```csharp
// Get all available tutorials for level 5
List<TutorialDefinition> availableTutorials = metaDataRepository.TutorialsMeta.GetAvailableTutorialsForLevel(5, completedTutorials);

foreach (var tutorial in availableTutorials)
{
    Debug.Log($"Available tutorial: {tutorial.DisplayName}");
}
```

### Getting Specific Tutorial Types

```csharp
// Get onboarding tutorials
List<TutorialDefinition> onboardingTutorials = metaDataRepository.TutorialsMeta.GetOnboardingTutorials();

// Get gameplay basics tutorials
List<TutorialDefinition> basicsTutorials = metaDataRepository.TutorialsMeta.GetGameplayBasicsTutorials();

// Get powerup tutorials
List<TutorialDefinition> powerupTutorials = metaDataRepository.TutorialsMeta.GetPowerupTutorials();

// Get booster tutorials
List<TutorialDefinition> boosterTutorials = metaDataRepository.TutorialsMeta.GetBoosterTutorials();

// Get special tiles tutorials
List<TutorialDefinition> specialTilesTutorials = metaDataRepository.TutorialsMeta.GetSpecialTilesTutorials();

// Get advanced tutorials
List<TutorialDefinition> advancedTutorials = metaDataRepository.TutorialsMeta.GetAdvancedTutorials();

// Get UI tutorials
List<TutorialDefinition> uiTutorials = metaDataRepository.TutorialsMeta.GetUITutorials();

// Get store tutorials
List<TutorialDefinition> storeTutorials = metaDataRepository.TutorialsMeta.GetStoreTutorials();

// Get event tutorials
List<TutorialDefinition> eventTutorials = metaDataRepository.TutorialsMeta.GetEventTutorials();
```

### Checking Tutorial Conditions

```csharp
TutorialDefinition tutorial = metaDataRepository.TutorialsMeta.GetTutorial(new UID("tutorial_bomb_powerup"));

// Check if tutorial should be shown for level
bool shouldShow = tutorial.ShouldShowForLevel(currentLevel);

// Check if tutorial should be shown for player level
bool shouldShowForPlayer = tutorial.ShouldShowForPlayerLevel(playerLevel);

// Get all rewards from tutorial
List<RewardDefinition> rewards = tutorial.GetAllRewards();
```

### Tutorial Flow Example

```csharp
public class TutorialManager : MonoBehaviour
{
    private IMetaDataRepository _metaDataRepository;
    private PlayerData _playerData;
    
    public void CheckForTutorials(int level)
    {
        HashSet<UID> completedTutorials = _playerData.CompletedTutorials;
        
        // Get next tutorial for this level
        TutorialDefinition tutorial = _metaDataRepository.TutorialsMeta.GetNextTutorialForLevel(level, completedTutorials);
        
        if (tutorial != null)
        {
            // Show tutorial after delay
            StartCoroutine(ShowTutorialWithDelay(tutorial));
        }
    }
    
    private IEnumerator ShowTutorialWithDelay(TutorialDefinition tutorial)
    {
        yield return new WaitForSeconds(tutorial.ShowDelay);
        
        // Show tutorial UI
        ShowTutorialUI(tutorial);
        
        // Track analytics
        if (tutorial.StartEvent != null && !tutorial.StartEvent.IsEmpty())
        {
            AnalyticsManager.LogEvent(tutorial.StartEvent);
        }
    }
    
    public void CompleteTutorial(TutorialDefinition tutorial)
    {
        // Mark tutorial as completed
        _playerData.CompletedTutorials.Add(tutorial.UID);
        
        // Grant rewards
        List<RewardDefinition> rewards = tutorial.GetAllRewards();
        foreach (var reward in rewards)
        {
            RewardManager.GrantReward(reward);
        }
        
        // Track analytics
        if (tutorial.CompleteEvent != null && !tutorial.CompleteEvent.IsEmpty())
        {
            AnalyticsManager.LogEvent(tutorial.CompleteEvent);
        }
    }
    
    public void SkipTutorial(TutorialDefinition tutorial)
    {
        // Track analytics
        if (tutorial.SkipEvent != null && !tutorial.SkipEvent.IsEmpty())
        {
            AnalyticsManager.LogEvent(tutorial.SkipEvent);
        }
    }
}
```

## Integration with MetaDataRepository

The Tutorial System is integrated into the existing [`IMetaDataRepository`](../IMetaDataRepository.cs) and [`MetaDataRepository`](../MetaDataRepository.cs):

```csharp
// In IMetaDataRepository
public TutorialsMeta TutorialsMeta { get; }

// In MetaDataRepository
[SerializeField] private TutorialsMeta _tutorialsMeta;
public TutorialsMeta TutorialsMeta => _tutorialsMeta;

// In GetObjectByUID
if (typeof(T) == typeof(TutorialDefinition))
{
    return _tutorialsMeta.Registry.GetObject(uid) as T;
}
```

## Best Practices

1. **Use Descriptive Names**: Give tutorials clear, descriptive display names
2. **Set Appropriate Priorities**: Lower priority tutorials are shown first
3. **Define Clear Triggers**: Use MinLevel and MaxLevel to control when tutorials appear
4. **Keep Steps Concise**: Each step should teach one concept
5. **Use Visual Highlights**: Highlight relevant UI elements to guide players
6. **Provide Rewards**: Reward players for completing tutorials to encourage engagement
7. **Track Analytics**: Use analytics events to measure tutorial effectiveness
8. **Allow Skipping**: Let experienced players skip tutorials they don't need
9. **Test Thoroughly**: Test tutorials at different player levels and game states
10. **Consider Replayability**: Mark important tutorials as replayable for reference

## Common Use Cases

### Onboarding Tutorial

```csharp
TutorialDefinition onboarding = new TutorialDefinition
{
    Type = TutorialType.Onboarding,
    Priority = 0,
    MinLevel = 1,
    MaxLevel = 1,
    ShowOnce = true,
    CanSkip = false,
    Title = "Welcome to Bubble Shooter!",
    Description = "Let's learn how to play!",
    BlockGameplay = true,
    ShowOverlay = true,
    Position = TutorialPosition.Center
};
```

### Powerup Tutorial

```csharp
TutorialDefinition powerupTutorial = new TutorialDefinition
{
    Type = TutorialType.Powerup,
    Priority = 10,
    MinLevel = 5,
    MaxLevel = 10,
    ShowOnce = true,
    CanSkip = true,
    Title = "Bomb Powerup",
    Description = "Use the Bomb to clear surrounding bubbles!",
    BlockGameplay = true,
    ShowOverlay = true,
    Position = TutorialPosition.Bottom
};
```

### Store Tutorial

```csharp
TutorialDefinition storeTutorial = new TutorialDefinition
{
    Type = TutorialType.Store,
    Priority = 20,
    MinLevel = 3,
    MaxLevel = 5,
    ShowOnce = true,
    CanSkip = true,
    CanReplay = true,
    Title = "Visit the Store",
    Description = "Buy powerups and boosters to help you progress!",
    BlockGameplay = false,
    ShowOverlay = false,
    Position = TutorialPosition.Top
};
```

## Summary

The Tutorial System provides a flexible, data-driven framework for creating and managing in-game tutorials. It integrates seamlessly with your existing metadata architecture and supports:

- Multiple tutorial types and categories
- Multi-step tutorial sequences
- Conditional triggering based on level and player progress
- Prerequisite system for tutorial dependencies
- Reward integration for tutorial completion
- Analytics tracking for tutorial effectiveness
- Visual highlighting and positioning options
- Skip and replay functionality

This system is designed to be reusable across future games in your codebase, following the same patterns as your IAP, Ads, and other metadata systems.