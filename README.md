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
    CoreDomain/     - Framework-core domain definitions: ads, analytics, notifications, remote config + service interfaces (IReward, ICostInfo, IPurchasable, IRewardProvider, ICostProvider)
    Services/       - SDK integrations: ads, analytics, IAP, purchasing, costs, rewards, remote config, notifications
    UISystem/       - Full UI framework: screens, fragments, animations, pooling
Assets/UGFW/Editor/ - Editor tools: define symbols window, UI visualizer, UID identity tooling, scene loader
Assets/UGFW/Examples/ - Example implementations: game model, providers, game-specific MetaData domains (currency, rewards, costs, store, IAP, achievements, etc.)
```

Each runtime module has its own assembly definition (`AK.Core`, `AK.CoreDomain`, `AK.Services`, `AK.UISystem`).

### Use Assembly Definitions From the Start

UGFW is built around assembly definitions from day one. Every runtime module is its own assembly — this isn't optional, it's how the framework works. When adding your own game code, **always create an `.asmdef`** for your namespaces. Don't dump everything into `Assembly-CSharp`.

Why this matters:
- **Compile times** — Assembly-CSharp recompiles on every script change. With asmdefs, only the changed assembly recompiles.
- **Dependency clarity** — `.asmdef` references make it explicit what depends on what. No circular tangles.
- **Consistency with UGFW** — UGFW modules reference each other through asmdef references. Your game code should follow the same pattern.

When creating an asmdef for your game code, reference the UGFW assemblies you need (e.g., `AK.Core`, `AK.UISystem`). The UGFW assemblies will already have their internal references set up correctly.

### Tests

The framework's own edit-mode tests live in `UGFW/Tests/EditMode` (assembly `AK.Tests.EditMode`, namespace `AK.Tests`) and depend only on UGFW assemblies, so they travel with the framework. `Support/UISystemHarness` builds a complete, headless `UISystem` (own Reflex container, in-memory `UIViewRepository`, synthetic view prefabs) — use it for tests that exercise view stacking, pooling or lifecycle without a scene. Play-mode tests that need the real player loop live in `UGFW/Tests/PlayMode` (assembly `AK.Tests.PlayMode`). Game-specific tests (ones that load game assets or reference game code) belong in the game's test assembly, not here. Run everything from *Window ▸ General ▸ Test Runner* or `unity command run_tests --mode editor`.

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

Everything a show can be told travels in one `ShowOptions` value: `Context`, `Parent`, `ViewId`, `Channel`, `StackBehaviour`, `Mode`. `default` is a plain show; the factories cover the common shapes and the fluent copies compose the rest. Setting `StackBehaviour` implies `ShowMode.Serialized` — there is no way to ask for a stack behaviour that is then ignored.

The `onInit` callback runs **before any lifecycle event** — before `SetContext`, before `OnPrepareShow`, before `RegisterResources`. Use it to call an Init method on the view that must run first, e.g. injecting a dependency the view needs during its lifecycle hooks.

```csharp
// Show a screen (fire-and-forget — returns immediately)
viewSystem.Show<MainMenuScreen>();

// Show a screen and await animation completion
await viewSystem.ShowAsync<MainMenuScreen>();

// Typed context
viewSystem.Show<RewardPopup>(ShowOptions.With(new RewardPopupContext { RewardId = rewardId }));

// Show a screen on a specific channel
viewSystem.Show<SettingsScreen>(new ShowOptions(channel: UIChannel.Overlay));

// Variant prefab (multi-variant by ViewId)
viewSystem.Show<UIViewBanner>(ShowOptions.Variant("banner2"));

// From inside a view: fragments hosted by this view
var panel = ShowFragment<CurrencyPanel>();
var popup = ShowFragment<RewardPopup>(ShowOptions.With(new RewardPopupContext { RewardId = rewardId }));

// Negotiate with the fragment below (serialized behind pending shows on this parent)
ShowFragment<SettingsPanel>(new ShowOptions(stackBehaviour: ViewStackBehaviour.HideBelow));

// No animation — tooltips and anything that relocates rapidly
viewSystem.Show<UIViewTooltip>(ShowOptions.With(ctx).Immediate());

// onInit runs before OnPrepareShow, RegisterResources, etc.
ShowFragment<RewardPopup>(ShowOptions.With(ctx), view => view.Init(rewardService));

// Close
viewSystem.Close(view);                                  // animated
viewSystem.Close(view, CloseOptions.Now);                // no animation
viewSystem.Close(view, default, onClosed: RefreshHud);   // callback fires only if the view was open
await viewSystem.CloseAsync(view);

