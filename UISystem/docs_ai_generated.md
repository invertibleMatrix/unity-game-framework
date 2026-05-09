#### 1. Canvas Ownership → `UIChannel` Component

```

- UIView has [RequireComponent(typeof(CanvasGroup))] ← always
- IF you add a UIChannel component → it also requires Canvas
    IF you don't add UIChannel → no Canvas, it's a "fragment"
```

The `UIChannel` MonoBehaviour carries the sort order and render mode — exactly what `UIClassification` SO did in V1, but as a component:

```csharp
// V1: External ScriptableObject on UIScreen
UIClassification SO → { channel enum, stackBehaviour, renderMode, orderInLayer, animConfig }

// Just a component on the same GameObject
UIChannel component → { sortOrder, renderMode }
// Everything else (stackBehaviour, animConfig) lives directly on UIView
```

#### 2. Two Separate Systems → One `UIViewSystem`

```
    UIViewSystem manages everything
    Channel views (have UIChannel) → _channelStacks[sortOrder int]
    Child views (no UIChannel) → _historyStacks[parentView]
    Decision is automatic: view.HasChannel ? channelStack : historyStack
```

#### 3. Two Stack Enums → One `ViewStackBehaviour`

```
    ViewStackBehaviour → DoNothing, HideBelow, PauseOnlyBelow, PauseAndHideBelow, CloseBelow
    (superset of both V1 enums, works for any UIView regardless of channel)
```

#### 4. Two Repository Lists → One List

```
    UIViewRepository has List<UIView> _views  (everything)
```

#### 5. Two Parent Reference Patterns → One

```
    UIView._parentView → UIView (can be any view — channel or child)
    Any UIView can ShowChildView<T>() 
    Channel views have _parentView = null (they're top-level)
```

#### 6. Pooling → Universal

```
    Any UIView has _returnToPoolOnClose (ViewPool)
    Channel views can be pooled too if you want
```

#### 7. The "Which Screen Do I Spawn In?" Problem → `FindBestParentView()`

```
    UIViewSystem.FindBestParentView():
    Same logic — finds topmost channel-view across all stacks
    Used when Show<T>() is called without explicit parent
```

---

### Concrete Side-by-Side: Creating a "Screen" vs a "Fragment"

** Same base class, same API, component decides behavior:**
```csharp
// "Screen" — add UIChannel component on the prefab's GameObject
public class MainMenuView : UIView { ... }
// Prefab has: UIView + UIChannel + Canvas + CanvasGroup

// "Fragment" — don't add UIChannel, that's it
public class SettingsView : UIView { ... }  
// Prefab has: UIView + CanvasGroup (no Canvas, no UIChannel)

// Both spawned the same way:
viewSystem.Show<MainMenuView>()     // detects UIChannel → channel stack
viewSystem.Show<SettingsView>()     // no UIChannel → child of best parent
// OR from a parent:
parentView.ShowChildView<SettingsView>()
```

**The type system no longer encodes the distinction.** It's a runtime concern based on whether the `UIChannel` component is present on the prefab.

---

### What This Means In Practice

1. **You can promote a "fragment" to a "screen"** by just adding `UIChannel` to its prefab — no code changes, no re-parenting in class hierarchy.

2. **A "screen" can be a child of another "screen"** — nested channel views with independent sorting. V1 couldn't do this.

3. **Any view can host children** — in V1, only `UIScreen` could host `UIFragment`. In V2, a child view can host its own children (`ShowChildView<T>()`).

4. **One API to learn** — `Show<T>()`, `Close()`, `GoBack()`. No more "is this a screen or fragment?" decision when writing calling code.

5. **DefaultChannel (sort order 0)** always exists as fallback — even if you never explicitly add `UIChannel` to anything, the system has a root to attach orphan views to.


## Dry-Run Verification: All 15 Scenarios After Fixes

### ✅ Scenario 1: Rapid double-tap open (same view, non-multi-instance)
**Before**: `OnPrepareHide`/`OnHide` ran without `OnShow` ever completing.
**After**: When show animation is cancelled, the catch block calls `UnRegisterResourcesSafe()`. This sets `_isResourcesRegistered=false`. Then `InternalHideAsync` runs → `OnPrepareHide` still fires (this is acceptable — `OnPrepareShow` did run), but `InternalCleanup`'s guard `if (_isResourcesRegistered)` won't re-fire the hooks since resources are already unregistered. **Net effect**: `OnPrepareShow` → `RegisterResources` → *cancel* → `UnRegisterResources`. The `OnShow`/`OnPrepareHide`/`OnHide` are skipped cleanly. ✅

### ✅ Scenario 2: Close mid-show-animation
**Before**: Same issue as Scenario 1.
**After**: Same fix. Show cancel → `UnRegisterResourcesSafe`. Then `InternalHideAsync` at the system level sees `_isResourcesRegistered=false`, so `InternalCleanup` skips the hide lifecycle hooks. ✅

