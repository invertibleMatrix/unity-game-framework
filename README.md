# UGFW - Unity Game Framework

An opinionated, all-in-one game development framework for Unity. UGFW ships as a single `Assets/UGFW` folder and is designed to be adopted as a whole -- every module works together.

## Installation

Add as a git submodule into your Unity project:

```bash
git submodule add https://github.com/invertibleMatrix/unity-game-framework.git Assets/UGFW
```

## Required Dependencies

Install these via Unity Package Manager or OpenUPM:

- **UniTask** - `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10`
- **Reflex** - `https://github.com/gustavopsantos/reflex.git?path=/Assets/Reflex/#14.3.0`
- **DOTween** - Install from Asset Store or OpenUPM
- **Addressables** - `com.unity.addressables` 2.11+
- **Cinemachine** - `com.unity.cinemachine` 3.1+

## Optional Dependencies

These enable additional service implementations. Toggle them via **Tools > UGFW > Define Symbols**:

- **Unity Purchasing** - Enables IAP service (`IAP`)
- **Google Mobile Ads** - Enables AdMob provider (`ADMOB_ENABLED`)
- **Firebase Core** - Enables Firebase initialization (`FIREBASE_INITIALIZATION`)
- **Firebase Analytics** - Enables Firebase analytics provider (`FIREBASE_ANALYTICS`)
- **Firebase Remote Config** - Enables remote config service (`FIREBASE_REMOTE_CONFIG`)
- **GameAnalytics** - Enables GameAnalytics provider (`GAME_ANALYTICS`)
- **Unity Notifications** - Enables notification service

---

## Architecture Overview

```
Assets/UGFW/Runtime/
    Core/           - Foundation: state machines, persistence, UID, events, camera, resource loading
    GameplayCore/   - Game data: MetaData system, GameModel, currencies, rewards, IAP definitions
    Services/       - SDK integrations: ads, analytics, IAP, remote config, notifications
    UISystem/       - Full UI framework: screens, fragments, animations, pooling
Assets/UGFW/Editor/ - Editor tools: define symbols window, UI visualizer, UID editor, scene loader
```

Each runtime module has its own assembly definition (`AK.Core`, `AK.GameplayCore`, `AK.Services`, `AK.UISystem`).

---

## Module: Core

The foundation layer. Everything else builds on top of these systems.

### StateMachine

A generic, pure-C# finite state machine with no Unity dependency.

```csharp
// Define states
public class IdleState : BaseState<Player>
{
    public override void OnEnter() { /* ... */ }
    public override void Tick()    { /* ... */ }
    public override void OnExit()  { /* ... */ }
}

// Create and use
var sm = new StateMachine<Player, BaseState<Player>>(player);
sm.ChangeState(new IdleState());
sm.Tick(); // called every frame
```

- `BaseState<TMediator>` -- abstract base with `OnEnter()`, `Tick()`, `OnExit()`, `Dispose()`
- `StateMachine<TMediator, TBaseState>` -- manages transitions, auto-enters initial state
- States can re-enter themselves (no same-instance guard) -- useful for restart flows
- Used by `StateEntity` and `BaseCamera` under the hood

### AppStateMachine

Application-level state machine using ScriptableObject states. Supports a pause/resume stack for navigation between coarse-grained app states (Boot, MainMenu, Gameplay, LevelEditor).

```csharp
// Define a state
[CreateAssetMenu(menuName = "AppStateMachine/BootState")]
public class BootState : AppState
{
    [Inject] private readonly GameModel _gameModel;
    [Inject] private readonly IMetaDataRepository _metaDataRepository;

    public override void OnEnter()
    {
        _gameModel.Initialize(_metaDataRepository, out bool isFirstLaunch);
        // ... boot sequence
    }
}

// Transition between states
AppStateMachine.ChangeState(_mainMenuState);                // replace current
AppStateMachine.ChangeState(_gameplayState, pauseCurrent: true); // push (pause current)
AppStateMachine.TryGoBack();                                // pop (resume previous)
```

**Key types:**
- `AppState` -- abstract ScriptableObject with lifecycle: `OnEnter()`, `OnExit()`, `OnPause()`, `OnResume()`, `Tick()`
- `AppState<TTransitionContext>` -- typed context subclass
- `TransitionContext` -- base class for passing data between states (extend with your own fields)
- `IAppStateMachine` -- interface with `ChangeState()`, `TryGoBack()`, `PreviousState`
- `AppStateMachine` -- MonoBehaviour implementation, boots from a configured `_bootState` in `Start()`, ticks the current state every frame