// Toasts and banners are extension helpers, not system members
viewSystem.DisplayToast("Saved");
viewSystem.DisplayBanner("Coming soon", UIViewBanner.DEFAULT_TOP_ID);
```

`ShowMode.Parallel` (the default) pushes onto the parent's history and animates at once — sibling fragments do not negotiate. `ShowMode.Serialized` waits for any show already running on that parent, then applies the new view's stack behaviour to the view below. Screens always run serialized against their channel stack.

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

Higher channels always render on top. Multiple screens on the same channel stack (e.g., two Menu screens) — the newer one gets `sortingOrder = 100 + stackDepth`. Override a screen's channel at show time with `ShowOptions.OnChannel(...)` (or the `channel:` constructor argument).

### Internals — How Lookups Stay Cheap

The system answers three questions on every `Show`/`Close`/`GetView`: *is a static of this kind already registered?*, *is a dynamic of this kind already open on this parent?*, *which view sits below this one in its stack?* None of them scan the whole registry or copy a stack.

- **`ViewKey`** — `(Type, ViewId)` identity of a view kind. Keys the prefab cache, the `ViewPool`, and the registry index.
- **`ViewRegistry`** — every registered view is threaded onto a per-kind singly linked list (`ViewRecord.NextOfKind`, one head per `ViewKey`). Finding a static or a dynamic instance of `UIViewCloseBar` walks the two or three close bars that exist, not the ~50 registered views. `Add`/`Remove` maintain the chain and the parent's `Children`; `CountOfKind` exposes it for diagnostics.
- **`ViewStack`** — list-backed navigation stack (bottom = index 0). `Remove`, `MoveToTop`, `BelowOrNull`, `IsCoveredAbove`, and `PeekBelowTopOrNull` run in place; there is no `ToArray()` or temporary stack anywhere on the show/close path. Enumeration is top-first, same as `Stack<T>`.
- **Prefab cache** — `(prefab, isScreen)` per kind, so the show path never calls `GetComponent<UIViewChannel>()` on the prefab.

Steady-state `GetView<T>()` is allocation-free (asserted by `UISystemTests.GetView_DoesNotAllocate_OnceWarm`). What still allocates per show is inherent to the design: the view instance on a pool miss, one `ViewRecord`, one `UniTaskCompletionSource` per serialized fragment show, and the UniTask async state machines.

### Internals — The System/View Boundary

`UISystem` and `UIView` talk to each other through two interfaces and nothing else — there are no `internal` fields or methods on `UIView`.

- **`IViewLifecycle`** (internal, system → view). `UIView` implements it *explicitly*, so `Attach`, `ShowAsync`, `HideAsync(HideMode)`, `ResumeAsync`, `Pause`, `Resume`, `Conceal`, `Teardown`, `OverrideStackBehaviour`, `ArmDelayedStart` and `AttachStaticChildren` never appear on the type a view author subclasses. The system reaches them through `view.Lifecycle()`. `HideMode.Close` runs the close hooks; `HideMode.Pause` is animation only. `Pause()` is "input off, then `OnPause`"; `Resume()` is "input on, then `OnResume`".
- **`IViewHost`** (public, view → system). Extends `IUISystem` with the five things a view asks of its owner: `IsRegistered`, `RegisterStatic`, `ShowStaticChildren`, `NotifyShown`, `NotifyDestroyedExternally`. A view holds an `IViewHost`, never the concrete system; `UIView.UISystem` returns that same object.
- **Registry-owned state.** Whether an instance is a template clone (`IsClone`, decides pooling) and which channel a screen was shown on (`ChannelOverride`) live on the system's `ViewRecord`, not on the view.

To observe shows from outside, subscribe to `IUISystem.ViewShown` — it fires after any view's `OnShow`, immediate and animated alike, and not on resume.

`UIView` itself is now only the lifecycle: its animation plumbing lives in `ViewAnimator` (owns the lifetime and in-flight cancellation sources; a run reports whether it finished instead of throwing), the dim in `ViewBackgroundOverlay`, and the raise-above-everything behaviour in `ViewHighlight`.

### Internals — How the System Is Put Together

`UISystem` (the MonoBehaviour, ~200 lines) is a composition root: it holds the serialized references, wires the parts below in `EnsureInitialized`, forwards `IUISystem`, and implements `IViewHost`. Everything else is an `internal sealed class` in the same folder, built once and never exposed to game code:

| Part | Owns | Answers |
|------|------|---------|
| `ViewRegistry` | `ViewRecord` per registered view + the per-kind index | *is X registered / static / dynamic / closing?* |
| `ScreenStacks` | one `ViewStack` per `UIChannel` + the UI camera | push/pop/remove with canvas re-sorting, *which screen hosts a parentless fragment?* |
| `FragmentHistories` | one `ViewStack` per parent + the per-parent show gate | *what is under this fragment?*, serialising rapid shows on one parent |
| `StackPolicy` | four predicates over `ViewStackBehaviour` (`Pauses`, `Hides`, `Closes`, `Covers`) | the only place a behaviour's meaning is spelled out; `ViewStack.IsCoveredAbove` uses it too |
| `ViewFactory` | prefab cache, `ViewPool`, the DI container | spawn, clone a template, release, destroy |
| `ShowPipeline` | — | resolve → spawn → register → push → pause what is below → entrance |
| `ClosePipeline` | — | pop → hide → cascade to children → pool/destroy/hide-in-place → resume what was uncovered |

The two pipelines share one pause path and one resume path each — `PauseBelowAsync(above, below, immediate, cascade)` and `ResumeBelowAsync(behaviour, below, immediate, cascade)` — where `cascade` is the single screen/fragment difference (a screen also pauses/resumes the fragments in its history). Both read `StackPolicy`, so a new behaviour is added there, not in a switch. On the close side, `SettleAsync` is the one place a view leaves presentation and `CascadeChildren` the one place its children follow it: dynamic children are pooled or destroyed, static children are torn down and either hidden (parent stays registered) or left to the parent's fate.

`ViewRecord.IsClosing` replaces the old closing set: a view mid-close is skipped by every lookup and a second `Close` is a no-op. The resume after a close uses the stack behaviour the view was *shown* with (captured before settle clears the per-show override), so a fragment shown with `ShowOptions(stackBehaviour: HideBelow)` over a `DoNothing` default still brings the view below back.

**Editor tooling.** *AK ▸ UI ▸ View Stack Visualizer* renders the live state — channel stacks, per-parent histories, the pool, and a consistency check across them. It reads the internal parts directly (`UISystem.Registry/Screens/Histories/Pool`, visible via `InternalsVisibleTo("AK.UISystem.Editor")`); there is no reflection on field names left to break.

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
  IUISystem.ViewShown          ← fired for observers
  [static fragments with ShowOnStart start their own show]

Pause (a higher-priority view covers this one):
  [input blocked]
  OnPause()                    ← pause game logic, timers
  [play hide animation - resources stay registered]

Resume (the covering view is closed):
  [play show animation]
  [input restored]
  OnResume()                   ← resume game logic, timers

Close:
  OnPrepareHide()              ← save state, cleanup visuals
  [play hide animation]
  OnHide()                     ← disable interactions, stop timers
  UnRegisterResources()        ← unsubscribe from events
  [destroy / pool / hide depending on static/dynamic + CloseContext]
```

**Key detail:** `RegisterResources` / `UnRegisterResources` are called exactly once per view lifecycle. On a normal pause/resume cycle, resources stay registered — the view is logically alive, just not visible.

`UIView.State` says where a view is: `Hidden → Showing → Shown → Paused → Hiding → Hidden`. Resources are registered in every state except `Hidden`, and the hooks above fire only on the transitions between states, which is what keeps them paired when things are interrupted:

- A close (or teardown) during an entrance that has not reached `OnShow` releases the resources and skips `OnPrepareHide`/`OnHide` — hide hooks fire only for a view that was actually shown.
- A show during an entrance restarts the animation and re-runs `OnPrepareShow` but does not re-register.
- Anything interrupting an exit first finishes it (`OnHide`, `UnRegisterResources`), then proceeds.
- Teardown runs whatever the current state still owes and is idempotent.

`IsVisible` is separate from `State`: a paused view under `PauseOnlyBelow` is still drawn, under `HideBelow` it is not. Pinned by `ViewStateTests`.

### Typed Context — UIView\<TContext\>

Pass data to views using `UIContext` subclasses:

```csharp
// Define a context
public class RewardPopupContext : UIContext
{
    public Uid<RewardDefinition> RewardId;
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
ShowFragment<RewardPopup>(new RewardPopupContext { RewardId = rewardId, Amount = 100 });
```

### Fragment Navigation — closing the top fragment

Fragments maintain a per-parent history stack. Closing the fragment on top pops it and resumes the one beneath — there is no separate back call:

```csharp
// User navigates: HomePanel → SettingsPanel → SoundPanel
// soundPanel.Close():
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

30+ built-in strategies including Fade, Slide, Scale, Bounce, Elastic, PopSnap, CardDeal, ConfettiBurst, and more, kept as assets so designers can try them out. All use DOTween under the hood. Create your own by extending `AnimationStrategy`:

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

A strategy returns tweens and nothing else — `IAnimationStrategy` is exactly the two methods above. The system awaits them through the `PlayShowAsync`/`PlayHideAsync` extension methods, which link every tween to the animated content (`SetLink(target.gameObject, KillOnDisable)`) so a tween can never finish on a view that was disabled, pooled or destroyed under it, and which turn cancellation into a killed tween plus `OperationCanceledException`. Return `null` for "nothing to animate". A component strategy that drives its own timing (an Animator, a Timeline) implements `IAsyncAnimationStrategy` on top and the system awaits that instead.

### Object Pooling

Dynamic fragments with `ReturnToPoolOnClose = true` are returned to a `ViewPool` instead of destroyed. The pool keys by `(Type, ViewId)`, so you can have multiple variants of the same view type. The close pipeline settles a pooled view completely before the pool sees it: hide hooks, then `OnBeforePool()` while its children are still registered (close dynamic children there with `CloseOptions.Now` if you want them to see a normal close rather than the cascade), then the cascade, then `OnReset()` — whose base implementation resets the animated transform, cancels a pending delayed start and turns input back on. The pool itself only kills leftover tweens, deactivates and reparents; on the next show the view is re-attached and re-initialised like a fresh instance.

### Background Overlay

Add a `ViewBackgroundOverlay` component next to a UIView and the view dims everything behind it: the overlay fades in when the show completes and out when a hide starts. Alpha, raycast blocking and both fade durations are serialized on the component (defaults 0.95 / block / 0.4s / 0.1s). The dim is a runtime-built Image at sibling index 0, oversized to cover the nearest Canvas. Useful for modal-style screens and popups.

### Common Components

| Component | Description |
|---|---|
| **UIFragButton** | Button with Animator + TMP text. Auto-enables Animator on show. |
| **UIFragTooltip** | 8-position auto-placement tooltip with edge clamping and auto-hide timer. |
| **UIFragLoadSpinner** | Loading spinner with `AutoCloseAfterSeconds(int)` for timed auto-close. |
| **UIViewToast** | Floating toast notification. Moves up 120px over 2s then fades out. |
| **UIViewBanner** | Top banner with text, duration timer, and Animator support. |
| **UITutorialArrow** | Oscillating arrow pointing at a target. Auto-rotates toward target. |

### Highlight

`ViewHighlight.Enter(view)` raises a view above everything else and dims the rest — what a tutorial step uses to force attention on one control. It adds the `ViewHighlight` component on demand (a view knows nothing about being highlighted), then:
1. Fades in a `ViewBackgroundOverlay` (reusing the view's own if it has one, otherwise adding a temporary one)
2. Screens: forces the channel Canvas to sort at `Overlay + 1`
3. Fragments (no Canvas of their own): adds a temporary override Canvas + GraphicRaycaster just above the parent Canvas

`ViewHighlight.Exit(view)` fades the dim out and restores sorting once it is gone; `Restore()` does the same immediately. The pool restores a highlighted view on release. (`UIViewSpotlight` is a different thing: the rounded cut-out view that tutorials point at a target.)

### Quick Reference

| I need to... | |
|---|---|
| Create a full-screen UI | Add `UIViewChannel` component to the UIView prefab → it becomes a Screen |
| Create a panel/popup | UIView with NO `UIViewChannel` → it's a Fragment, lives in parent's Canvas |
| Show a screen | `viewSystem.ShowAsync<T>()` or `viewSystem.Show<T>()` |
| Show a fragment | `parentView.ShowFragment<T>()` or `parentView.ShowFragmentAsync<T>()` |
| Pass data to a view | Create a `UIContext` subclass, pass via show method or `UIView<TContext>` |
| Navigate back | `topFragment.Close()` — pops from the parent's history and resumes the one below |
| Make a fragment auto-close when parent closes | It already does — child fragments auto-cleanup on parent close |
| Pre-place a fragment that survives close | Add it to parent's `_staticViews` list — statics are hidden, not destroyed |
| Pool a frequently-spawned fragment | Set `_returnToPoolOnClose = true` on the UIView |
| Allow multiple instances of same fragment | Set `_allowMultipleInstances = true` |
| Add a dark overlay behind a screen | Add a `ViewBackgroundOverlay` component to the UIView |
| Highlight one view for a tutorial | `ViewHighlight.Enter(view)` … `ViewHighlight.Exit(view)` |
| Create a custom animation | Extend `AnimationStrategy`, override `PlayShowAnimation` and `PlayHideAnimation` |
| See what is on the stacks right now | *AK ▸ UI ▸ View Stack Visualizer* (editor window, live in Play Mode) |

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
    public Uid<LevelDefinition> LevelId;
    public bool IsRestart;
}

// Typed state — access context with no casting
public class GameplayState : AppState<LevelLoadContext>
{
    protected override void OnEnter()
    {
        var levelId = _context.LevelId;     // strongly typed
        var isRestart = _context.IsRestart;
    }
}

// Pass context when transitioning
AppStateMachine.ChangeState(_gameplayState, context: new LevelLoadContext { LevelId = levelId, IsRestart = false });
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

A registry-driven, pooled audio system. Create `AudioConfig` ScriptableObjects for each sound type, register them in an `AudioRegistry`, and the `AudioSpawner` handles pooling, playback, and cleanup.

```csharp
// Primary API — play a held AudioConfig (covers 99% of use cases)
audioSpawner.PlayAudio(audioIds.CoinPickup);
audioSpawner.PlayAudio(audioIds.Explosion, position: hitPoint);  // 3D spatial

// Or by identity, resolved through the registry
audioSpawner.PlayAudio(coinPickupId);   // Uid<AudioConfig>