### ✅ Scenario 3: Show same view while close animation is running
**Before**: No `_closingViews` guard for child views → registry corruption.
**After**: 
1. `CloseChildViewAsync` now adds to `_closingViews` in try/finally. ✅
2. `PrepareAndRegisterView` excludes `_closingViews` from `existingRecord` lookup → treats the closing instance as gone → creates a new one cleanly. ✅
3. For channel views, `_closingViews` guard already existed in `CloseInternalAsync`. ✅

### ✅ Scenario 4: GoBack with empty history
`GoBackInternalAsync` → history count 0 → early return. No change needed. ✅

### ✅ Scenario 5: Parent destroyed while child's fire-and-forget animation is running
**Before**: Destroyed `CanvasGroup`/`gameObject` accessed by the orphaned async task.
**After**: `_destroyCts` is linked to every animation CTS via `CreateLinkedAnimationCts`. When `Destroy(parent.gameObject)` runs, Unity's `OnDestroy` fires on the child → `_destroyCts.Cancel()` → the in-flight show animation's CTS is cancelled → `OperationCanceledException` is caught → no further access to destroyed components. ✅

### ✅ Scenario 6: Concurrent ShowAsync of two views on same parent
**Before**: Both animations fight over the same CanvasGroup/history stack.
**After**: `RunChildViewShowAsync` serializes through `_pendingShowTasks[parent]`. Second call awaits the first's completion before pushing to history and starting its own animation. ✅

### ✅ Scenario 7: Channel views with same sort order
Both share `_channelStacks[100]`. Second pushed on top. `channel.Initialize` uses `100 + stackDepth`. Always worked. ✅

### ✅ Scenario 8: Multiple toasts + parent destroyed during animation
**Before**: Same use-after-destroy as Scenario 5.
**After**: Same fix — `_destroyCts` kills all in-flight toast animations when parent is destroyed. Each toast's `OnDestroy` cancels its own CTS too (belt and suspenders). ✅

### ✅ Scenario 9: Pooled view with static children returned to pool, then reused
**Before**: Static children removed from `_viewRegistry` during `CloseChildrenImmediate`, never re-registered on pool reuse. Ghost objects.
**After**: `InitializeStaticChildren` is now idempotent — if the child is already in the registry, it resets its lifecycle flags (`_isCleanedUp`, `_isResourcesRegistered`, `_isShowComplete`) and skips re-registration. For children NOT in the registry (removed during previous close), it re-registers them normally. The `IsViewRegistered` check makes this work for both fresh and pooled views. ✅

### ✅ Scenario 10: CloseChildViewAsync delegates to GoBack for static child at top
GoBack pops and calls `HideAndDestroyAsync(current, Normal)`. For static: `ShouldDestroyView=false` → survives. GoBack also resumes previous if exists. All correct. Now also properly guarded by `_closingViews`. ✅

### ✅ Scenario 11: Show channel view that's already in _closingViews
`_closingViews` excludes it from `existingRecord` lookup. New instance created cleanly. Old one finishes closing independently. ✅

### ✅ Scenario 12: Show non-channel view with no screens active
`FindBestParentView` returns null → error log → returns null. Acceptable behavior — caller needs at least one channel view active first. ✅

### ✅ Scenario 13: Cross-channel pause/resume
Same-channel only. HUD on channel 0 not affected by Shop on channel 100. Matches V1 behavior. ✅

### ✅ Scenario 14: Re-show a static view whose InternalHideAsync already ran
`_isResourcesRegistered=false` (set by hide). `_isCleanedUp` stays false (only `InternalCleanup` sets it). `InternalShowAsync` → resets `_isCleanedUp=false` → `OnPrepareShow` → `RegisterResources` (guard passes since `_isResourcesRegistered=false`) → animation → `OnShow`. ✅

### ✅ Scenario 15: Pool key vs OnReset clearing ViewId
Pool key captured before `InternalCleanup`/`OnReset`. Lookup uses `prefab.ViewId`. Matches. ✅

---
## Static vs Dynamic Views — V2 Behavior Matrix

### Definitions

- **Static view**: Pre-placed in the hierarchy (a child GameObject of another view). Registered via `InitializeStaticChildren`. `ViewRecord.IsStatic = true`.
- **Dynamic view**: Instantiated at runtime (from pool or `Instantiate`). Registered via `PrepareAndRegisterView`. `ViewRecord.IsStatic = false` (i.e. `IsDynamic = true`).

---

### Scenario 1: Close a static SettingsView that lives inside MainMenuView (Normal)

```
MainMenuView (channel, dynamic)
  └── SettingsView (static, ShowOnStart=false)
        └── KeybindingsView (dynamic, spawned by user)
```

**User calls**: `settingsView.Close()`