**DI:** States receive Reflex `[Inject]` dependencies automatically. The `Container` is also available as `_container`.

### Prefs (UniPrefs & PrefsProperty)

Two-layer persistence built on top of PlayerPrefs.

**UniPrefs** -- static wrapper over PlayerPrefs that adds JSON serialization for any `[Serializable]` type:

```csharp
UniPrefs.Set("player_name", "Alice");
UniPrefs.Set("high_score", myScoreObject);   // any serializable type
string name = UniPrefs.Get<string>("player_name");
var score = UniPrefs.Get<ScoreData>("high_score");
UniPrefs.Delete("player_name");
UniPrefs.DeleteAll();  // fires OnReset event
```

**PrefsProperty<T>** -- instance-based wrapper with lazy caching:

```csharp
// Declare
private readonly PrefsProperty<int> _highScore = new("high_score", 0);
private readonly PrefsProperty<GameModel> _save = new("UGFW_GAME_MODEL");

// Read (lazy-loads from prefs on first access, then cached)
int score = _highScore.Read();

// Save (writes to prefs immediately)
_highScore.Save(100);

// Reset (deletes from prefs, reverts to default)
_highScore.Reset();

// Implicit conversion
int val = _highScore; // same as _highScore.Read()
```

### UID System

Unique identifiers as ScriptableObject assets. The backbone of the MetaData system.

```csharp
// UID is a ScriptableObject with an auto-generated GUID
// Equality is value-based (by GUID string), not reference-based
uidA == uidB;           // true if same GUID
uidA == "some-guid";    // compare with string
string id = uidA;       // implicit conversion to string
```

**Registry lookup:**
- `UIDRegistry` -- global registry of all UID assets, lookup by GUID or asset name
- `TypedUIDRegistry<T>` -- maps UID to typed objects (where `T : UID`), bidirectional lookup
- `TypedUIDRegistryAsset<T>` -- ScriptableObject wrapper with editor validation buttons

**The MetaData identity chain:**
```
UID (ScriptableObject with GUID)
  -> MetaDataAsset (UID + Name, DisplayName, Description, Icon)
    -> CurrencyDefinition, RewardDefinition, etc. (game-specific definitions)
```

Every definition extends `MetaDataAsset` which extends `UID`. This means every definition has a stable GUID identity AND display metadata.

### EventBus

High-performance, allocation-conscious event bus with priority-based dispatch and event consumption.

```csharp
// Define events
public struct DamageEvent : IEvent
{
    public int Amount;
    public bool IsCritical;
}

// Subscribe
bus.SubscribeTo<DamageEvent>(OnDamage, priority: 10); // higher = earlier

// Raise
var evt = new DamageEvent { Amount = 50, IsCritical = true };
bus.Raise(in evt);

// Consume (stop propagation to lower-priority listeners)
bus.ConsumeCurrentEvent();
```

- `GenericEventBus<TBaseEvent>` -- simple event bus with priority and consumption
- `TargetedGenericEventBus<TBaseEvent, TObject>` -- adds target/source filtering for object-scoped events
- Recursive raise is safe (queued and dispatched after current event finishes)
- Uses internal object pooling for enumerators and queued events

### ResourceManagement

Async resource loading facade over Unity Addressables.

```csharp
// Load single asset
var texture = await UniResources.LoadAssetAsync<Texture2D>("my_texture");

// Load multiple assets as a group
var group = await UniResources.LoadAssetsAsync<Sprite>(new[] { "icon1", "icon2" });

// Spawn prefab
var instance = await UniResources.SpawnAsync("enemy_prefab", transform);

// Sync (blocking) variants also available
var tex = UniResources.LoadAsset<Texture2D>("my_texture");

// Cleanup
UniResources.DisposeAsset(texture);
UniResources.DisposeInstance(instance.gameObject);
```

- `UniResources` -- static facade, delegates to `IResourceLoadingStrategy`
- `AddressablesLoadingStrategy` -- default implementation using Unity's Addressables
- `AssetsGroup<T>` -- tracks a batch of loaded assets for group release
- Sprite loading components: `ImageSpriteLoader`, `SpriteRendererLoader` -- drop on UI Image or SpriteRenderer to auto-load from Addressables