// Type-safe spawn — returns AudioComponent without auto-playing
var audio = audioSpawner.Spawn<MusicAudioComponent>(variantId);
audio.Play();
```

#### AudioConfig — Sound Configuration

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
var firework = particleSpawner.Spawn<FireworkParticle>(variantId);   // Uid<ParticleConfigBase>

// Or from a held config
var confetti = particleSpawner.Spawn(particleIds.Confetti);
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

> Full guide — setup, field-type decisions, redirects, namespaces, tooling, FAQ: [`Runtime/Core/UID/UID-GUIDE.md`](Runtime/Core/UID/UID-GUIDE.md).

Identity for game content, split into two things that are easy to confuse and must not be:

| Thing | What it is | Where it lives |
|---|---|---|
| `Uid` | A 16-byte **value** (two `ulong`s). Cannot be null, cannot dangle, means the same thing on disk, on a server, in a save file, and in a dictionary key. Serializes as one 32-hex string field. | Everywhere identity is stored, compared, hashed, or transmitted. |
| `UID` (asset) | A ScriptableObject that **carries** one `Uid`. It is the thing a designer drags into a field. Immutable identity, assigned once at creation by the editor and never changed. Reference equality only. | Only in the asset database. Never in persisted state, never on the wire. |

```csharp
// Asset -> identity (one direction, explicit)
Uid id = factType.Id;
Uid<FactType> typed = factType.IdAs<FactType>();

// Identity -> asset (the other direction, through a registry or the repository)
if (registry.TryResolve(typed, out FactType asset)) { ... }
if (metaDataRepository.TryResolve(id, out UID any)) { ... }

// There are NO implicit conversions to or from string or UID. Every crossing is visible.
```

**`Uid<T>`** is a phantom-typed `Uid` — same 16 bytes, but `Uid<AudioConfig>` cannot be passed where `Uid<FactType>` is expected, and the inspector filters its picker to `T` assets with no attribute. Use untyped `Uid` only where identities are genuinely polymorphic (aggregate resolvers, redirect tables, wire formats). For an untyped field that should still get a filtered picker, use `[UidOf(typeof(MetaDataAsset))]`.

**Minting.** `Uid.NewRandom()` (UUIDv4) for ordinary content. `Uid.Deterministic(namespace, name)` (UUIDv5-style) for closed vocabularies that must agree across projects and servers — create a `UidNamespace` asset in a folder and every UID asset created under it derives its identity from the folder namespace and its file name. `UidProvenance` records which path produced the identity (Minted / Imported / Derived) and governs what tooling may do to it.

**Registries.** `UidRegistryAsset<T>` is one asset per domain that maps `Uid -> T`. Lookups are a single hash probe; `UidHandle<T>` turns a hot-path lookup into one array index. Registries are kept in sync by the editor automatically — an asset that appears is tracked, one that disappears is untracked. Nothing calls `Initialize()`; the lookup builds lazily and rebuilds on mutation. A miss returns `false` and never logs: the caller knows what a missing definition means.

**Persistence.** Ledgers (`FactService`, `TransactionService`, `CurrencyModel`) store `Uid` values, never asset references or names. When content is deliberately replaced, the old identity lives on in saves; a human records `old -> new` in a `UidRedirectTable` and every resolver follows it on a miss. Ledgers apply redirects once at load and rewrite the file. An identity that no longer resolves is reported as an orphan (`FindOrphans`), never healed by name.

**Analytics and external systems.** The `Uid` is internal. What goes on the wire is the definition's external key (`EventID`, `ProductID`, `VariableKey`). An unresolvable identity is dropped with a one-time warning, never sent as hex.

**Editor tooling** (`Tools -> UGFW -> UID`):

| Tool | What it does |
|---|---|
| Identity authority | Mints on asset creation (before first save) and on import of identity-less assets. A Ctrl+D duplicate is re-minted immediately. Two imported assets sharing a Uid are a **collision**: recorded, surfaced, and blocked from building until a human picks the survivor. Imported identities are never re-minted. |
| Audit Project | One pass over identities, registries, redirects, Addressables groups, persisted types, and external keys. Errors block the build (`UidBuildValidator`). CI: `-executeMethod AK.Core.Editor.UidAuditMenu.AuditForCI`. |
| Resolve Collisions | Choose which asset keeps a shared identity; the others receive fresh ones. |
| Inspect Identity | Paste a hex from a save/log/server row, see the owning asset (following redirects). |
| Export Catalog | `uid-catalog.json` — every identity with type, path, provenance, external key, plus all redirects. The contract sibling projects and servers consume. |
| Registries / Refresh All From Project | Full resync when you do not trust the auto-tracker. |

**Rules the tooling enforces:**
- Every UID asset has a non-empty identity.
- Every identity is owned by exactly one asset.
- No UID asset lives in an Addressables group (bundle duplication would create two live instances of one identity).
- No `PersistableState` field holds a `UID` asset reference — store `Uid`/`Uid<T>`.
- Redirect chains terminate at an identity a live asset owns.

**The MetaData identity chain:**
```
UID (ScriptableObject carrying one Uid)
  -> MetaDataAsset (UID + Name, DisplayName, Description, Icon)
    -> CurrencyDefinition, RewardDefinition, etc. (game-specific definitions)
```

### SlotMap & Handles

`AK.Core.Collections.SlotMap<T>` is generational slot storage: `Add` returns a `Handle<T>` (`{Index, Generation}`, 8 bytes, value-equal), `Remove` frees the slot and bumps its generation, and any handle from the previous lifetime stops resolving. Add/Remove/TryGet are O(1) and allocate nothing at steady state; `foreach` uses a struct enumerator and tolerates removal mid-walk.

Use it wherever code keeps a reference to something that can be recycled underneath it — pooled objects, active tweens, timers, VFX bookkeeping, AI targets. A stale `Handle<T>` answers `false`; a stale C# reference points at whoever got the slot next.

```csharp
var map = new SlotMap<Enemy>();
Handle<Enemy> h = map.Add(enemy);

if (map.TryGet(h, out Enemy e)) e.TakeDamage(5);   // false once removed, even if the slot was reused

foreach (Enemy alive in map) alive.Tick();          // zero-alloc; skips dead slots

var walker = map.GetEnumerator();
while (walker.MoveNext())
    if (walker.Current.IsDead) map.Remove(walker.CurrentHandle);
```

### Object Pooling (GameObjects)

`ObjectPoolService` (`IObjectPoolService`) pools prefab instances per `PoolableObjectDefinition` (a `UID` asset: prefab, initial size, max size, prewarm flag), registered through an `ObjectPoolRegistry`. Every checked-out instance lives in one `SlotMap<PooledInstance>`.

Two APIs over the same pool:

```csharp
// Reference API — you own a GameObject; do not touch it after release.
Bullet b = _pools.Get<Bullet>(bulletDef, muzzle.position, muzzle.rotation);
b.ReturnToPool();                        // or _pools.Release(b.gameObject)

