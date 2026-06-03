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
- **DOTween** - Install from Asset Store or OpenUPM. After import, click **Tools → Demigiant → DOTween Utility Panel → Setup DOTween** to generate assembly definitions.
- **Addressables** - `com.unity.addressables` 2.11+
- **Cinemachine** - `com.unity.cinemachine` 3.1+

### Required Scripting Define Symbol

After installing all dependencies, add the following scripting define symbol in **Edit → Project Settings → Player → Scripting Define Symbols**:

```
UNITASK_DOTWEEN_SUPPORT
```

This is required for UniTask's DOTween extensions (`Tween.ToUniTask()`) which UGFW's animation system depends on.

## Optional Dependencies

These enable additional service implementations. Toggle them via **Tools > UGFW > Define Symbols**:

- **Unity Purchasing** - Enables IAP service (`IAP`)
- **Google Mobile Ads** - Enables AdMob provider (`ADMOB_ENABLED`)
- **Firebase Core** - Enables Firebase initialization (`FIREBASE_INITIALIZATION`)
- **Firebase Analytics** - Enables Firebase analytics provider (`FIREBASE_ANALYTICS`)
- **Firebase Remote Config** - Enables remote config service (`FIREBASE_REMOTE_CONFIG`)
- **GameAnalytics** - Enables GameAnalytics provider (`GAME_ANALYTICS`)
- **Unity Notifications** - Enables notification service (`UNITY_NOTIFICATIONS`)

---

## Architecture Overview

```
Assets/UGFW/Runtime/
    Core/           - Foundation: state machines, persistence (PersistableState), UID, events, camera, resource loading
    CoreDomain/     - Domain definitions: costs, currencies, rewards, ads, analytics, store, models
    Services/       - SDK integrations: ads, analytics, IAP, remote config, notifications
    UISystem/       - Full UI framework: screens, fragments, animations, pooling
Assets/UGFW/Editor/ - Editor tools: define symbols window, UI visualizer, UID editor, scene loader
Assets/UGFW/Examples/ - Example implementations: game model, providers, MetaData domains (achievements, daily challenges, etc.)
```

Each runtime module has its own assembly definition (`AK.Core`, `AK.CoreDomain`, `AK.Services`, `AK.UISystem`).

### Use Assembly Definitions From the Start

UGFW is built around assembly definitions from day one. Every runtime module is its own assembly — this isn't optional, it's how the framework works. When adding your own game code, **always create an `.asmdef`** for your namespaces. Don't dump everything into `Assembly-CSharp`.

Why this matters:
- **Compile times** — Assembly-CSharp recompiles on every script change. With asmdefs, only the changed assembly recompiles.
- **Dependency clarity** — `.asmdef` references make it explicit what depends on what. No circular tangles.
- **Consistency with UGFW** — UGFW modules reference each other through asmdef references. Your game code should follow the same pattern.

When creating an asmdef for your game code, reference the UGFW assemblies you need (e.g., `AK.Core`, `AK.UISystem`). The UGFW assemblies will already have their internal references set up correctly.

### Dependency Injection — No Managers, No Singletons

UGFW uses **Reflex** as its DI container. The container flows through the entire framework:

- **AppStateMachine** injects the container into every `AppState` on transition
- **UIViewSystem** injects the container into every `UIView` when shown
- **GameBindings** (your installer) wires everything into the container at startup

This means you should **never use Managers or Singletons**. If you need a service, an API, or shared state — register it in GameBindings and inject it with `[Inject]`:

```csharp
// ❌ Bad — Manager pattern / Singleton
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public MyGameModel Model => _model;
    // ...
}

// ❌ Bad — accessing via FindObjectOfType or .Instance
var model = GameManager.Instance.Model;

// ✅ Good — register in GameBindings, inject where needed
public class GameBindings : MonoBehaviour, IInstaller
{
    public MyGameModel GameModel;

    public void InstallBindings(ContainerBuilder builder)
    {
        builder.RegisterValue(GameModel, new[] { typeof(MyGameModel) });
    }
}

// ✅ Good — inject wherever you need it
public class MyUIView : UIView
{
    [Inject] private readonly MyGameModel _gameModel;
}
```

Every UGFW module is designed to receive its dependencies through the container — not by reaching out to singletons. This keeps things testable, decoupled, and scalable.

### GameBindings — The Bootstrap

The `GameBindings` MonoBehaviour is the glue. It implements Reflex's `IInstaller` and lives in your bootstrap scene. This is where you register everything the app needs — app states, models, services, repositories — all in one place.

```csharp
public sealed class GameBindings : MonoBehaviour, IInstaller
{
    [SerializeField] private AppStateMachine.AppStateMachine _appStateMachine;
    [SerializeField] private BootState _bootState;
    [SerializeField] private MetaDataRepository _metaDataRepository;

    [Header("Providers — create as SO assets, assign here")]
    [SerializeField] private SoftCurrencyCostProvider _softCurrencyCostProvider;
    [SerializeField] private CurrencyRewardProvider _currencyRewardProvider;

    [Header("IAP (optional — leave null for games without IAP)")]
    [SerializeField] private IIAPService _iapService;

    public MyGameModel GameModel;

    public void InstallBindings(ContainerBuilder builder)
    {
        // App states
        builder.RegisterValue(_appStateMachine, new[] { typeof(AppStateMachine), typeof(IAppStateMachine) });
        builder.RegisterValue(_bootState, new[] { typeof(BootState) });

        // MetaData
        builder.RegisterValue(_metaDataRepository, new[] { typeof(MetaDataRepository), typeof(IMetaDataRepository) });

        // Game model — load from save, initialize
        GameModel = MyGameModel.Load();
        GameModel.SetMetaDataRepository(_metaDataRepository);
        GameModel.Initialize(out bool isFirstLaunch);
        builder.RegisterValue(GameModel, new[] { typeof(MyGameModel) });

        // Cost Service — init providers before registering
        var costService = new CostService();
        costService.RegisterProvider(_softCurrencyCostProvider);
        builder.RegisterValue(costService, new[] { typeof(ICostService) });

        // Reward Service — init providers before registering
        var rewardService = new RewardService();
        rewardService.RegisterProvider(_currencyRewardProvider);
        builder.RegisterValue(rewardService, new[] { typeof(IRewardService) });

        // Purchase Service — IAP is optional (null = no IAP)
        var purchaseService = new PurchaseService(costService, rewardService, _iapService);
        builder.RegisterValue(purchaseService, new[] { typeof(IPurchaseService) });
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) GameModel?.Commit();
    }

    private void OnApplicationQuit()
    {
        GameModel?.Commit();
    }
}
```