### CameraSystem

Multi-camera management with URP camera stacking.

```csharp
// Get a camera
var mainCam = cameraSystem.Get<MainCamera>();

// Enable/disable
cameraSystem.EnableCamera<MainMenuCamera>();
cameraSystem.DisableCamera<GameplayCamera>();

// Camera shake
mainCam.Shake(intensity: 0.5f, duration: 0.3f);
```

- `ICameraSystem` -- get/enable/disable cameras, reorder stacks
- `CameraRole` -- `Base` (renders to screen) or `Overlay` (stacks on top)
- `BaseCamera` -- abstract `StateEntity` implementing `IGameCamera`, auto-registers with `ICameraSystem`
- `CinemachineBaseCamera` -- adds Cinemachine integration with impulse-based shake

### Core Utilities

| Utility | Description |
|---------|-------------|
| **Timer** | UniTask-based countdown/stopwatch/interval with pause/resume and UI binding |
| **TimeFormatter** | Format durations, arrival times, relative times ("5m ago"), smart dynamic formatting |
| **NumberFormatter** | Abbreviate large numbers (1500 -> "1.5K"), parse back |
| **AudioSpawner** | Pooled audio spawner with registry-based configs, fade in/out, pitch randomization |
| **ParticleSpawner** | Pooled particle spawner with registry-based configs, sync/async instantiation |
| **JobDispatcher** | Multi-threaded job dispatch with double-buffered lock-free main/worker thread communication |
| **DataStructures** | `FreeList<T>` (generational handles), `PriorityQueue<T,P>` (quaternary min-heap port) |
| **Extensions** | `Shuffle<T>` (Fisher-Yates), `SafeInvoke`, `TweenExt` (DOTween value tweening), enum caching |
| **PropertyProxy\<T\>** | Reactive property wrapper with `UnityEvent<T> OnChange` |
| **Haptics** | `IHapticsPlayer` interface (8 haptic methods), `HapticPlayerComponent` with DI resolution |
| **Profiling** | `ScopedTimeProfiler` (IDisposable), `StopwatchTimeProfiler` |
| **Visual** | `PingPongRotator`, `LineRendererScroller`, `DiscoEffect` |

---

## Module: GameplayCore

Game data layer built around the **MetaData system** -- a ScriptableObject-driven architecture for defining all game content as data assets.

### The MetaData Pattern

Every game domain follows a consistent four-part pattern:

```
[Domain]Meta          -- ScriptableObject container (e.g., CurrencyMeta, RewardsMeta)
  -> [Domain]Registry    -- TypedUIDRegistry<Definition> for UID-based lookup
  -> [Domain]Definition  -- The actual data asset (extends MetaDataAsset extends UID)
  -> [Domain]Type        -- Enum categorizing the domain (e.g., CurrencyType, RewardType)
```

**Example: The Currency domain**

```
CurrencyMeta (ScriptableObject)
  -> CurrencyRegistry (TypedUIDRegistry<CurrencyDefinition>)
  -> CurrencyDefinition : MetaDataAsset   // fields: Type, MaxAmount, StartingAmount, etc.
  -> CurrencyType enum                    // SoftCurrency, HardCurrency, Energy, etc.
```

### The MetaDataRepository

`MetaDataRepository` is a single ScriptableObject that holds references to ALL domain metas:

```csharp
public class MetaDataRepository : MonoBehaviour, IMetaDataRepository
{
    public UIDRegistry        UIDRegistry;
    public CurrencyMeta       CurrencyMeta;
    public RewardsMeta        RewardsMeta;
    public AdsMeta            AdsMeta;
    public AnalyticsMeta      AnalyticsMeta;
    public IAPMeta            IAPMeta;
    public ShopMeta           ShopMeta;
    public NotificationsMeta  NotificationsMeta;
    public ProgressionMeta    ProgressionMeta;
    public AchievementsMeta   AchievementsMeta;
    public DailyChallengesMeta DailyChallengesMeta;
    public DailyRewardsMeta   DailyRewardsMeta;
    public DifficultyMeta     DifficultyMeta;
    public GameModesMeta      GameModesMeta;
    public SeasonsMeta        SeasonsMeta;
    public SpinWheelMeta      SpinWheelMeta;
    public TutorialsMeta      TutorialsMeta;
    public RemoteConfigMeta   RemoteConfigMeta;
    // ...
}
```