**Flow**:
1. `CloseInternalAsync(settings, Normal)` → `CloseChildViewAsync` → `GoBackInternalAsync` → `HideAndDestroyAsync`
2. `InternalHideAsync(immediate=false)` → `OnPrepareHide → [animation] → OnHide → UnRegisterResources` ✅
3. `ShouldDestroyView(static, Normal)` → **false** — SettingsView survives
4. `CloseChildrenOfSurvivingView(settings)`:
   - KeybindingsView is **dynamic** → fully destroyed (InternalCleanup, Destroy/Pool) ✅
5. SettingsView: `gameObject.SetActive(false)`, removed from history stack ✅
6. SettingsView stays in `_viewRegistry` and parent's `Children` list ✅

**Re-show**: `mainMenu.ShowChildView<SettingsView>()` → finds existing static record → `ShowRegisteredViewAsync` → `InternalShowAsync` → full show lifecycle ✅

---

### Scenario 2: MainMenuView is destroyed (its static SettingsView must also be destroyed)

```
MainMenuView (channel, dynamic)
  └── SettingsView (static)
        └── AccountView (static, nested)
```

**User calls**: `mainMenuView.Close()`

**Flow**:
1. `CloseChannelViewAsync` → `HideAndDestroyAsync(mainMenu, Normal)`
2. `ShouldDestroyView(dynamic, Normal)` → **true** — MainMenuView is destroyed
3. `CloseChildrenImmediate(mainMenu, Normal)`:
   - childContext = **ParentDestroyed**
   - SettingsView: `ShouldDestroyView(static, ParentDestroyed)` → **true** ✅
     - Recurse: `CloseChildrenImmediate(settings, ParentDestroyed)`:
       - AccountView: `ShouldDestroyView(static, ParentDestroyed)` → **true** ✅
       - AccountView.InternalCleanup → OnPrepareHide → OnHide → UnRegister ✅
       - AccountView removed from registry ✅
     - SettingsView.InternalCleanup → OnPrepareHide → OnHide → UnRegister ✅
     - SettingsView removed from registry ✅
4. MainMenuView: InternalCleanup, Destroy(gameObject) ✅

**All views destroyed.** ✅

---

### Scenario 3: ListViewer (dynamic) has static ListHeader and dynamic ListItems, ListViewer is closed

```
SomeScreenView (channel)
  └── ListViewerView (dynamic)
        ├── ListHeaderView (static)
        └── ListItem1, ListItem2, ListItem3 (dynamic)
```

**User calls**: `listViewer.Close()`

**Flow**:
1. `HideAndDestroyAsync(listViewer, Normal)`
2. `ShouldDestroyView(dynamic, Normal)` → **true** — ListViewer is destroyed
3. `CloseChildrenImmediate(listViewer, Normal)`:
   - childContext = **ParentDestroyed**
   - ListHeaderView (static): `ShouldDestroyView(static, ParentDestroyed)` → **true** ✅ **destroyed**
   - ListItem1-3 (dynamic): `ShouldDestroyView(dynamic, ParentDestroyed)` → **true** ✅ **destroyed/pooled**
4. ListViewer: InternalCleanup, Destroy(gameObject) — takes static ListHeader with it ✅

**Key**: Static children of a dynamic parent are destroyed when the parent is destroyed. The static child's lifetime is bound to its parent. ✅

---

### Scenario 4: Static view with static children — normal close (hide, not destroy)

```
MainMenuView (channel)
  └── SettingsView (static)
        └── AudioSettingsView (static, nested)
              └── VolumeSliderView (dynamic, spawned by user)
```

**User calls**: `settingsView.Close()`

**Flow**:
1. `HideAndDestroyAsync(settings, Normal)`
2. `ShouldDestroyView(static, Normal)` → **false** — SettingsView survives
3. `CloseChildrenOfSurvivingView(settings)`:
   - AudioSettingsView is **static** → survives:
     - Recurse: `CloseChildrenOfSurvivingView(audioSettings)`:
       - VolumeSliderView is **dynamic** → **destroyed** ✅
     - AudioSettingsView: InternalCleanup, SetActive(false) ✅
4. SettingsView: SetActive(false) ✅

**Re-show**: SettingsView → InternalShowAsync → ShowStaticChildrenOnStart → shows AudioSettingsView (if `ShowOnStart=true`).
AudioSettingsView goes through InternalShowAsync → can spawn new VolumeSliderView. ✅

---

### Summary Table

| Parent Type | Child Type | Parent Closed Normally | Parent Destroyed |
|-------------|-----------|----------------------|------------------|
| Dynamic | Dynamic | Child **destroyed** | Child **destroyed** |
| Dynamic | Static | Child **destroyed** (parent gone) | Child **destroyed** |
| Static | Dynamic | Child **destroyed** | Child **destroyed** |
| Static | Static | Child **hidden** (survives) | Child **destroyed** |