// Lease API — for anything that outlives a frame. A stale lease resolves to nothing.
Handle<PooledInstance> lease = _pools.Lease(bulletDef, muzzle.position, muzzle.rotation);
if (_pools.TryGet(lease, out Bullet bullet)) bullet.Fire();
_pools.Release(lease);                   // false (no log) if already released
```

Get/Lease/Release are O(1) and allocate nothing once a pool has reached its working size; `IPoolable` components are captured once per instance. Double release by reference logs a warning; double release by lease is a silent `false`.

### Result & ErrorCode

Expected failures are values, not exceptions and not `null`. `Result` (no payload) and `Result<T>` are `readonly struct`s carrying an `ErrorCode` and an optional `Detail` string that is `null` on the happy path — so success allocates nothing. Bugs still throw; only outcomes the caller is expected to handle travel as `Result`.

```csharp
Result<Transaction> recorded = _transactions.Record(type, amount);
if (recorded.IsFailed) return recorded.Untyped;         // propagate the code, drop the payload
Transaction tx = recorded.Value;

Result granted = _rewards.Grant(reward);
if (!granted) Debug.LogWarning($"reward skipped: {granted}");   // implicit bool; ToString prints Fail(Code: Detail)

// Inside a method returning Result<T>:
return value;                          // implicit Ok
return Result.Fail(ErrorCode.NotFound); // implicit failure conversion
```

`ErrorCode` is grouped in blocks of 100 by domain (1xx argument/state, 2xx providers, 3xx cost, 4xx reward, 5xx transaction, 6xx store/IAP, 9xx internal). Games add their own codes from `ErrorCode.GameDefined` (10000) upward via `Result.Fail(int, string)`. UI switches on `Code`; nothing parses `Detail`.

Services returning `Result`: `ICostService.CanAfford/Deduct`, `IRewardService.Grant`, `ITransactionService.Record/RecordPending/CreditAsync/Reverse`, `IPurchaseService.Purchase`.

### Scope Guards (`ref struct` + `using`)

Three guards give RAII-style "runs on every exit path" semantics. Each is a `ref struct`, so the compiler forbids storing it in a field, capturing it in a lambda, boxing it, or holding it across an `await` — the scope *is* the stack frame.

```csharp
// One disk flush instead of one per Record. Counts and Changed events still fire immediately.
using (_facts.BeginBatch())
{
    _facts.Record(districtButtonPressed);
    _facts.Record(districtEntered);
}

// Scratch list from a pool; returned cleared when the scope ends.
using PooledList<IReward> rewards = ListPool<IReward>.Rent();
item.CollectRewards(rewards.List);
for (int i = 0; i < rewards.Count; i++) _rewards.Grant(rewards[i]);

// Profiler region; compiles to nothing without ENABLE_PROFILER.
using (ProfilerScope.Begin(Markers.ShopRefresh))
{
    RebuildShopList();
}
```

`LedgerBatch` is exposed by `IFactService.BeginBatch()` and `ITransactionService.BeginBatch()`; scopes nest and flush once at the outermost dispose. Both services persist through `PersistableState.Commit`, which serializes to JSON and calls `PlayerPrefs.Save()` — a disk flush — so batching matters anywhere more than one record happens in a frame.

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

### CameraSystem

Registry-driven multi-camera management with URP camera stacking. Cameras can be **pre-placed** in the scene or **dynamically spawned** from the registry at runtime.

#### Core Concepts

| Concept | Description |
|---------|-------------|
| **CameraType** | Identity asset (extends UID) that identifies a camera kind (e.g., "Main", "UI", "Effects"). Acts as the shared identity bridge between definitions and runtime instances. |
| **CameraDefinition** | MetaDataAsset defining a camera's prefab, CameraType, role, layer order, and base camera reference. The single source of truth for spawned cameras. |
| **CameraRegistry** | `UidRegistryAsset<CameraDefinition>` — lookup definitions by CameraType identity. Assigned to `CameraSystem` via Inspector. |
| **CameraRole** | `Base` (renders to screen) or `Overlay` (stacks on top of a Base camera's URP output). |
| **BaseCamera** | Abstract `StateEntity` implementing `IGameCamera`. All cameras extend this. |
| **CinemachineBaseCamera** | Adds Cinemachine integration with impulse-based shake. |

#### Pre-Placed Cameras

Pre-placed cameras live in the scene hierarchy and configure themselves via their own serialized fields. They auto-bind to `ICameraSystem` via Reflex `[Inject]` in `Start()`:

```csharp
// Pre-placed in scene — auto-binds when Start() fires
// _cameraSystem is injected by Reflex DI
[Inject] private ICameraSystem _cameraSystem;

protected virtual void Start()
{
    if (_cameraSystem != null && !_isBound)
    {
        _cameraSystem.BindCamera(this);
        _isBound = true;
    }
}
```

Pre-placed cameras use their own Inspector fields for `_cameraType`, `_role`, `_layerOrder`, and `_baseCameraType`. They do NOT need a `CameraDefinition` — their serialized fields are the configuration.

#### Dynamically Spawned Cameras

Cameras spawned at runtime via `CameraSystem.SpawnCamera<T>()` are created from `CameraDefinition` assets registered in the `CameraRegistry`. The definition is the single source of truth — `ApplyDefinition()` copies all config from the definition to the spawned instance:

```csharp
// Spawn a camera from the registry
var uiCamera = cameraSystem.SpawnCamera<UICamera>(uiCameraType);

// SpawnCamera flow:
// 1. Look up CameraDefinition by CameraType in CameraRegistry
// 2. Instantiate the prefab as a child of CameraSystem
// 3. ApplyDefinition() — copies CameraType, Role, LayerOrder, BaseCameraType from definition
// 4. BindToSystem() — registers with CameraSystem, sets _isBound flag
//    (prevents double-bind when the spawned camera's Start() fires)
```

Cameras marked `SpawnOnStart` in their definition are automatically spawned during `CameraSystem.Start()`.

#### URP Camera Stacking

Overlay cameras are automatically added to their Base camera's URP camera stack. The binding system handles out-of-order registration — if an Overlay camera is bound before its Base camera, the overlay is deferred until the base registers:

```
Base Camera (CameraType: "Main")
  └─ URP Camera Stack:
       ├─ Overlay Camera (CameraType: "UI", LayerOrder: 10)
       └─ Overlay Camera (CameraType: "Effects", LayerOrder: 20)