Place ONE `MetaDataRepository` in your bootstrap scene. It's registered in DI and injected everywhere.

### Game Domains

#### Currency

- `CurrencyDefinition` -- type, max amount, starting amount, exchange rates
- `CurrencyType` -- SoftCurrency, HardCurrency, Energy, Premium, Token, etc.
- `CurrencyModel` -- runtime model with `Add()`, `Deduct()`, `DeductPartial()`, respects MaxAmount cap
- `CurrencyExchangeRate` -- conversion rates between currencies

#### Rewards

- `RewardDefinition` -- amount, type, linked currency/bundle/gacha data
- `RewardType` -- Star, Currency, Bundle, Gacha, Subscription, Unlockable, Powerup, Booster, Live, NoAds
- `RewardBundle` -- ordered/weighted list of sub-rewards (recursive), bundle types: Sequential, Random, Weighted, RandomWeighted, All
- `GachaBundle` -- weighted random reward pool with `EvaluateRewards()` for gacha pulls
- `CheckpointReward` -- rewards tied to progression milestones
- `SubscriptionReward` -- time-based rewards for subscribers

#### Ads

- `AdPlacementDefinition` -- defines an ad placement with: placement ID, ad unit ID, ad type (Rewarded/Interstitial/Banner/AppOpen/RewardedInterstitial), frequency caps (MaxPerSession, MaxPerDay, CooldownSeconds), level gating, loading strategies (PreloadOnInitialize, AutoReloadAfterShow, AutoReloadOnFail, MaxRetryAttempts, GetRetryDelay())
- `AdType` enum
- `AdLoadingStrategy` enum
- `AdsMeta` / `AdsRegistry` -- container and registry

#### IAP / Store

- `IAPProductDefinition` -- store product ID, product type (Consumable/NonConsumable/Subscription)
- `IAPProductType` enum
- `ShopCategoryDefinition` -- categories of shop items with cost type, product UIDs
- `ShopItemDefinition` -- individual shop items with rarity, cost, rewards
- `PurchasableItemDefinition` -- bridge between shop items and the purchase system: cost type, currency type, price, product ID, reward/bundle references
- `CostType` -- None, Free, Currency, Gem, Ad, Resource, InAppPurchase

#### Other Domains

| Domain | Definitions | Description |
|--------|------------|-------------|
| **Analytics** | AnalyticsEventDefinition, ParameterName | Typed event and parameter definitions for analytics tracking |
| **Progression** | ProgressionLevel, MilestoneDefinition | Level progression and milestone tracking |
| **Achievements** | AchievementDefinition, AchievementType | Achievement definitions |
| **DailyChallenges** | DailyChallengeDefinition, ChallengeType | Daily challenge definitions |
| **DailyRewards** | DailyRewardSlot, StreakBonusDefinition | Daily reward calendar with streak bonuses |
| **Difficulty** | DifficultyDefinition, DifficultyType | Difficulty settings |
| **GameModes** | GameModeDefinition, GameModeType | Game mode definitions |
| **Seasons** | EventDefinition, EventType | Seasonal/live event definitions |
| **SpinWheel** | SpinWheelSlot | Spin wheel prize definitions |
| **Tutorials** | TutorialDefinition, TutorialType | Tutorial step definitions |
| **Notifications** | NotificationDefinition, NotificationType | Local notification templates |
| **RemoteConfig** | RemoteVariable, RemoteBool/Int/Float/String | Remote variables with Remote > Cached > Default priority |

### Remote Variables

`RemoteVariable<T>` provides a three-tier value resolution: **Remote > Cached > Default**.

```csharp
// Define in a RemoteConfigMeta ScriptableObject
public RemoteInt DailyCoinReward = new() { DefaultValue = 100 };

// At runtime, after remote config fetch:
int reward = DailyCoinReward.Value; // remote value if available, else cached, else 100

// Each RemoteVariable automatically:
// 1. Uses the remote value if fetched and non-default
// 2. Falls back to the last cached value (persisted locally)
// 3. Falls back to the DefaultValue set in the inspector
```

### GameModel