**Key pattern:** Register services by both concrete type AND interface type. Consumers inject the interface — they never know (or care about) the concrete implementation. This makes swapping implementations trivial.

Once registered in GameBindings, everything is available via `[Inject]` throughout the app — in `AppState` lifecycle hooks, `UIView` lifecycle hooks, and any class constructed by the container.

### Startup Flow

```
Scene Loads
  → GameBindings.Awake() — Reflex builds the container, InstallBindings() runs
  → GameBindings.Start() — (your early logic if needed)
  → AppStateMachine.Start() — Boot() runs, BootState.OnEnter() fires
  → BootState — initializes game, transitions to first real state
```

**GameBindings is the very first thing that happens.** The DI container is fully built before `AppStateMachine` starts its boot state. This guarantees that when `BootState.OnEnter()` runs, all dependencies registered in GameBindings are available via `[Inject]`.

## Module: UISystem

A unified view framework where **Screen** and **Fragment** are the same `UIView` class. The distinction is purely logical — determined at runtime by the presence of a `UIViewChannel` component.

### Screen vs Fragment

| | Screen | Fragment |
|---|---|---|
| **Has UIViewChannel?** | Yes | No |
| **Has Canvas?** | Yes — created by UIViewChannel | No — shares parent's Canvas |
| **Typical use** | Full-screen UIs (main menu, gameplay HUD, settings overlay) | Everything else (buttons, panels, popups, toasts) |
| **Where it lives** | Root container (`_viewsContainer`) | Inside a parent's `FragmentContainer` |
| **Stack** | Channel stack (`_channelStacks`) | Per-parent history stack (`_historyStacks`) |
| **Sorting** | `(int)UIChannel + stackDepth` | Inherits from parent Canvas |

**Rule of thumb:** If it's a full-screen UI, make it a Screen. Everything else is a Fragment. Since a Fragment doesn't need its own Canvas, it renders inside its parent's Canvas — fewer Canvas objects means better performance on mobile.

### Setup

1. Create a `UIViewRepository` ScriptableObject (Create → Gameplay → UIViewRepository) and assign all your UIView prefabs
2. Add a `UIViewSystem` MonoBehaviour to your scene, assign the repository and a `_viewsContainer` transform
3. Assign a `Camera` for `ScreenSpaceCamera` canvases
4. Inject Reflex's DI Container

### Showing Views

Every `Show` method has an async variant (`ShowAsync`) and a fire-and-forget variant (`Show`). Use `Show` when you don't need to wait for the animation to complete — it starts the show and returns the view instance immediately.

The `onInit` callback runs **before any lifecycle event** — before `SetContext`, before `OnPrepareShow`, before `RegisterResources`. Use it to call an Init method on the view that must run first, e.g. injecting a dependency the view needs during its lifecycle hooks.

```csharp
// Show a screen (fire-and-forget — returns immediately)
viewSystem.Show<MainMenuScreen>();

// Show a screen and await animation completion
await viewSystem.ShowAsync<MainMenuScreen>();

// Show a screen on a specific channel
viewSystem.Show<SettingsScreen>(channelOverride: UIChannel.Overlay);

// Show a fragment inside a parent view
var fragment = ShowFragment<CurrencyPanel>();

// Show a fragment with typed context
var fragment = ShowFragment<RewardPopup>(new RewardPopupContext { RewardUID = rewardUID });

// Show a fragment with custom stack behaviour
var fragment = ShowFragment<SettingsPanel>(stackBehaviour: ViewStackBehaviour.HideBelow);

// Use onInit to initialize the view before any lifecycle event fires
var fragment = ShowFragment<RewardPopup>(
    new RewardPopupContext { RewardUID = rewardUID },
    onInit: view => view.Init(rewardService)  // runs before OnPrepareShow, RegisterResources, etc.
);
```

### ViewStackBehaviour — What Happens to the View Below

When a new view is shown on top of an existing one, `ViewStackBehaviour` controls what happens to the previous view:

| Behaviour | Previous View |
|---|---|
| `DoNothing` | Stays visible and interactive |
| `HideBelow` | Paused → hidden with animation → input blocked |
| `PauseOnlyBelow` | Paused → stays visible → input blocked |
| `PauseAndHideBelow` | Paused → hidden with animation → input blocked |
| `CloseBelow` | Fully closed and destroyed |

When the top view is closed, the previous view is **automatically resumed** using the inverse logic — `HideBelow` and `PauseAndHideBelow` trigger resume animation, `PauseOnlyBelow` just restores interactivity.

### UIChannel — Sorting Layers

Screens are organized into channel stacks by sort order:

```csharp
public enum UIChannel
{
    HUD     = 0,    // Always-on gameplay UI
    Menu    = 100,  // Menus, settings, popups
    Overlay = 200   // System overlays, tutorial highlights
}
```

Higher channels always render on top. Multiple screens on the same channel stack (e.g., two Menu screens) — the newer one gets `sortingOrder = 100 + stackDepth`. Override a screen's channel at show time with the `channelOverride` parameter.

### Static vs Dynamic Fragments

**Static fragments** are pre-placed as children in the prefab. They survive `Normal` close — they're hidden, not destroyed. When the parent re-shows, static fragments with `ShowOnStart = true` automatically reappear. Use statics for permanent UI elements like a currency bar or navigation tabs that always exist in a screen.