```

The `CameraType` SO acts as the shared identity bridge — both the Overlay camera's `_baseCameraType` field and the Base camera's `_cameraType` field reference the same CameraType asset, so they match by identity.

#### API Reference

```csharp
// --- Get cameras ---
var mainCam = cameraSystem.Get<MainCamera>();           // by type
var uiCam = cameraSystem.GetCamera(uiCameraType);       // by CameraType asset (or Uid<CameraType>)

// --- Enable / Disable ---
cameraSystem.EnableCamera<MainMenuCamera>();
cameraSystem.DisableCamera<GameplayCamera>();
cameraSystem.EnableCamera(uiCameraType);                // by asset or identity
cameraSystem.DisableCamera(effectsCameraType);

// --- Spawn / Remove ---
var cam = cameraSystem.SpawnCamera<UICamera>(uiCameraType);  // spawn from registry
cameraSystem.RemoveCamera(uiCameraType, destroy: true);       // remove and destroy

// --- Camera shake ---
mainCam.Shake(intensity: 0.5f, duration: 0.3f);

// --- Manual binding (rare — only for non-Reflex contexts) ---
baseCam.BindToSystem(cameraSystem);

// --- Reorder stacks ---
cameraSystem.ReorderCameraStack();
```

#### Setup

1. Create `CameraType` ScriptableObjects for each camera kind (Create → AK → Camera → Camera Type)
2. Create `CameraDefinition` ScriptableObjects linking CameraType + prefab + config (Create → AK → Camera → Camera Definition)
3. Create a `CameraRegistry` and add all definitions (Create → AK → Camera → Camera Registry)
4. Add `CameraSystem` MonoBehaviour to your scene, assign the `CameraRegistry` in Inspector
5. For pre-placed cameras: add `BaseCamera` components to GameObjects in the scene, assign their `_cameraType` and `_baseCameraType` fields in Inspector
6. For dynamic cameras: set `SpawnOnStart = true` on definitions that should auto-spawn, or call `SpawnCamera<T>()` at runtime


## Module: Jobs

Assembly `AK.Jobs` (namespace `AK.Jobs`, not auto-referenced — add it to your asmdef). A lock-free, frame-synchronous job system for per-frame managed work that has no business on the main thread: simulation ticks, steering, influence maps, procedural generation, scoring. It schedules **where** work runs; **when** stays with UniTask (`await UniTask.Delay(...)` then schedule).

### The frame contract

```
frame N      Schedule(job) / batch.Add()        → appended to main-owned pending buffers
top of N+1   barrier (EarlyUpdate, before any Update)
             workers idle? → deliver N-1's completions, swap pending ⇄ executing, kick workers
             workers busy? → skip: nothing swapped, pending keeps accumulating, one frame of latency
frame N+1    workers run the frozen executing set in parallel with the whole main-thread frame
top of N+2   completions on the main thread; batch Results readable for the whole frame
```

Nothing is locked. Every buffer has one owner at a time and ownership changes only at the barrier, when every worker is provably parked; the two barrier events are the memory fences. Workers read the frozen buffers and write only their own job data, so there is no line for two cores to fight over. The only atomics are the chunk cursor and two counters.

### Setup

```csharp
// GameBindings
var scheduler = new JobScheduler(new JobSchedulerOptions { WorkerCount = 2 });
scheduler.AttachToPlayerLoop();                      // ticks at the start of EarlyUpdate
builder.AddSingleton(scheduler, typeof(IJobScheduler));   // dispose with the container
```

`WorkerCount` defaults to `clamp(cores - 2, 1, 4)`. Unity already runs a render thread and its own job workers, so more managed workers oversubscribe a phone; measure on device before raising it.

### Batches — the fast path

Homogeneous struct jobs stored contiguously. The element's fields are the inputs and the outputs, so a worker streams through the array with a direct call per element and no object to chase.

```csharp
struct SteerJob : IJob
{
    public Vector3 Position, Target;   // in
    public Vector3 Velocity;           // out
    public void Execute(in FrameContext ctx) => Velocity = (Target - Position).normalized * ctx.DeltaTime;
}

var batch = new JobBatch<SteerJob>(capacity: 512);
scheduler.Register(batch);

// every frame
foreach (var agent in agents) { ref SteerJob j = ref batch.Add(); j.Position = agent.Position; j.Target = agent.Target; }
foreach (ref readonly SteerJob r in batch.Results) Apply(r);   // last finished frame; stays until a newer frame with elements completes
```

`Results` becomes readable two frames after `Add` (one frame to hand off, one to run). The three buffers rotate at the barrier — fill, execute, read — so reading last frame's results and filling this frame's inputs never touch the same array.

A per-frame producer should add only when `batch.PendingCount == 0`. If the workers overran and the barrier skipped a frame, last frame's elements are still pending; adding another frame's worth on top just makes the next hand-off twice as long, and the overrun feeds itself. Skipping the add re-simulates from the latest `Results` next frame, which is what a simulation wants anyway. `Stats.SkippedFrames` tells you it is happening; `Stats.LastFrameCriticalTicks` tells you by how much.

### Reference jobs

```csharp
sealed class BakeJob : IJob, IJobCallback
{
    public void Execute(in FrameContext ctx) { /* worker: pure C# */ }
    public void OnComplete(bool cancelled)   { /* main thread, exactly once */ }
}