The central runtime game state model. Persisted via `PrefsProperty<GameModel>`.

```csharp
// Load saved game (or get fresh instance)
var gameModel = GameModel.Load();

// Initialize (resolves UIDs, credits pending transactions, detects first launch)
gameModel.Initialize(metaDataRepository, out bool isFirstLaunch);

// Persist any time
gameModel.Commit();   // writes to prefs via PrefsProperty

// Access currencies
var coins = gameModel.GetCurrencyModel(CurrencyType.SoftCurrency);
coins.Add(100);         // respects MaxAmount cap
gameModel.Commit();     // persist

// Queue and credit rewards
gameModel.AppendLevelCompleteRewards(rewardUIDs);
gameModel.CreditPendingTransactions(TransactionType.LevelCompleteTransaction);

// Check for save
bool hasSave = GameModel.HasSave();
GameModel.DeleteSave(); // wipe all data
```

**Key models:**
- `GameModel` -- player level, session, currencies, pending transactions, settings, dirty tracking
- `CurrencyModel` -- runtime currency with `Add()`, `Deduct()`, `DeductPartial()`, UID resolution for deserialization
- `GameSettingsModel` -- audio/vibration preferences
- `GameStateModel` -- gameplay state
- `Transaction` -- UID + timestamp for pending rewards/purchases, with deserialization resolution

---

## Module: Services

Provider-based service layer for SDK integrations. Each service has a public interface and swap-able provider implementations guarded by preprocessor symbols.

### Ads Service

Priority-based waterfall mediation with frequency capping and auto-reload.

```csharp
// Initialize with metadata
await adsService.InitializeAsync(metaDataRepository.AdsMeta, playerLevel: 5);

// Show a rewarded ad
bool rewardGranted = await adsService.ShowRewardedAdAsync(placementDefinition);

// Check readiness and frequency caps
bool canShow = adsService.CanShowPlacement(placement);
int sessionCount = adsService.GetSessionShowCount("placement_id");

// Consent
adsService.SetUserConsent(canTrack: true);
adsService.SetUserUnderAge(isUnderAge: false);
```

**AdPlacementDefinition** controls everything about an ad slot:
- Frequency caps: `MaxPerSession`, `MaxPerDay`, `CooldownSeconds`
- Level gating: `IsAvailable(playerLevel)` with min/max level
- Loading: `PreloadOnInitialize`, `AutoReloadAfterShow`, `MaxRetryAttempts`

**Providers:** AdMob (`ADMOB_ENABLED`), NullProvider (testing)
**Builder:** `AdServiceBuilder` for fluent construction, or `IMetaDataRepository.CreateAdService()`

### Analytics Service

Multi-provider analytics facade with metadata-driven event definitions.

```csharp
// Track raw events
analyticsService.TrackEvent("level_complete", new Dictionary<string, object> { { "level", 5 } });

// Track metadata-driven events (validates parameters, maps provider names)
analyticsService.TrackEvent(eventUID, parameters);

// Track monetization
analyticsService.TrackPurchase("com.game.coinpack", 0.99, "USD");
analyticsService.TrackAdImpression("rewarded_level_end", "admob");
```

**Providers:** Firebase Analytics (`FIREBASE_ANALYTICS`), GameAnalytics (`GAME_ANALYTICS`), DebugAnalyticsProvider (console logging)

### IAP & Purchasing Service

Two-layer system: `IIAPService` (raw store operations) and `IPurchaseService` (bridges IAP with MetaData and GameModel).

```csharp
// High-level purchase (handles currency deduction and reward delivery)
var status = await purchaseService.Purchase(purchasableItemDefinition, immediateCredit: true);

// Check IAP ownership
bool owned = purchaseService.IAPService.IsProductOwned("no_ads");
bool subscribed = purchaseService.IAPService.IsSubscribed("vip_monthly");
```

**Purchase flow:**
1. `CostType.InAppPurchase` -> delegates to `IIAPService.PurchaseAsync()`, then credits rewards
2. `CostType.Currency` -> checks/deducts from `CurrencyModel`, queues rewards
3. Currency-type rewards from IAP are always credited immediately

### Firebase Services

Must be initialized first. Other Firebase services depend on `IFirebaseInitializationService`.