**Dynamic fragments** are spawned at runtime via `ShowFragment<T>()`. They are always destroyed on close (or returned to pool if `ReturnToPoolOnClose = true`). Use dynamics for popups, toasts, contextual panels — anything that comes and goes.

```csharp
// In the parent's Inspector:
// _staticViews list → add pre-placed child UIViews
//   ShowOnStart → auto-show when parent shows
//   SetActive → initial active state

// Dynamic — spawned at runtime
var popup = ShowFragment<RewardPopup>(new RewardPopupContext { ... });
```

### Child Fragment Auto-Cleanup

When a parent view closes, its children are handled automatically:

- **Parent is destroyed** (Dynamic parent, or `CloseContext.ParentDestroyed`/`ForceDestroy`): All children are destroyed immediately — dynamic children are destroyed recursively, static children are cleaned up and destroyed too.
- **Parent is hidden** (Static parent, `CloseContext.Normal`): Dynamic children are destroyed. Static children are hidden — they stay in the hierarchy for next show.

This means you never need to manually track and close child fragments. If a screen closes, all its fragments clean up automatically.

### UIView Lifecycle

Every UIView follows the same lifecycle. Override the virtual methods you need:

```
Show:
  SetActive(true)
  OnPrepareShow()              ← reset visual state while still invisible
  RegisterResources()          ← subscribe to events (called ONCE per lifecycle)
  [play show animation]
  OnShow()                     ← enable interactions, start timers
  ShowStaticChildrenOnStart()  ← auto-show static fragments with ShowOnStart

Pause (a higher-priority view covers this one):
  OnPause()                    ← pause game logic, timers
  [play hide animation - resources stay registered]

Resume (the covering view is closed):
  [play show animation]
  OnResume()                   ← resume game logic, timers

Close:
  OnPrepareHide()              ← save state, cleanup visuals
  [play hide animation]
  OnHide()                     ← disable interactions, stop timers
  UnRegisterResources()        ← unsubscribe from events
  [destroy / pool / hide depending on static/dynamic + CloseContext]
```

**Key detail:** `RegisterResources` / `UnRegisterResources` are called exactly once per view lifecycle (guarded internally). On a normal pause/resume cycle, resources stay registered — the view is logically alive, just not visible.

### Typed Context — UIView\<TContext\>

Pass data to views using `UIContext` subclasses:

```csharp
// Define a context
public class RewardPopupContext : UIContext
{
    public UID RewardUID;
    public int Amount;
}

// Create a typed view
public class RewardPopup : UIView<RewardPopupContext>
{
    // Context is strongly typed — no casting needed
    protected override void OnShow()
    {
        rewardLabel.text = Context.Amount.ToString();
    }
}

// Show with context
ShowFragment<RewardPopup>(new RewardPopupContext { RewardUID = uid, Amount = 100 });
```

### Fragment Navigation — GoBack

Fragments maintain a per-parent history stack. `GoBack()` pops the current fragment and resumes the previous one:

```csharp
// User navigates: HomePanel → SettingsPanel → SoundPanel
// Calling GoBack() on SoundPanel:
//   SoundPanel closes (destroyed)
//   SettingsPanel resumes (animation plays)
```

The system also handles mid-stack removal correctly — if a fragment in the middle of the stack is closed, it checks whether the fragment below is still covered by anything above before resuming it.

### Animation System

Animations are ScriptableObject strategies. Assign a `UIAnimationConfig` to any UIView's `_animationConfig` field:

```csharp
// UIAnimationConfig wraps:
//   _animationStrategy       — the SO that defines show/hide animations
//   _playInParallelWithPrevious — true = crossfade, false = sequential
```

30+ built-in strategies including Fade, Slide, Scale, Bounce, Elastic, PopSnap, CardDeal, ConfettiBurst, and more. All use DOTween under the hood. Create your own by extending `AnimationStrategy`:

```csharp
[CreateAssetMenu(menuName = "UISystem/Animations/MyCustomAnimation")]
public class MyCustomAnimation : AnimationStrategy
{
    public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
    {
        return target.DOScale(Vector3.one, EntryDuration).SetEase(EntryEase);
    }

    public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
    {
        return target.DOScale(Vector3.zero, ExitDuration).SetEase(ExitEase);
    }
}
```

### Object Pooling

Dynamic fragments with `ReturnToPoolOnClose = true` are returned to a `ViewPool` instead of destroyed. The pool keys by `(Type, ViewId)`, so you can have multiple variants of the same view type. Pooled views go through `OnBeforePool()` → `OnReset()` before returning to the pool, and are fully re-initialized on next show.

### Background Overlay