JobHandle h = scheduler.Schedule(new BakeJob());          // runs next frame
scheduler.Cancel(h);                                       // before hand-off: never runs; after: may run once, OnComplete(true)
scheduler.ScheduleRepeating(job);                          // every frame until cancelled
scheduler.Schedule(ctx => Work(), done => Refresh());      // delegate form; allocates a wrapper, not for per-frame use
```

`JobHandle` is an 8-byte generational value: a handle whose job completed is rejected by every call instead of aliasing a later job in the same slot. Completions are delivered in schedule order regardless of how many workers ran them.

### Phases

`JobSchedulerOptions.PhaseCount = 2` splits each frame into ordered phases: every chunk of phase 0 finishes before any chunk of phase 1 starts, so a phase-1 job may read what phase 0 wrote. Pass `phase:` to `Schedule`, `ScheduleRepeating`, or the `JobBatch` constructor. This covers integrate → resolve → post-process pipelines without a dependency graph.

### Rules for job code

1. **No Unity API** — engine objects are main-thread-only. Read time from `FrameContext`, not `Time`.
2. **No `ListPool<T>`** or any other main-thread pool. In the editor `ListPool.Rent` logs an error if a worker calls it.
3. **Own your scratch memory** — allocate it once on the job object or in the batch element and reuse it.

A job that throws is logged, counted in `Stats.LastFrameFaults` / `batch.ResultFaults`, and never stops the other jobs or the worker. `Stats` also reports the slowest worker's time per frame (`LastFrameCriticalTicks`) so a game can tune `WorkerCount` at runtime.

### What it deliberately is not

No timers or delays (UniTask), no main-thread scheduling (UniTask), no dependency graph (phases), no work stealing (the executing set is frozen for the frame, so a chunk cursor is enough), no `unsafe`. Tests: `UGFW/Tests/EditMode/Jobs` drive the barrier by hand; `UGFW/Tests/PlayMode/Jobs` (assembly `AK.Tests.PlayMode`) check the real player loop.


## Module: CoreDomain

Game data layer built around the **MetaData system** -- a ScriptableObject-driven architecture for defining all game content as data assets.

### The MetaData Pattern

Every game domain follows a consistent four-part pattern:

```
[Domain]Meta          -- ScriptableObject container (e.g., CurrencyMeta, RewardsMeta)
  -> [Domain]Registry    -- UidRegistryAsset<Definition> for identity lookup
  -> [Domain]Definition  -- The actual data asset (extends MetaDataAsset extends UID)
  -> [Domain]Type        -- ScriptableObject categorizing the domain (e.g., CurrencyType, RewardType)
```

**`[Domain]Type` assets are ScriptableObjects, not enums.** This is critical for making UGFW universal — each game creates its own type assets (e.g., "SoftCurrency", "HardCurrency") instead of being locked to hardcoded enum values. The framework provides the base `MetaDataAsset` class; games create instances via the Create Asset menu.

**Example: The Currency domain**

```
CurrencyMeta (ScriptableObject)
  -> CurrencyRegistry (UidRegistryAsset<CurrencyDefinition>)
  -> CurrencyDefinition : MetaDataAsset   // fields: Type (CurrencyType SO), MaxAmount, StartingAmount, etc.
  -> CurrencyType : MetaDataAsset         // Create instances: "SoftCurrency", "HardCurrency", "Energy", etc.
```

### The MetaDataRepository

`MetaDataRepository` is a single ScriptableObject that provides a **type-keyed registry** for all domain metas and acts as the aggregate `IUidResolver` over every registered registry (plus the optional `UidRedirectTable`). There are no hardcoded convenience properties — every domain is accessed uniformly through `GetMeta<T>()`:

```csharp
public class MetaDataRepository : ScriptableObject, IMetaDataRepository
{
    public UidRedirectTable Redirects { get; }

    // IUidResolver — resolves any identity across every registered registry
    public bool TryResolve(Uid id, out UID asset);
    public bool TryResolve<T>(Uid<T> id, out T asset) where T : UID;

    // Type-keyed registry — extensible without modifying framework code
    public void RegisterMeta<T>(T meta) where T : class, IMeta;
    public T    GetMeta<T>() where T : class, IMeta;
    public bool TryGetMeta<T>(out T meta) where T : class, IMeta;
}
```

**All domains** — whether framework-core or game-specific — are registered during bootstrap in `GameBindings`:

```csharp
public void InstallBindings(ContainerBuilder builder)
{
    // Register all Meta domains (framework-core and game-specific)
    if (_adsMeta != null) _metaDataRepository.RegisterMeta(_adsMeta);
    if (_analyticsMeta != null) _metaDataRepository.RegisterMeta(_analyticsMeta);
    if (_currencyMeta != null) _metaDataRepository.RegisterMeta(_currencyMeta);
    if (_rewardsMeta != null) _metaDataRepository.RegisterMeta(_rewardsMeta);
    if (_shopMeta != null) _metaDataRepository.RegisterMeta(_shopMeta);

    // Build the aggregate resolver BEFORE anything loads persisted identities
    _metaDataRepository.InitializeRegistries();

    // Then register the repository in DI
    builder.RegisterValue(_metaDataRepository, new[] { typeof(MetaDataRepository), typeof(IMetaDataRepository), typeof(IUidResolver) });
}
```

**Accessing Meta containers at runtime:**

```csharp
// All domains use the same type-keyed lookup
var rewardsMeta = repository.GetMeta<RewardsMeta>();
var currencyMeta = repository.GetMeta<CurrencyMeta>();
var adsMeta = repository.GetMeta<AdsMeta>();
var shopMeta = repository.GetMeta<ShopMeta>();