```csharp
// Initialize Firebase
bool available = await firebaseInit.InitializeAsync();

// Remote Config
await remoteConfigService.InitializeAsync(metaDataRepository.RemoteConfigMeta);
await remoteConfigService.FetchAndActivateAsync();
// RemoteVariables on the meta now reflect server values
```

### Notification Service

Local push notifications with UID-based scheduling from MetaData definitions.

```csharp
// Request permission
notificationService.RequestPermission(status => { /* ... */ });

// Schedule from MetaData definition
notificationService.ScheduleNotification(welcomeUID, delaySeconds: 86400);

// Schedule custom
notificationService.ScheduleNotification("Title", "Message", fireTime, "id", data, repeatInterval);
```

---

## Module: UISystem

A unified UI framework where one class (`UIView`) serves as both screens and fragments. The distinction is determined by a `UIViewChannel` component on the prefab -- no separate class hierarchies.

### Core Concepts

**Screen** (has `UIViewChannel`) -- gets its own Canvas, pushed onto a channel-based stack, sorted by `UIChannel` (HUD=0, Menu=100, Overlay=200).

**Fragment** (no `UIViewChannel`) -- lives inside a parent view's `FragmentContainer`, tracked in per-parent history stacks.

### Show / Close / Navigate

```csharp
// Show a screen (fire-and-forget or async)
uiSystem.Show<UIMainMenuScreen>();
var screen = await uiSystem.ShowAsync<UIMainMenuScreen>(context: myData);

// Show a fragment inside a specific parent
uiSystem.Show<UISettingsFragment>(parent: screen);

// Close
uiSystem.Close(view);
await uiSystem.CloseAsync(view);

// Navigate back (fragment history)
uiSystem.GoBack(parentView);

// Convenience
uiSystem.DisplayToast("Saved!");
uiSystem.DisplayBanner("Special Offer!", variantId: "sale");
```

### UIView Lifecycle

```csharp
public class UIMyScreen : UIView<MyContext>
{
    public override void SetContext(MyContext ctx) { /* receive data */ }
    public override void RegisterResources() { /* subscribe to events */ }
    public override void UnRegisterResources() { /* unsubscribe */ }
    public override void OnPrepareShow() { /* before animation */ }
    public override void OnShow() { /* after animation */ }
    public override void OnPrepareHide() { /* before hide animation */ }
    public override void OnHide() { /* after hide animation */ }
    public override void OnPause() { /* covered by another view */ }
    public override void OnResume() { /* uncovered */ }
    public override void OnReset() { /* returned to pool */ }
}
```

### Stack Behavior

Control what happens to the view below when a new view is pushed:

| Behavior | Effect |
|----------|--------|
| `DoNothing` | Below view unaffected (toasts, overlays) |
| `HideBelow` | Below view hidden (full-screen takeover) |
| `PauseOnlyBelow` | Below view input-blocked but visible (popups, dialogs) |
| `PauseAndHideBelow` | Below view fully paused + hidden (replacement screens) |
| `CloseBelow` | Below view closed/destroyed (no-return navigation) |

### Animations

30+ animation strategy ScriptableObjects for show/hide transitions:

- **Core:** Fade, Slide, Scale, Composite
- **Bouncy/Elastic:** Bounce, Boing, Elastic, PopSnap
- **Card-themed:** CardArc, CardDeal, CardFan, CardFlipDeal, CardFlyIn, CardPop, CardSpread, CardStack
- **Rotation:** Flip, RotateIn, SlideRotate, ZoomRotate, Spiral
- **Special:** Cascade, ConfettiBurst, DropBounce, OrganicGrowth, PartyPopper, Pulse, Reward, Shake, WobblyLife

Assign via `UIAnimationConfig` ScriptableObject on each `UIView`.

### Common UI Components

Ready-made views: `UIViewToast`, `UIViewBanner`, `UIFragButton`, `UIFragTooltip`, `UIFragLoadSpinner`, `UITutorialArrow`

### Static vs Dynamic Views

- **Static** -- pre-placed as child GameObjects. Survive `Close()` with `Normal` context (just hidden).
- **Dynamic** -- instantiated at runtime. Always destroyed (or pooled) on close.

---

## Module: Editor