Any UIView with `_showBackgroundOverlay = true` automatically shows a dark overlay at sibling index 0 (renders behind the view's content). The overlay fades in over 0.4s, fades out over 0.1s. Useful for modal-style screens and popups.

### Common Components

| Component | Description |
|---|---|
| **UIFragButton** | Button with Animator + TMP text. Auto-enables Animator on show. |
| **UIFragTooltip** | 8-position auto-placement tooltip with edge clamping and auto-hide timer. |
| **UIFragLoadSpinner** | Loading spinner with `AutoCloseAfterSeconds(int)` for timed auto-close. |
| **UIViewToast** | Floating toast notification. Moves up 120px over 2s then fades out. |
| **UIViewBanner** | Top banner with text, duration timer, and Animator support. |
| **UITutorialArrow** | Oscillating arrow pointing at a target. Auto-rotates toward target. |

### Tutorial Mode

Any UIView can enter tutorial mode via `SetupTutorialMode()`. This:
1. Shows a very dark background overlay (0.95 alpha)
2. Forces the view's Canvas to sort above everything (`Overlay + 1`)
3. Fragments (which don't own a Canvas) get a temporary override Canvas + GraphicRaycaster

Call `CleanupTutorialMode()` to revert.

### Quick Reference

| I need to... | |
|---|---|
| Create a full-screen UI | Add `UIViewChannel` component to the UIView prefab → it becomes a Screen |
| Create a panel/popup | UIView with NO `UIViewChannel` → it's a Fragment, lives in parent's Canvas |
| Show a screen | `viewSystem.ShowAsync<T>()` or `viewSystem.Show<T>()` |
| Show a fragment | `parentView.ShowFragment<T>()` or `parentView.ShowFragmentAsync<T>()` |
| Pass data to a view | Create a `UIContext` subclass, pass via show method or `UIView<TContext>` |
| Navigate back | `view.GoBack()` — pops from per-parent history stack |
| Make a fragment auto-close when parent closes | It already does — child fragments auto-cleanup on parent close |
| Pre-place a fragment that survives close | Add it to parent's `_staticViews` list — statics are hidden, not destroyed |
| Pool a frequently-spawned fragment | Set `_returnToPoolOnClose = true` on the UIView |
| Allow multiple instances of same fragment | Set `_allowMultipleInstances = true` |
| Add a dark overlay behind a screen | Set `_showBackgroundOverlay = true` |
| Create a custom animation | Extend `AnimationStrategy`, override `PlayShowAnimation` and `PlayHideAnimation` |

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

The **entry point** of the app. Requires a `_bootState` asset reference in the Inspector — this state runs on `Start()` and kicks off the entire app flow.

Application-level state machine using **ScriptableObject** states. Designed for coarse-grained app states (Boot, MainMenu, Gameplay, LevelEditor).

**Why ScriptableObject?** App-level states often need scene-independent references — a GameplayState might need a prefab reference for spawning a player, a BootState might need an initial config asset. SOs let you drag these references in the Inspector, which is far more convenient than hardcoding paths or using `Resources.Load`. For everything else, use the `UniResources` API (see below).

#### Defining States

```csharp
// Simple state
[CreateAssetMenu(menuName = "AppStateMachine/BootState")]
public class BootState : AppState
{
    [Inject] private readonly GameModel _gameModel;
    [Inject] private readonly IMetaDataRepository _metaDataRepository;

    public override void OnEnter()
    {
        _gameModel.Initialize(_metaDataRepository, out bool isFirstLaunch);
        AppStateMachine.ChangeState(isFirstLaunch ? _tutorialState : _mainMenuState);
    }
}
```

#### State Lifecycle

| Method | When It's Called |
|---|---|
| `OnEnter()` | State entered fresh (not in paused stack) |
| `OnExit()` | State fully leaving (not being paused) |
| `OnPause()` | State pushed to paused stack (`pauseCurrent: true`) |
| `OnResume()` | State resumed from paused stack |
| `Tick()` | Every frame while active |
| `SetContext(TransitionContext)` | Before `OnEnter` or `OnResume` — stores the context |

**The pause/resume pattern** is what makes AppStateMachine powerful. When you `ChangeState` with `pauseCurrent: true`, the current state is paused (not exited) — it goes onto a LIFO stack. When that same SO instance is passed to `ChangeState` later, it's detected in the paused list and `OnResume` fires instead of `OnEnter`. This gives you modal-style overlays for free:

```csharp
// In GameplayState — user opens pause menu
AppStateMachine.ChangeState(_pauseMenuState, pauseCurrent: true);
// → GameplayState.OnPause() fires
// → PauseMenuState.OnEnter() fires

// In PauseMenuState — user resumes
AppStateMachine.ChangeState(_gameplayState);
// → _gameplayState is found in the paused list
// → PauseMenuState.OnExit() fires
// → GameplayState.OnResume() fires (NOT OnEnter)
```

#### Transitions & TryGoBack

```csharp
// Replace current state (current gets OnExit)
AppStateMachine.ChangeState(_mainMenuState);

// Push: pause current, enter new (current gets OnPause)
AppStateMachine.ChangeState(_gameplayState, pauseCurrent: true);

// Pop: return to last paused state (current gets OnExit, previous gets OnResume)
AppStateMachine.TryGoBack();
```

`TryGoBack()` removes the last paused state and transitions to it. Useful for back-button patterns.

#### Typed TransitionContext

Pass data between states by subclassing `TransitionContext`:

```csharp
public class LevelLoadContext : TransitionContext
{
    public UID LevelUID;
    public bool IsRestart;
}

// Typed state — access context with no casting
public class GameplayState : AppState<LevelLoadContext>
{
    protected override void OnEnter()
    {
        var levelUID = _context.LevelUID;   // strongly typed
        var isRestart = _context.IsRestart;
    }
}

// Pass context when transitioning
AppStateMachine.ChangeState(_gameplayState, context: new LevelLoadContext { LevelUID = uid, IsRestart = false });
```

#### Setup

1. Create `AppState` ScriptableObjects for each app state
2. Add `AppStateMachine` MonoBehaviour to your scene
3. Assign the `_bootState` in Inspector — this state runs on `Start()`
4. Inject Reflex's DI Container — states receive `[Inject]` dependencies automatically

**Key details:**
- States are identity-based — the *same SO instance* must be passed to `ChangeState` for resume detection. Different SO instances of the same type won't match.
- DI injection happens on every transition — SOs get fresh `Container` injection each time they become active.
- `AppStateMachine` ticks `CurrentState.Tick()` every frame in `Update()`.
- Editor-only hotkey: press **B** during Play Mode to call `TryGoBack()` for quick debugging.

### UniResources — Resource Loading API

A static façade over Unity's resource loading, powered by **Addressables** by default. Use this instead of `Resources.Load` — Addressables should be used from the get-go for scalable games. `UniResources` abstracts the Addressables API behind a clean surface so your code never directly depends on it.

> **⚠️ Common bad practice:** Dragging a prefab `GameObject` as a `[SerializeField]` into a MonoBehaviour forces Unity to include that asset in the scene's bundle, bloating build size and killing incremental loading. Instead, use `AssetReferenceGameObject` or `AssetReference<T>` and load/spawn through `UniResources`. This keeps assets addressable, loadable on demand, and memory-efficient.

#### Loading Assets

```csharp
// Load a single asset by address string
var texture = await UniResources.LoadAssetAsync<Texture2D>("my_texture");

// Load a single asset by AssetReference (preferred)
[SerializeField] private AssetReferenceT<Texture2D> _iconRef;
var icon = await UniResources.LoadAssetAsync<Texture2D>(_iconRef);

// Batch load by keys
var group = await UniResources.LoadAssetsAsync<Sprite>>(new[] { "icon1", "icon2" });

// Synchronous (blocks frame — avoid in production)
var tex = UniResources.LoadAsset<Texture2D>("my_texture");
```

#### Spawning (Instantiating)

```csharp
// Spawn a GameObject by address
var instance = await UniResources.SpawnAsync("enemy_prefab", transform);

// Spawn from AssetReference (preferred)
[SerializeField] private AssetReferenceGameObject _enemyPrefab;
var enemy = await UniResources.SpawnAsync(_enemyPrefab, transform);

// Spawn + get component (throws if component missing)
var enemyAI = await UniResources.SpawnAsync<EnemyAI>(_enemyPrefab, transform);
```

#### Cleanup

```csharp
// Release a loaded asset
UniResources.DisposeAsset(texture);

// Release a spawned instance
UniResources.DisposeInstance(instance.gameObject);

// Release a batch-loaded group
UniResources.DisposeAssetsGroup(group);
```

**Key principles:**
- Always prefer `AssetReference` / `AssetReferenceT<T>` over hardcoded address strings — the Inspector validates the reference at edit time
- Always prefer `*_Async` methods over synchronous ones — sync blocks the frame with `WaitForCompletion()`
- Always call `DisposeAsset` / `DisposeInstance` when done — Addressables doesn't auto-release
- Use `GetRemoteResourcesSizeAsync` / `GetRemoteDependenciesAsync` for downloadable content

### Timer

A UniTask-based timer with countdown, count-up, and interval modes. Not a coroutine wrapper — built on `CancellationTokenSource` and `UniTask.Delay`.

```csharp
// Countdown — 60 second timer with tick callback
var timer = new Timer();
timer.StartCountdown(
    TimeSpan.FromMinutes(1),
    onTick: (remaining, progress) => UpdateUI(remaining, progress),  // progress is 0→1
    onComplete: () => Debug.Log("Time's up!")
);

// Count-up — stopwatch mode
timer.StartCountUp(
    onTick: (elapsed, progress) => UpdateTimerDisplay(elapsed)
);

// Interval — repeat every 5 seconds, forever
timer.StartInterval(
    TimeSpan.FromSeconds(5),
    repeatCount: -1,  // -1 = infinite
    onInterval: (count) => Debug.Log($"Tick #{count}")
);

// Pause / Resume / Stop
timer.Pause();
timer.Resume();
timer.Stop();       // resets to Idle, no OnCancel
timer.Dispose();    // fires OnCancel, clears subscriptions, instance is dead
```

**Key details:**
- `onTick` receives `(TimeSpan remaining, float progress)` where progress is 0→1 normalized
- `TickInterval` controls how often `OnTick` fires (default 100ms)
- `UseRealTime = true` ignores `Time.timeScale` — use for UI countdowns during pause
- `StartCountdown`/`StartCountUp` **add** handlers to `OnTick`/`OnComplete` — they don't replace. Don't call Start multiple times without clearing.

#### TimerExtensions — UI Bindings

```csharp
// Auto-bind timer to a TMP text — disposes to unbind
var binding = timer.BindToText(countdownLabel, "mm\\:ss");
// ... later
binding.Dispose();

// Bind progress to UI fill (0→1)
var fillBinding = timer.BindToFill(cooldownOverlay);

// Bind progress inverted (1→0) — useful for cooldown overlays
var inverseFill = timer.BindToFillInverse(cooldownOverlay);
```

Formatting helpers: `remaining.ToMMSS()` → `"05:30"`, `remaining.ToCompactFormat()` → `"5m 30s"`

### AudioSpawner

A registry-driven, pooled audio system. Create `AudioConfigBase` ScriptableObjects for each sound type, register them in an `AudioRegistry`, and the `AudioSpawner` handles pooling, playback, and cleanup.

```csharp
// Primary API — play any sound by UID (covers 99% of use cases)
audioSpawner.PlayAudio(coinPickupUID);
audioSpawner.PlayAudio(explosionUID, position: hitPoint);  // 3D spatial

// Type-safe spawn — returns AudioComponent without auto-playing
var audio = audioSpawner.Spawn<MusicAudioComponent>(variantUID);
audio.Play();
```

#### AudioConfigBase — Sound Configuration

Each sound is an SO with:
- **Clips** — list of AudioClips (one is picked randomly, or play all sequentially)
- **PitchRange** — random pitch between min/max (default 0.95–1.05 for variation)
- **Volume, FadeIn/Out duration** — smooth volume transitions
- **Loop, LoopInterval** — loop with optional fixed interval between iterations
- **IsSpatial** — true = 3D sound (spatialBlend=1), false = 2D UI sound
- **StartAfterSeconds / StopAfterSeconds** — delayed start, auto-stop
- **InitialPoolSize** — pre-warmed pool count

#### AudioMixerController

```csharp
// Set volume by mixer parameter name (0→1 linear, converted to dB internally)
mixerController.SetVolume("MasterVolume", 0.5f);

// Transition to snapshotted mix (e.g. lower music during pause)
mixerController.TransitionToSnapshot(pausedSnapshot, transitionTime: 0.5f);
```

### ParticleSpawner

Same registry+config+pooling pattern as AudioSpawner but for particle systems. Create `ParticleConfigBase` SOs for each particle effect, register in a `ParticlesRegistry`, and the `ParticleSpawner` handles lifecycle.

```csharp
// Spawn a particle by type
var explosion = particleSpawner.Spawn<ExplosionParticle>();
explosion.Show(hitPoint);

// Spawn with position + rotation + color override
var firework = particleSpawner.Spawn<FireworkParticle>(variantUID);
firework.Show(position, rotation, color: Color.red);

// Async variant (uses InstantiateAsync)
var effect = await particleSpawner.SpawnAsync<SmokeParticle>();
effect.Show(transform.position);
```

#### ParticleConfigBase — Effect Configuration

- **Prefab** — the `ParticleComponent` prefab
- **StartDelayInSeconds** — delay before particle plays
- **StopAfterSeconds** — for looping particles, auto-stop after this time
- **InitialPoolSize** — pre-warmed pool count; 0 = no pooling (create-destroy)

#### Auto-Return to Pool

`ParticleComponent` sets `ParticleSystemStopAction.Callback` — when the particle system finishes, `OnParticleSystemStopped()` fires, the `onStop` callback runs, and the component returns to the pool automatically. You never manually return particles.

### JobDispatcher

A lock-free, multi-threaded job scheduling system with frame-level precision. Three threads work in lock-step: **Main Thread** (Unity API jobs), **Worker Thread** (background computation), and **Handler Thread** (buffer preparation). The system uses a "one frame ahead" double-buffer strategy — while threads execute from the front buffer, the handler prepares the back buffer for the next frame, eliminating contention.

```csharp
// Inject IJobDispatcher
[Inject] private readonly IJobDispatcher _jobDispatcher;
```

#### Execute on Main Thread (Unity API safe)

```csharp
// Immediate — runs this frame
_jobDispatcher.UnityThread.Execute(() => transform.position = newPos);

// Next frame
_jobDispatcher.UnityThread.ExecuteInNextFrame(() => RefreshUI());

// After delay
_jobDispatcher.UnityThread.ExecuteAfterDelay(() => ShowResult(), 2.0f);

// Every Update
var handle = _jobDispatcher.UnityThread.ExecuteEveryUpdate(() => PollInput());

// Every FixedUpdate
var handle = _jobDispatcher.UnityThread.ExecuteEveryFixedUpdate(() => ProcessPhysics());

// Repeating at interval
var handle = _jobDispatcher.UnityThread.InvokeRepeating(() => SyncState(), 1.0f, 0.5f);

// At specific frame
_jobDispatcher.UnityThread.ExecuteAtFrame(() => FrameExactAction(), targetFrame);

// Cancel a repeating/scheduled job
handle.CancelJob();
```

#### Execute on Worker Thread (offload heavy work)

```csharp
// Run on background thread — NO Unity API access in the job
_jobDispatcher.WorkerThread.Execute(() =>
{
    var result = HeavyComputation();     // pure C# computation
    // Cannot touch GameObject, Transform, etc. here
});

// Get result back on main thread — use callCompleteOnMainThread
_jobDispatcher.WorkerThread.Execute(
    job: () =>
    {
        var data = ComputePathfinding();   // background thread
    },
    onComplete: () =>
    {
        RenderPath(data);                  // main thread — safe to use Unity API
    },
    callCompleteOnMainThread: true
);
```

#### IDispatchableJob — Structured Jobs

For more complex jobs, implement `IDispatchableJob`:

```csharp
public class PathfindJob : IDispatchableJob
{
    private Vector3 _start, _end;
    private List<Vector3> _path;

    public void OnExecute()
    {
        // Runs on worker thread — heavy computation
        _path = AStar.Compute(_start, _end);
    }

    public void OnComplete()
    {
        // Runs on the thread that dispatched the job
        // (or main thread if callCompleteOnMainThread was true)
        RenderPath(_path);
    }

    public void OnStop() { /* cleanup */ }
}

// Dispatch
_jobDispatcher.WorkerThread.ExecuteJob(new PathfindJob(), callCompleteOnMainThread: true);
```

#### How the Pipeline Works

```
Frame N:   Threads execute from FrontBuffer[N]
           Handler prepares BackBuffer[N+1] from backlog

Frame N+1: Buffers swap atomically
           Threads execute from FrontBuffer[N+1] (prepared last frame)
           Handler prepares BackBuffer[N+2]
```

**Three-tier job classification:**
- **Immediate** (`CurrentFrameJobs`) — bypass handler, execute this frame
- **Near future** (`NextFrameJobs`) — bypass handler, execute next frame
- **Distant future** (handler backlog) — handler schedules into back buffer

This means immediate jobs have zero scheduling overhead, and the handler never becomes a bottleneck.

**Key rules:**
- Workers run on a background thread — never access Unity API from `OnExecute()`
- Use `callCompleteOnMainThread: true` to safely use Unity API in `OnComplete()`
- Keep jobs short (< 16ms) to avoid frame drops
- Use `IDispatchedJobHandle.CancelJob()` to cancel scheduled/repeating jobs
- Access frame timing via `IJobDispatcher.FrameCounter`, `.UnityTime`, `.Dt`

### NumberFormatter

Static utility for formatting large numbers into abbreviated, mobile-friendly strings. Extension methods on `int`, `long`, `float`, `double`.

```csharp
// Abbreviated format — auto-scales with K/M/B/T/Q suffixes
1500.FormatAbbreviated()          // "1.5K"
2500000.FormatAbbreviated()       // "2.5M"
1000000000.FormatAbbreviated()    // "1B"
42.FormatAbbreviated()            // "42"  (< 1000, no suffix)

// Control decimal places
coins.FormatAbbreviated(decimalPlaces: 2)  // "1.50K"

// Floats/doubles — same API
goldAmount.FormatAbbreviated()    // works on float, double, int, long

// Parse abbreviated strings back to numbers
NumberFormatter.ParseAbbreviated("1.5K")   // 1500
NumberFormatter.ParseAbbreviated("2.5M")   // 2500000

// FormatDouble — game-specific formatter with rounding control
// Use roundDown: true for player inventory (never show more than they have)
// Use roundDown: false for costs/targets (never understate what's needed)
NumberFormatter.FormatDouble(1500.7, roundDown: true)   // "1.5K"
NumberFormatter.FormatDouble(1500.7, roundDown: false)  // "2K"

// With min/max decimal range
NumberFormatter.FormatDouble(1234, minDecimals: 0, maxDecimals: 2, roundDown: true)  // "1.23K"

// Parse with TryParse for safe input handling
if (NumberFormatter.TryParse(userInput, out double value))
    ApplyValue(value);
```

**Key details:**
- `FormatAbbreviated` is the simple API — K/M/B/T/Q suffixes, trailing zeros stripped
- `FormatDouble` has rounding control critical for f2p games — inventory rounds down, costs round up
- Numbers below 1000 always show with zero decimals in `FormatDouble`
- Beyond quadrillion (Q), suffixes become double-letter (aa, ab, ...) via `GetSuffix`
- `ParseAbbreviated` reverses `FormatAbbreviated`, `Parse`/`TryParse` reverse `FormatDouble`

### TimeFormatter

Static utility for formatting time durations, parsing time strings, and displaying relative/arrival times. Extension methods on `float`, `int`, `double`.

#### Five Format Styles

```csharp
// Digital — classic clock display
90f.FormatDuration()                              // "01:30"
3661f.FormatDuration()                            // "01:01:01"

// Abbreviated — compact with units
3661f.FormatDuration(TimeFormat.Abbreviated)      // "1h 1m 1s"

// Full — human-readable words
3661f.FormatDuration(TimeFormat.Full)             // "1 Hour 1 Minute 1 Second"

// Compact — no spaces
3661f.FormatDuration(TimeFormat.Compact)           // "1h1m1s"

// Stopwatch — with milliseconds
90.45f.FormatDuration(TimeFormat.Stopwatch)        // "01:30.45"
```

#### Rounding — Critical for F2P

```csharp
// Floor — "Time Played" displays (never overstate)
59f.FormatDuration(rounding: TimeRounding.Floor)   // "00:59"

// Ceil — Cooldowns (59 seconds left shows 1 minute — player must wait)
59f.FormatDuration(rounding: TimeRounding.Ceil)    // "01:00"

// Nearest — general purpose
59f.FormatDuration(rounding: TimeRounding.Nearest) // "01:00"
30f.FormatDuration(rounding: TimeRounding.Nearest) // "00:30"
```

#### Max Units — Control Detail Level

```csharp
3661f.FormatDuration(TimeFormat.Abbreviated, max: 2)  // "1h 1m" (drops seconds)
3661f.FormatDuration(TimeFormat.Abbreviated, max: 1)  // "1h" (hours only)
```

#### Progress, Arrival, and Relative Time

```csharp
// Progress — "current / total" display
90f.FormatProgress(120f)                    // "01:30 / 02:00"

// Arrival time — when will this cooldown end?
600f.GetArrivalTime()                       // "5:30 PM" (clock format)
600f.GetArrivalDescription()                // "Today at 5:30 PM"
86400f.GetArrivalDescription()              // "Tomorrow at 5:30 PM"

// Relative time — how long ago was this?
pastDateTime.ToRelativeTime()               // "5m ago" / "3h ago" / "2d ago"
```

#### Dynamic — Switches Format by Urgency

```csharp
// > 1 minute: Digital format. < 1 minute: decimal seconds.
90f.FormatDynamic()     // "01:30"
45f.FormatDynamic()     // "45.0"
```

#### Parsing

```csharp
// Parse any format back to seconds
TimeFormatter.ParseTime("1h 30m")    // 5400
TimeFormatter.ParseTime("90m")       // 5400
TimeFormatter.ParseTime("01:30:00")  // 5400
TimeFormatter.ParseTime("5400")      // 5400
```

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

## Module: CoreDomain

Game data layer built around the **MetaData system** -- a ScriptableObject-driven architecture for defining all game content as data assets.

### The MetaData Pattern

Every game domain follows a consistent four-part pattern:

```
[Domain]Meta          -- ScriptableObject container (e.g., CurrencyMeta, RewardsMeta)
  -> [Domain]Registry    -- TypedUIDRegistry<Definition> for UID-based lookup
  -> [Domain]Definition  -- The actual data asset (extends MetaDataAsset extends UID)
  -> [Domain]Type        -- ScriptableObject categorizing the domain (e.g., CurrencyType, RewardType)
```

**`[Domain]Type` assets are ScriptableObjects, not enums.** This is critical for making UGFW universal — each game creates its own type assets (e.g., "SoftCurrency", "HardCurrency") instead of being locked to hardcoded enum values. The framework provides the base `MetaDataAsset` class; games create instances via the Create Asset menu.

**Example: The Currency domain**

```
CurrencyMeta (ScriptableObject)
  -> CurrencyRegistry (TypedUIDRegistry<CurrencyDefinition>)
  -> CurrencyDefinition : MetaDataAsset   // fields: Type (CurrencyType SO), MaxAmount, StartingAmount, etc.
  -> CurrencyType : MetaDataAsset         // Create instances: "SoftCurrency", "HardCurrency", "Energy", etc.
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

- `CurrencyDefinition` -- type (CurrencyType SO), max amount, starting amount, exchange rates
- `CurrencyType` -- ScriptableObject asset. Create instances for each currency in your game (e.g., "SoftCurrency", "HardCurrency", "Energy")
- `CurrencyModel` -- runtime model with `Add()`, `Deduct()`, `DeductPartial()`, respects MaxAmount cap
- `CurrencyExchangeRate` -- conversion rates between currencies

#### Rewards

- `RewardDefinition` -- amount, type (RewardType SO), linked currency/bundle/gacha data
- `RewardType` -- ScriptableObject asset. Create instances for each reward type in your game (e.g., "Star", "Currency", "Bundle", "Gacha")
- `RewardBundle` -- ordered/weighted list of sub-rewards (recursive), bundle types: Sequential, Random, Weighted, RandomWeighted, All
- `GachaBundle` -- weighted random reward pool with `EvaluateRewards()` for gacha pulls
- `CheckpointReward` -- rewards tied to progression milestones
- `SubscriptionReward` -- time-based rewards for subscribers

#### Costs & Purchasing

- `CostType` -- ScriptableObject asset. Create instances for each cost type in your game (e.g., "SoftCurrency", "HardCurrency", "Ad")
- `CostOption` -- links a CostType to an amount. Used by `PurchasableItemDefinition`
- `CostProvider` -- abstract SO that handles `CanAfford`/`Deduct` for a specific CostType. Games create their own (e.g., `SoftCurrencyCostProvider`)
- `ICostService` / `CostService` -- dispatches cost operations to the registered `CostProvider` for a given CostType
- `PurchasableItemDefinition` -- bridge between shop items and the purchase system: cost (CostOption), product ID, reward/bundle references

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

### PersistableState — Game Save System

UGFW provides a generic base class `PersistableState<T>` for game state persistence. **There is no built-in `GameModel`** — each game creates its own model by extending this base class. This keeps the framework universal.

#### Creating Your Game Model

```csharp
using AK.Core;
using MyGame.Models; // your game's model namespace

[Serializable]
public class MyGameModel : PersistableState<MyGameModel>
{
    // Override SaveKey for unique prefs key (defaults to "UGFW_GAME_MODEL")
    protected override string SaveKey => "MY_GAME_SAVE";

    // Override for save migration
    protected override int CurrentSaveVersion => 2;
    protected override void OnMigrate() { /* handle version upgrades */ }

    // Add your game-specific fields
    public int TotalStars;
    public int LastPlayedLevel = -1;
    public GameSettingsModel Settings = new();

    // Currency management (common pattern)
    [NonSerialized] private List<CurrencyModel> _currencies = new();
    [SerializeField] private List<SerializableCurrency> _serializedCurrencies = new();

    public CurrencyModel GetCurrencyModel(CurrencyDefinition def) { /* lookup by UID */ }
    public override void OnInitialized(bool isFirstLaunch) { /* resolve UIDs */ }
    public override void OnBeforeSerialize() { /* serialize currencies */ }
    public override void OnAfterDeserialize() { /* deserialize currencies */ }
}
```

#### Using Your Game Model

```csharp
// Load saved game (or get fresh instance)
var gameModel = MyGameModel.Load();

// Initialize (session tracking, migration, first-launch detection)
gameModel.Initialize(out bool isFirstLaunch);

// Persist any time
gameModel.Commit();

// Access currencies
var coins = gameModel.GetCurrencyModel(softCurrencyDef);
coins.Add(100);         // respects MaxAmount cap
gameModel.Commit();     // persist

// Check for save
bool hasSave = MyGameModel.HasSave();
MyGameModel.DeleteSave(); // wipe all data
```

#### Framework Building Blocks

These are provided by the framework for use in your game model:

| Model | Description |
|-------|-------------|
| `PersistableState<T>` | Generic base class with `Load()`, `Commit()`, `DeleteSave()`, `HasSave()`, `Initialize()`, session tracking, save migration |
| `CurrencyModel` | Runtime currency with `Add()`, `Deduct()`, `DeductPartial()`, UID resolution for deserialization |
| `GameSettingsModel` | Audio/vibration/notification preferences with `OnChanged` event |
| `Transaction` | UID + timestamp record for pending rewards/purchases, with deserialization resolution |
| `TransactionType` | ScriptableObject asset for categorizing transactions. Create instances per game (e.g., "LevelComplete", "GachaBox") |
| `SerializableCurrency` | Polymorphic currency serialization helper for `OnBeforeSerialize`/`OnAfterDeserialize` |

#### Extending Definitions for Your Game

All `[Domain]Type` assets are ScriptableObjects. To add new types for your game:

1. **Create the Type asset** via the Create Asset menu (e.g., Create → Gameplay/MetaData/Currency/CurrencyType)
2. **Name it** for your game (e.g., "Gems", "Energy", "PremiumToken")
3. **Reference it** from your Definition assets (e.g., set `CurrencyDefinition.Type` to your new CurrencyType)
4. **Create a Provider** if the domain uses provider-based dispatch (Cost, Reward):

```csharp
// Example: Creating a cost provider for a new currency
[CreateAssetMenu(fileName = "GemCostProvider", menuName = "Game/Costs/GemCostProvider")]
public class GemCostProvider : CostProvider
{
    private CurrencyModel _gemModel;

    public void Init(CurrencyModel gemModel) => _gemModel = gemModel;

    public override bool CanAfford(CostOption costOption) => _gemModel.Amount >= costOption.Amount;
    public override bool Deduct(CostOption costOption) => _gemModel.Deduct(costOption.Amount);
}
```

5. **Register the provider** in your GameBindings:

```csharp
var gemProvider = Instantiate(_gemCostProviderPrefab); // or reference directly
gemProvider.Init(gameModel.GetCurrencyModel(gemCurrencyDef));
costService.RegisterProvider(gemProvider);
```

This pattern applies to all extensible domains: **CurrencyType**, **RewardType**, **CostType**, **TransactionType** — all are SO assets. Create as many instances as your game needs without modifying framework code.

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

Two-layer system: `IIAPService` (raw store operations) and `IPurchaseService` (bridges IAP with costs and rewards via provider dispatch). IAP is **optional** — pass `null` for `IIAPService` in games without IAP.

```csharp
// Create with optional IAP
var purchaseService = new PurchaseService(costService, rewardService, iapService: null);

// High-level purchase (handles cost deduction and reward delivery)
var status = await purchaseService.Purchase(purchasableItemDefinition, immediateCredit: true);

// Check IAP ownership (only when IAP is enabled)
if (purchaseService.IAPService != null)
{
    bool owned = purchaseService.IAPService.IsProductOwned("no_ads");
    bool subscribed = purchaseService.IAPService.IsSubscribed("vip_monthly");
}
```

**Purchase flow:**
1. Item has a `ProductID` and IAP is available → delegates to `IIAPService.PurchaseAsync()`, then credits rewards
2. Item has a `CostOption` → delegates to `ICostService` which dispatches to the registered `CostProvider`
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