// Safe access with TryGetMeta
if (repository.TryGetMeta<RewardsMeta>(out var rewards))
{
    rewards.TryGetReward(rewardId, out var rewardDef);   // Uid<RewardDefinition>
}
```

**The `IMeta` interface** — all Meta containers implement `IMeta` (defined in `AK.Core`). `MetaDataAsset` already implements `IMeta`, so any Meta class extending `MetaDataAsset` is automatically compatible. A Meta that owns a registry implements `IMetaWithRegistry` and exposes it through `RegistryAsset`; the repository registers it automatically in `InitializeRegistries()`.

Place ONE `MetaDataRepository` in your bootstrap scene. It's registered in DI and injected everywhere.

### Framework-Core Domains (in CoreDomain)

These domains ship with the framework because their services directly depend on them:

#### Ads

- `AdPlacementDefinition` -- defines an ad placement with: placement ID, ad unit ID, ad type (Rewarded/Interstitial/Banner/AppOpen/RewardedInterstitial), frequency caps (MaxPerSession, MaxPerDay, CooldownSeconds), level gating, loading strategies (PreloadOnInitialize, AutoReloadAfterShow, AutoReloadOnFail, MaxRetryAttempts, GetRetryDelay())
- `AdType` enum
- `AdLoadingStrategy` enum
- `AdsMeta` / `AdsRegistry` -- container and registry

#### Analytics

- `AnalyticsEventDefinition` -- typed event definitions with category, provider name mappings, and parameter validation
- `AnalyticsParameter` -- typed parameter definitions with name mapping for providers
- `AnalyticsEventCategory` -- categorization of events (Core, Gameplay, Monetization, etc.)
- `AnalyticsMeta` / `AnalyticsRegistry` -- container and registry

#### Notifications

- `NotificationDefinition` -- local notification template with title, body, delay, repeat interval
- `NotificationType` -- ScriptableObject asset for categorizing notifications
- `NotificationsMeta` / `NotificationsRegistry` -- container and registry

#### Remote Config

- `RemoteVariable<T>` -- three-tier value resolution: Remote > Cached > Default
- `RemoteBool`, `RemoteInt`, `RemoteFloat`, `RemoteString`, `RemoteLong`, `RemoteDouble`, `RemoteJson` -- typed remote variables
- `RemoteConfigMeta` / `RemoteVariablesRegistry` -- container and registry

### Game-Specific Domains (in Examples/MetaData/)

These domains are **not part of the framework core** — they are example implementations in `Assets/UGFW/Examples/MetaData/`. Each game creates its own domain definitions following the same MetaData pattern. Use these as reference implementations:

#### Currency (Example)

- `CurrencyDefinition` -- type (CurrencyType SO), max amount, starting amount, exchange rates
- `CurrencyType` -- ScriptableObject asset. Create instances for each currency in your game (e.g., "SoftCurrency", "HardCurrency", "Energy")
- `CurrencyMeta` / `CurrencyRegistry` -- container and registry

#### Rewards (Example)

- `RewardDefinition` -- amount, type (RewardType SO), linked currency/bundle/gacha data
- `RewardType` -- ScriptableObject asset. Create instances for each reward type in your game (e.g., "Star", "Currency", "Bundle", "Gacha")
- `RewardBundle` -- ordered/weighted list of sub-rewards (recursive), bundle types: Sequential, Random, Weighted, RandomWeighted, All
- `GachaBundle` -- weighted random reward pool with `EvaluateRewards()` for gacha pulls
- `CheckpointReward` -- rewards tied to progression milestones
- `SubscriptionReward` -- time-based rewards for subscribers
- `RewardsMeta` / `RewardsRegistry` -- container and registry

#### Costs & Purchasing (Example)

- `CostType` -- ScriptableObject asset. Create instances for each cost type in your game (e.g., "SoftCurrency", "HardCurrency", "Ad")
- `CostOption` -- links a CostType to an amount. Used by `PurchasableItemDefinition`
- `CostProvider` -- abstract SO that handles `CanAfford`/`Deduct` for a specific CostType. Games create their own (e.g., `SoftCurrencyCostProvider`)
- `CostMeta` -- container for cost definitions

#### Store / IAP (Example)

- `IAPProductDefinition` -- store product ID, product type (Consumable/NonConsumable/Subscription)
- `IAPProductType` enum
- `ShopCategoryDefinition` -- categories of shop items with cost type and product identities (`List<Uid<ShopItemDefinition>>`)
- `ShopItemDefinition` -- individual shop items with rarity, cost, rewards
- `ShopMeta` / `ShopRegistry` -- container and registry

#### Other Example Domains

| Domain | Definitions | Description |
|--------|------------|-------------|
| **Achievements** | AchievementDefinition, AchievementType | Achievement definitions |
| **DailyChallenges** | DailyChallengeDefinition, ChallengeType | Daily challenge definitions |
| **Difficulty** | DifficultyDefinition, DifficultyType | Difficulty settings |
| **GameModes** | GameModeDefinition, GameModeType | Game mode definitions |
| **Seasons** | EventDefinition, EventType | Seasonal/live event definitions |
| **Tutorials** | TutorialDefinition, TutorialType | Tutorial step definitions |

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

    public CurrencyModel GetCurrencyModel(CurrencyDefinition def) { /* lookup by identity */ }
    public override void OnInitialized(bool isFirstLaunch) { /* re-resolve identities; quarantine orphans */ }
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

Game-specific building blocks (CurrencyModel, GameSettingsModel, Transaction, TransactionType, SerializableCurrency) are provided as examples in `Assets/UGFW/Examples/`.

#### Extending Definitions for Your Game

All `[Domain]Type` assets are ScriptableObjects. To add new types for your game:

1. **Create the Type asset** via the Create Asset menu (e.g., Create → Gameplay/MetaData/Currency/CurrencyType)
2. **Name it** for your game (e.g., "Gems", "Energy", "PremiumToken")
3. **Reference it** from your Definition assets (e.g., set `CurrencyDefinition.Type` to your new CurrencyType)
4. **Create a Provider** if the domain uses provider-based dispatch (Cost, Reward). See `Assets/UGFW/Examples/` for reference implementations:

```csharp
// Example: Creating a cost provider for a new currency (see SoftCurrencyCostProvider in Examples)
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
var adsMeta = metaDataRepository.GetMeta<AdsMeta>();
await adsService.InitializeAsync(adsMeta, playerLevel: 5);

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
**Builder:** `AdServiceBuilder` for fluent construction

### Analytics Service

Multi-provider analytics facade with metadata-driven event definitions.

```csharp
// Track raw events
analyticsService.TrackEvent("level_complete", new Dictionary<string, object> { { "level", 5 } });

// Track metadata-driven events (validates parameters, maps provider names)
analyticsService.TrackEvent(analyticsMeta.Ids.LevelComplete, parameters);   // AnalyticsEventDefinition or Uid<AnalyticsEventDefinition>

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
Result purchase = await purchaseService.Purchase(purchasableItemDefinition, immediateCredit: true);
switch (purchase.Code)
{
    case ErrorCode.None:          ShowSuccess(); break;
    case ErrorCode.CannotAfford:  ShowNotEnoughCurrency(); break;
    case ErrorCode.Cancelled:     break;                       // user backed out of the store sheet
    default:                      ShowStoreError(purchase.Code); break;
}

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
var remoteConfigMeta = metaDataRepository.GetMeta<RemoteConfigMeta>();
await remoteConfigService.InitializeAsync(remoteConfigMeta);
await remoteConfigService.FetchAndActivateAsync();
// RemoteVariables on the meta now reflect server values
```

### Notification Service

Local push notifications scheduled from MetaData definitions (by asset or `Uid<NotificationDefinition>`).

```csharp
// Request permission
notificationService.RequestPermission(status => { /* ... */ });

// Schedule from MetaData definition
notificationService.ScheduleNotification(notificationsMeta.Welcome, delaySeconds: 86400);

// Schedule custom
notificationService.ScheduleNotification("Title", "Message", fireTime, "id", data, repeatInterval);
```

---

## Module: Editor

| Tool | Menu Path | Description |
|------|-----------|-------------|
| **Define Symbols Window** | Tools > UGFW > Define Symbols | Toggle preprocessor symbols for optional SDKs |
| **View Stack Visualizer** | AK > UI > V2 - View Stack Visualizer | Inspect live UI channel/fragment stacks, validate consistency |
| **UID tooling** | `Tools -> UGFW -> UID` | Identity authority, audit + build gate, collision resolver, identity inspector, catalog export (see UID System) |
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