| Tool | Menu Path | Description |
|------|-----------|-------------|
| **Define Symbols Window** | Tools > UGFW > Define Symbols | Toggle preprocessor symbols for optional SDKs |
| **View Stack Visualizer** | AK > UI > V2 - View Stack Visualizer | Inspect live UI channel/fragment stacks, validate consistency |
| **UID Editor** | Context menu on null UID fields | Create UID assets in-place from inspector |
| **Missing Scripts Finder** | Tools > Missing Scripts | Find and remove missing script references |
| **Always Start From Scene 0** | Tools > AK > AlwaysStartsFromScene0 | Force Play mode to start from bootstrap scene |
| **Inspector Ping Button** | Automatic on all inspectors | Ping button in every Inspector header |

---

## Wiring It All Together: GameBindings

The glue is a `GameBindings` MonoBehaviour implementing Reflex's `IInstaller`. It lives in your bootstrap scene and wires everything into the DI container.

```csharp
public sealed class GameBindings : MonoBehaviour, IInstaller
{
    [SerializeField] private AppStateMachine.AppStateMachine _appStateMachine;
    [SerializeField] private BootState _bootState;
    [SerializeField] private MetaDataRepository _metaDataRepository;
    // ... other scene references

    public GameModel GameModel;

    public void InstallBindings(ContainerBuilder builder)
    {
        // App states
        builder.AddSingleton(_appStateMachine, typeof(AppStateMachine.AppStateMachine), typeof(IAppStateMachine));
        builder.AddSingleton(_bootState, typeof(BootState));

        // MetaData
        builder.AddSingleton(_metaDataRepository, typeof(MetaDataRepository), typeof(IMetaDataRepository));
        _metaDataRepository.UIDRegistry.Initialize();

        // Game model - load from save
        GameModel = GameModel.Load();
        builder.AddSingleton(GameModel, typeof(GameModel));

        // Services
        builder.AddSingleton(_metaDataRepository.CreateAdService(), typeof(IAdsService));
        builder.AddSingleton(container =>
            new PurchaseService(_metaDataRepository, container.Resolve<GameModel>(), new UnityIAPService()),
            typeof(IPurchaseService));

        // Firebase
        builder.AddSingleton(new FirebaseInitializationService(), typeof(IFirebaseInitializationService));
        builder.AddSingleton(container =>
            new FirebaseRemoteConfigService(_metaDataRepository, container.Resolve<IFirebaseInitializationService>()),
            typeof(IRemoteConfigService));
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        GameModel.Commit();
    }
}
```

### BootState

Your `BootState` ScriptableObject is where initialization happens:

```csharp
[CreateAssetMenu(menuName = "AppStateMachine/BootState")]
public class BootState : AppState
{
    [Inject] private readonly IMetaDataRepository _metaDataRepository;
    [Inject] private readonly GameModel _gameModel;
    [Inject] private readonly IRemoteConfigService _remoteConfigService;
    [Inject] private readonly IFirebaseInitializationService _firebaseInit;

    public override void OnEnter()
    {
        _metaDataRepository.InitializeRegistries();
        _gameModel.Initialize(_metaDataRepository, out bool isFirstLaunch);
        BootAsync().Forget();
    }

    private async UniTask BootAsync()
    {
        bool firebaseAvailable = await _firebaseInit.InitializeAsync();
        await _remoteConfigService.InitializeAsync();
        // ... more async initialization
        AppStateMachine.ChangeState(_mainMenuState);
    }
}
```

---

## Preprocessor Symbol Reference

Toggle via **Tools > UGFW > Define Symbols**:

| Symbol | Enables |
|--------|---------|
| `ADMOB_ENABLED` | Google Mobile Ads SDK (AdMob provider) |
| `FIREBASE_INITIALIZATION` | Firebase Core SDK initialization |
| `FIREBASE_ANALYTICS` | Firebase Analytics provider |
| `FIREBASE_REMOTE_CONFIG` | Firebase Remote Config service |
| `GAME_ANALYTICS` | GameAnalytics SDK provider |
| `IAP` | Unity In-App Purchasing (UnityIAPService) |

Each Firebase capability is a separate symbol because each requires its own Firebase SDK package (they are NOT a single SDK).

---

## Pulling Updates

```bash
cd Assets/UGFW
git pull origin main
```

Or from project root:

```bash
git submodule update --remote Assets/UGFW
```

## Making Changes

```bash
cd Assets/UGFW
git add -A
git commit -m "your change"
git push
```
