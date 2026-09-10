# UGFW Identity System (UID) — Guide

This is the one document to read before touching identities in a UGFW project. It covers
why the system exists, how to set it up in a fresh project, how to use it day to day, and
how to retire content without breaking anything.

Source lives in `UGFW/Runtime/Core/UID/` (runtime) and `UGFW/Editor/UID/` (tooling).
Tests: `Assets/Tests/EditMode/Uid*Tests.cs`.

---

## 1. Why this exists

A live game needs a way to say "this thing" that survives everything a project goes through:
renames, folder moves, merges, asset bundles, server payloads, analytics, save files that
outlive the content that produced them, and designers duplicating assets by accident.

Unity gives you two tools for this and both fall short:

| Unity primitive | Why it is not an identity |
|---|---|
| Object reference (`{fileID, guid}`) | Cannot be written to a save file or sent to a server. Breaks the moment the asset is duplicated or moved into an asset bundle. `null` on a miss with no way to know *what* was missing. |
| Asset name / string key | Renames break saves. Typos compile. Two assets can share a name. String hashing allocates. |

The UGFW identity system separates **identity** from **asset**:

- **`Uid`** — a 128-bit *value* (two `ulong`s). This is the identity. It crosses every boundary:
  dictionaries, save files, server payloads, asset bundles, analytics. It cannot be null, cannot
  dangle, and is meaningful whether or not an asset currently carries it.
- **`UID`** — a *ScriptableObject* that carries exactly one `Uid`, assigned once at creation and
  never changed. This is the designer-facing handle: something to drag into an inspector field.
- **Registry** — the one place a `Uid` becomes an asset again. Lookups return `false` on a miss,
  never a null-reference or a name-based guess.

### What the redesign fixed

| Before | Now |
|---|---|
| Identity was a GUID **string**; equality was string compare, hashing allocated | 16-byte struct, two-`ulong` compare, zero-alloc hash |
| `UID` assets had **value equality** — two assets with the same GUID were "equal", silently masking asset-bundle duplication | Assets are reference-equal; identities are value-equal. A duplicate is a build error, not a coincidence |
| Empty or duplicate GUIDs were discovered at runtime | Identity is assigned once by an editor authority. Duplicates → collision window. Missing → build gate + CI lint. Runtime never mints |
| Any `Uid` field could hold any kind of asset's identity; nothing stopped a currency id from landing in a fact field | `Uid<CurrencyDefinition>` will not accept a `Uid<FactType>` — the compiler enforces it |
| Renames broke lookups keyed by name | Nothing is keyed by name. Renames are free |
| Deleting a definition orphaned every save that referenced it | Deletions go through a **redirect table**; old saves heal once at load |
| One flat registry mixing every kind, string lookups, `Initialize()` ordering bugs | Typed `UidRegistryAsset<T>` per domain; lazy-built dense lookup; one aggregate resolver |
| Save data stored references or strings that could not be validated | Save data stores `Uid`. Orphans are detectable and quarantined |

---

## 2. The types, in one page

| Type | Kind | What it is for |
|---|---|---|
| `Uid` | `struct` | The identity value. `Uid.None` is the zero identity and never resolves. Serializes as one 32-char lowercase hex string (`_value`) so YAML/JSON diffs stay readable. |
| `Uid<T>` where `T : UID` | `struct` | Same 16 bytes, plus a compile-time tag saying which kind of asset it identifies. `.Value` gives the untyped `Uid`. Prefer this everywhere the kind is known. |
| `UidHandle<T>` | `struct` | A resolved `{Id, Slot, Version}` for hot loops: one array index instead of a hash probe. Re-resolves itself if the registry rebuilt. |
| `UID` | `ScriptableObject` | Base class of every identity-carrying asset. Exposes `Id`, `HasIdentity`, `IdAs<T>()`, `Provenance`, `Notes`. Reference equality only. |
| `MetaDataAsset : UID` | `ScriptableObject` | `UID` plus `Name`, `DisplayName`, `Description`, `Icon`. Base class for every game definition. |
| `UidRegistry<T>` | `[Serializable] class` | The lookup structure: `List<T>` of tracked assets + lazily-built `Dictionary<Uid,int>` + dense array. |
| `UidRegistryAsset<T>` | `ScriptableObject` | Wraps one `UidRegistry<T>` as an asset. Subclass it once per domain (`CurrencyRegistry : UidRegistryAsset<CurrencyDefinition>`). |
| `UidRegistryAssetBase` | abstract | Non-generic view of any registry: `TryResolveUntyped`, `GetTrackedObjects`, `ElementType`. What `MetaDataRepository` iterates. |
| `MetaDataRepository` | `ScriptableObject` | The aggregate resolver. Holds every Meta container and every registry, and the redirect table. Implements `IUidResolver`. |
| `IUidResolver` | interface | `TryResolve(Uid, out UID)`, `TryResolve<T>(Uid, out T)`, `TryResolve<T>(Uid<T>, out T)`. Inject this when a service needs to turn identities into assets. |
| `IMeta` / `IMetaWithRegistry` | interface | A Meta container implements `IMetaWithRegistry` to expose its registry to the repository. |
| `UidRedirectTable` | `ScriptableObject` | Human-authored `From → To` identity replacements. Consulted on every registry miss and applied once to save data at load. |
| `UidNamespace : UID` | `ScriptableObject` | Placed in a folder, makes every asset created in that folder get a **deterministic** identity derived from its file name. For vocabularies a server must agree on. |
| `UidOfAttribute` | attribute | Constrains the inspector picker on an **untyped** `Uid` field to a given asset kind. |
| `UidDebugNames` | static | `[Conditional]` name lookup for log lines: `UidDebugNames.Describe(id)` → `SoftCurrency (c0d454b4)` in editor/dev, nothing in release. |

### `Uid` API cheat-sheet

```csharp
Uid.None                              // zero identity
Uid.NewRandom()                       // UUIDv4 — client-minted content
Uid.Deterministic(Uid ns, string name) // UUIDv5 (SHA-1) — same on every machine
Uid.Parse("c0d454b46d6640d5b270d19d61817d79")
Uid.TryParse(text, out Uid id)        // accepts N, D, {braced}, (parenthesised), any case
Uid.FromGuid(Guid) / id.ToGuid()      // text-preserving: ToGuid().ToString("N") == ToString()
Uid.FromBytes(ReadOnlySpan<byte>) / id.WriteBytes(Span<byte>) // network order: bytes[0] is the first hex pair

id.IsNone / id.IsSet
id.ToString()                         // "c0d454b46d6640d5b270d19d61817d79"
id.ToDashedString()                   // "c0d454b4-6d66-40d5-b270-d19d61817d79"
id.ToShortString()                    // "c0d454b4" — logs only, not unique
id.Hi / id.Lo                         // the two ulong halves
```

There are deliberately **no implicit conversions** between `Uid`, `string`, and `UID`. Every
crossing between identity and asset is explicit: `asset.Id` one way, `registry.TryResolve(id, out asset)`
the other.

---

## 3. Setting up a fresh project

### 3.1 Assets to create (once)

| Asset | Create via | Where | Notes |
|---|---|---|---|
| `MetaDataRepository` | `Create → AK → MetaData → MetaDataRepository` | `Assets/Content/Metadata/` | Exactly one. Referenced by your `GameBindings`. |
| `UidRedirectTable` | `Create → AK → UID → Redirect Table` | next to the repository | Optional but recommended from day one. Drag it into `MetaDataRepository._redirects`. Starts empty. |
| One `…Registry` per domain | `Create → AK → <Domain> → <Domain> Registry` | in that domain's content folder | e.g. `CurrencyRegistry`, `FactTypeRegistry`, `TransactionTypeRegistry`. Auto-tracks assets of its element type. |
| One `…Meta` per domain (if the domain has one) | `Create → AK → <Domain> → <Domain>Meta` | in that domain's content folder | Drag the domain's registry into it. Register it in bindings. |
| `UidNamespace` | `Create → AK → UID → Namespace` | **inside** the folder whose contents need deterministic ids | Optional. Only for closed vocabularies a server or another project must agree on (fact types, transaction types, analytics events). See §7. |

Registries that have **no owning Meta** (audio, particles, cameras, object pools) go in
`MetaDataRepository._standaloneRegistries` so the aggregate resolver still sees them.

### 3.2 Code to write per domain

Every domain follows the same four-part shape. Here is the complete Currency domain from
`UGFW/Examples`:

```csharp
// 1. The definition — what designers author. Extends MetaDataAsset, therefore carries a Uid.
[CreateAssetMenu(fileName = "CurrencyDefinition", menuName = "AK/MetaData/Currency/CurrencyDefinition")]
public class CurrencyDefinition : MetaDataAsset
{
    public CurrencyType Type;
    public int MaxAmount;
    public int StartingAmount;

    public Uid<CurrencyDefinition> CurrencyId => IdAs<CurrencyDefinition>();
}

// 2. The registry — one line. Subclassing gives it its own Create menu and inspector.
[CreateAssetMenu(fileName = "CurrencyRegistry", menuName = "AK/MetaData/Currency/CurrencyRegistry")]
public class CurrencyRegistry : UidRegistryAsset<CurrencyDefinition> { }

// 3. The meta — the domain's API surface. Exposes its registry to the repository.
[CreateAssetMenu(fileName = "CurrencyMeta", menuName = "AK/MetaData/Currency/CurrencyMeta")]
public class CurrencyMeta : MetaDataAsset, IMetaWithRegistry
{
    [SerializeField] private CurrencyRegistry _currencyRegistry;

    public CurrencyRegistry      Registry      => _currencyRegistry;
    public UidRegistryAssetBase  RegistryAsset => _currencyRegistry;

    public override void InitializeMeta() { }

    public bool TryGetCurrency(Uid<CurrencyDefinition> id, out CurrencyDefinition currency)
    {
        currency = null;
        return _currencyRegistry != null && _currencyRegistry.TryResolve(id, out currency);
    }
}

// 4. Bootstrap — register metas, then initialize registries, then bind.
public void InstallBindings(ContainerBuilder builder)
{
    _metaDataRepository.RegisterMeta(_currencyMeta);
    _metaDataRepository.RegisterMeta(_factsMeta);
    _metaDataRepository.InitializeRegistries();   // wires every IMetaWithRegistry.RegistryAsset + the redirect table

    builder.RegisterValue(_metaDataRepository, new[] { typeof(IMetaDataRepository), typeof(IUidResolver) });

    var transactions = new TransactionService(resolver: _metaDataRepository, redirects: _metaDataRepository.Redirects);
    var facts        = new FactService(_metaDataRepository.Redirects);
}
```

Order matters: `RegisterMeta` → `InitializeRegistries` → anything that resolves. `InitializeRegistries`
calls each meta's `InitializeMeta()`, registers its registry, hands the redirect table to every
registry, and populates `UidDebugNames`.

### 3.3 What happens automatically

You do **not** assign identities and you do **not** add assets to registries by hand.

- **Creating an asset** (`Create →` menu, or `ScriptableObject.CreateInstance` + `AssetDatabase.CreateAsset`)
  → `UidIdentityAuthority` mints a `Uid` in the same import pass. Random by default; deterministic
  if a `UidNamespace` sits in the folder or a parent.
- **Duplicating an asset in the editor** (Ctrl+D) → the copy gets a **fresh** identity. The duplicate
  is detected because the new asset carries an identity the index already owns.
- **Copying an asset outside Unity** (file explorer, `git cherry-pick` of a renamed file) → two assets
  share one identity. The authority does **not** guess. It logs, marks the collision, and
  `Tools → UGFW → UID → Resolve Collisions` asks a human to pick the survivor.
- **Saving any `UID` subclass** → `UidRegistryAutoTracker` finds the most specific registry whose
  element type matches and adds the asset to it.
- **Deleting an asset** → the auto-tracker removes the null entry from its registry.

### 3.4 Migrating an existing project

The identity is a `_id: { _value: <32 hex> }` block in the asset's YAML. If you have assets
without it, run `Tools → UGFW → UID → Repair Missing Identities`. It mints one per asset and
reports what it did. If you are migrating from a previous string-GUID scheme, strip the dashes and
lowercase the GUID — that is a valid `_value` and the old identity survives.

---

## 4. Referencing identities — which field type to use

This is the question that comes up most, so it gets its own section.

You have three ways to point at a definition from another asset or a component. They differ in
**what is stored**, **what the compiler checks**, and **what the inspector shows**.

### 4.1 Direct asset reference — `public AudioConfig WooshOut;`

```csharp
public class AudioIds : ScriptableObject
{
    public AudioConfig WooshOut;   // AudioConfig : MetaDataAsset : UID
    public AudioConfig WooshIn;
}
```

- **Stored as:** Unity object reference `{fileID, guid}`.
- **Compiler checks:** yes — only an `AudioConfig` fits.
- **Inspector:** normal object picker, filtered to `AudioConfig`.
- **Use when:** the referencing object lives in the same build as the referenced asset and you
  need the *data* (clips, volume, prefab), not just the identity. This is the right choice for the
  overwhelming majority of authoring: an `AudioIds` catalog, a tutorial step pointing at its
  `FactType`, a shop item pointing at its `CostType`.

You get the identity for free when you need it: `WooshOut.Id` or `WooshOut.IdAs<AudioConfig>()`.

### 4.2 Typed identity — `public Uid<ShopItemDefinition> Product;`

```csharp
public class ShopCategoryDefinition : MetaDataAsset
{
    public List<Uid<ShopItemDefinition>> Products = new();
}
```

- **Stored as:** the 32-hex identity string. No object reference.
- **Compiler checks:** yes — a `Uid<FactType>` cannot be assigned here.
- **Inspector:** object picker filtered to `ShopItemDefinition` (the drawer reads the generic
  argument). Shows the resolved asset's name; tinted orange if the identity resolves to nothing; copy button.
- **Use when:** the value must **outlive or cross** the asset reference:
  - it goes into a **save file** (`CurrencyModel._currencyId`, `FactCountEntry.FactId`);
  - it is **sent to or received from a server**;
  - the referenced asset may live in a **different asset bundle / Addressables group** and you do
    not want a hard dependency;
  - you want the relationship to **survive the target being deleted** (a redirect can fix it; a
    dead object reference cannot).

### 4.3 Untyped identity with a kind hint — `[UidOf(typeof(MetaDataAsset))] public Uid CostResource;`

```csharp
public class ShopCategoryDefinition : MetaDataAsset
{
    [UidOf(typeof(MetaDataAsset))]
    public Uid CostResource;   // a currency, or a stat, or an item — the category does not care which
}
```

- **Stored as:** the same 32-hex string as `Uid<T>`.
- **Compiler checks:** **no** — any `Uid` fits. That is the point.
- **Inspector:** object picker filtered to the attribute's kind (here anything deriving from
  `MetaDataAsset`). Without the attribute the picker accepts any `UID`.
- **Use when:** the slot is genuinely **polymorphic** — one field that may hold identities of
  several unrelated kinds, or a list mixing kinds. `CostResource` is the canonical example: a shop
  category can be priced in soft currency, hard currency, or a stat, and those are three unrelated
  `MetaDataAsset` subclasses. A `Uid<CurrencyDefinition>` would forbid the stat case.

The attribute is a **picker hint only**. It does nothing at runtime and does not stop code from
writing a `Uid<FactType>.Value` into the field. If you find yourself reaching for `[UidOf]` and the
kind is actually always the same, use `Uid<T>` instead — you are throwing away a compile-time check
for nothing.

### 4.4 Decision table

| You need… | Use |
|---|---|
| The asset's data, same build, no persistence | Direct reference (`AudioConfig field`) |
| To store the relationship in a save file or send it to a server | `Uid<T>` |
| To reference across asset bundles without a hard dependency | `Uid<T>` |
| One slot that can hold several unrelated kinds | `Uid` + `[UidOf(typeof(CommonBase))]` |
| A dictionary / set key | `Uid` or `Uid<T>` (both hash by value) |
| To compare two assets by identity | `a.Id == b.Id` — **never** `a == b` when you mean identity |

### 4.5 Why `AudioConfig` inherits from `MetaDataAsset` at all

Because it needs to be *resolvable by identity* even if most call sites reference it directly.
The audio system's registry (`ExtraLifeAudioRegistry`) lets a server-driven event say "play
`9d94087d…`" and lets analytics report which clip played, without anyone hard-coding names. The
inheritance gives the asset an identity; the direct reference in `AudioIds` is just the ergonomic
way for gameplay code to reach the asset without a lookup. Both are correct; they serve different
consumers.

---

## 5. Usage patterns

### 5.1 Record by asset, persist by value

```csharp
// A tutorial step: the designer wired up a FactType asset; the ledger stores its Uid.
var factId   = _advanceOn.IdAs<FactType>();
int baseline = context.Facts.Count(factId);
await context.Facts.WaitForCountAsync(factId, baseline + 1, ct);

// Services accept both, so call sites read naturally.
_facts.Record(_ageUpReadyFact);      // FactType asset
_facts.Record(savedEntry.FactId);    // Uid<FactType> read back from a save file
```

### 5.2 Resolve value → asset, with the miss meaning something

```csharp
if (!_currencyMeta.TryGetCurrency(reward.CurrencyId, out CurrencyDefinition def))
{
    Debug.LogWarning($"Reward {UidDebugNames.Describe(reward.CurrencyId)} targets a currency that no longer exists.");
    return false;   // the caller decides what a missing definition means here — nothing null propagates
}
var model = _gameModel.GetOrCreateCurrencyModel(def);
```

Registries never log on a miss. If a miss is an error *at your call site*, you log it there,
with `UidDebugNames.Describe` for a readable name. In a release build `Describe` compiles to
nothing.

### 5.3 The aggregate resolver, when you do not know the kind

```csharp
public sealed class RewardService
{
    private readonly IUidResolver _resolver;   // MetaDataRepository, injected

    public bool TryGrant(Uid rewardId)
    {
        if (_resolver.TryResolve(rewardId, out RewardDefinition reward)) { /* … */ return true; }
        if (_resolver.TryResolve(rewardId, out CurrencyDefinition currency)) { /* … */ return true; }
        return false;
    }
}
```

`MetaDataRepository.TryResolve` walks every registered registry (metas' and standalone), caches
hits, and applies redirects on a miss.

### 5.4 Hot loops — resolve once, then index

```csharp
private UidHandle<PoolableObjectDefinition> _bulletHandle;

void Awake() => _bulletHandle = _registry.GetHandle(_bulletId);

void Fire()
{
    if (_registry.TryGet(ref _bulletHandle, out var def))   // one bounds check when the handle is current;
        _pool.Get(def);                                      // re-resolves through the map if the registry rebuilt
}
```

### 5.5 Persisted models — value in, asset out, survives deletion

```csharp
[Serializable]
public class CurrencyModel : EntityModel
{
    [SerializeField] private Uid<CurrencyDefinition> _currencyId;   // this is what the save file holds
    public int Amount;

    public CurrencyDefinition CurrencyDefinition { get; private set; }
    public bool IsResolved => CurrencyDefinition != null;

    public bool TryResolve(IUidResolver resolver)
    {
        if (resolver.TryResolve(_currencyId, out CurrencyDefinition def)) { CurrencyDefinition = def; return true; }
        return false;   // the owning model quarantines this entry; it is not deleted, not guessed
    }
}
```

```csharp
// The owning game model, at load:
protected override void OnInitialized()
{
    foreach (var currency in Currencies)
        if (!currency.TryResolve(_resolver)) _orphanedCurrencies.Add(currency);
    Currencies.RemoveAll(c => !c.IsResolved);
}
```

### 5.6 Dictionaries, sets, and comparisons

```csharp
var unlocked = new HashSet<Uid<AchievementDefinition>>();     // value hashing, no allocation
var counts   = new Dictionary<Uid, int>();

bool sameThing = a.Id == b.Id;         // identity
bool sameAsset = ReferenceEquals(a, b); // instance — almost never what you mean
```

### 5.7 Analytics and server payloads

```csharp
_analytics.Track("purchase", new { item = item.Id.ToString(), category = category.Id.ToString() });
// or, if the endpoint wants RFC 4122 dashes:
payload.ItemId = item.Id.ToDashedString();
// and back:
if (Uid.TryParse(payload.ItemId, out Uid id) && _resolver.TryResolve(id, out ShopItemDefinition item)) { … }
```

---

## 6. Retiring and merging content — the redirect table

You will delete definitions. Saves in the wild will still reference them. The redirect table is
how the two reconcile.

**Scenario:** you merge `CurrencyDefinition_Gems` into `CurrencyDefinition_Hard`.

1. Copy the Gems identity before deleting it (inspector copy button, or `Tools → UGFW → UID → Inspect Identity…`).
2. Delete the Gems asset.
3. Open the `UidRedirectTable` asset; add a row:
   `From: <gems id>  To: <hard id>  Reason: "merged gems into hard"  Date: 2026-09-10`.
4. Done. No code changes.

What happens next:

- **Registries:** `TryResolve(gemsId)` misses, follows the redirect, resolves to `Hard`. Callers
  never notice.
- **Save data:** `FactService` and `TransactionService` call `ApplyRedirects` once at load and
  rewrite their ledgers in place, so the redirect only needs to exist until every player has
  loaded once past the change. (Keep the row anyway; it is documentation.)
- **Chains:** redirects follow up to 8 hops (`A → B → C`). A cycle logs an error and returns the
  last identity reached. The audit flags `redirect-cycle` and `redirect-dangling` (a `To` that
  resolves to nothing) as errors, and `redirect-live` (a `From` that still exists) as a warning.

**Do not** rename an asset to "heal" a save. Names are not identities and nothing looks them up.

**Do not** reuse a deleted asset's identity for a new asset. Old saves would silently point at the
wrong thing. Mint a new identity, add a redirect if the semantics carry over.

---

## 7. Deterministic identities — `UidNamespace`

By default identities are random (UUIDv4). That is correct for content: two projects, two
servers, two designers can mint without coordinating.

Some vocabularies must be **the same on every machine and computable without the asset**: fact
types the server records, transaction types the backend validates, analytics event ids a data
pipeline joins on. For those, drop a `UidNamespace` asset into the folder:

```
Assets/Content/Metadata/Facts/
    FactsNamespace.asset          ← UidNamespace, Path = "extra-life.facts"
    FactType_DistrictEntered.asset ← id = Deterministic(namespace.Id, "FactType_DistrictEntered")
    FactType_GoPressed.asset       ← id = Deterministic(namespace.Id, "FactType_GoPressed")
```

- Any asset **created** in that folder (or a sub-folder) gets `Provenance = Derived` and
  `ProvenanceSource = "extra-life.facts/FactType_DistrictEntered"`.
- The server computes the same `Uid` from the namespace id and the canonical name (UUIDv5:
  SHA-1 over namespace bytes + UTF-8 name, version/variant bits set). Share the namespace id and
  the naming convention once; never share a spreadsheet of ids again.
- **Renaming a Derived asset does not change its identity** — identity is assigned once. If you
  want the name and identity to agree again, that is a deliberate act: re-mint from the audit, and
  add a redirect from the old identity.

Runtime API, if code needs it: `_factsNamespace.Derive("district_entered")`.

---

## 8. Tooling reference

All under **`Tools → UGFW → UID →`**:

| Menu | What it does |
|---|---|
| **Audit Project** | Runs every rule below and prints one report to the console (error/warning/info by severity). Opens the collision window automatically if any collision is unresolved. Same rules run in the build validator. |
| **Repair Missing Identities** | Mints an identity for every `UID` asset that has none, saves, logs how many it minted, then re-checks for collisions. |
| **Resolve Collisions** | Window listing every identity owned by more than one asset. You pick the survivor; the others are re-minted (Minted/Derived) or refused (Imported — re-import correctly instead). |
| **Inspect Identity…** | Paste any `Uid` (any text form) → see which asset owns it, its registry, provenance, and redirects touching it. |
| **Registries → Refresh All From Project** | Rebuilds every registry's tracked list from the asset database. Normally unnecessary; the auto-tracker keeps them current. |
| **Export Catalog (JSON)** | Writes `Build/uid-catalog.json`: every identity, owning asset path, type, provenance. Hand this to server/analytics teams. |

Inspectors: every `UID` subclass shows an **Identity** block (hex with copy button, provenance,
source, which registry tracks it). `UidRegistryAsset` inspectors count nulls, missing identities,
duplicates, and untracked project assets, with one-click fixes. `UidRedirectTable` inspectors
validate each row as you type: unset ends, self-redirects, a `From` that is still live, and a chain
that ends at an identity no asset owns. `Uid`/`Uid<T>` fields render as an object picker filtered
to the right kind, show the resolved asset name, tint orange when the identity resolves to nothing,
and offer a copy button.

### Audit rules

| Code | Severity | Meaning |
|---|---|---|
| `missing-identity` | error | A `UID` asset has no identity. Run Repair. |
| `collision` | error | Two or more assets share an identity. Resolve Collisions. |
| `registry-null` | error | A registry tracks a null (deleted) entry. Refresh. |
| `redirect-cycle` | error | A redirect chain loops. |
| `redirect-dangling` | error | A redirect's `To` resolves to nothing. |
| `persisted-asset` | error | A `PersistableState` subclass has a field typed as a `UID` asset. Persist `Uid`/`Uid<T>`, never the asset. |
| `addressable-uid` | error | A `UID` asset is inside an Addressables group or folder entry. A bundled identity asset is duplicated into every bundle that references it — two live instances, one identity. Keep identity assets in the main build and reference them from bundles by `Uid`. |
| `registry-untracked` | warning | An asset of a registry's element type is not in that registry. Auto-tracker will fix on next save; or Refresh. |
| `registry-overlap` | warning | Two registries track the same asset. |
| `redirect-live` | warning | A redirect's `From` still exists as an asset. Probably forgot to delete it. |
| `external-key` | warning | A definition's third-party string key (`EventID`, `ProductID`, `VariableKey`, `PlacementID`, `NotificationID`) is empty or shared with another asset. These keys address external systems (store SKU, remote-config key, ad placement); they are not identities, but they still have to be unique. |

### Gates

- **Build:** `UidBuildValidator` (`IPreprocessBuildWithReport`, `callbackOrder -100`) runs the
  audit and throws `BuildFailedException` on any error. Warnings pass.
- **CI:** `.github/workflows/check-uid-identities.yaml` runs `.github/scripts/check-uid-identities.js`
  on every PR touching `unity/Assets/**/*.asset`. It is a text-level lint (no Unity required):
  missing/duplicate `_value`, malformed hex, redirect targets that do not exist.

---

## 9. Rules of the road

1. **Never compare or store a `UID` asset when you mean identity.** Use `.Id`. Assets are
   reference-equal; identities are value-equal.
2. **Never persist an asset reference.** Save `Uid`/`Uid<T>`. The audit will fail the build if a
   `PersistableState` field is asset-typed.
3. **Never mint or assign an identity from runtime code.** There is no public API for it; the
   only write path is `internal` and editor-only. If you think you need it, you need a redirect
   or a namespace.
4. **Never reuse a retired identity.** Mint new, redirect old.
5. **A miss is information, not an error.** Registries return `false`. Decide at the call site
   what missing means, and use `UidDebugNames.Describe` in the log.
6. **Prefer `Uid<T>` over `Uid`.** Reach for `[UidOf]` only when the slot is genuinely polymorphic.
7. **Prefer a direct asset reference over `Uid<T>`** when nothing crosses a boundary. The identity is
   always one `.Id` away.
8. **Let the tooling do the bookkeeping.** Do not hand-edit `_id` in YAML; do not add assets to
   registries by hand. If something looks wrong, run Audit and read the report.

---

## 10. FAQ

**I see `_redirects` and `_standaloneRegistries` on `MetaDataRepository`. What goes there?**
`_redirects`: your project's one `UidRedirectTable` (optional; leave empty until you retire
something). `_standaloneRegistries`: registries that have no Meta container — audio, particles,
cameras, object pools — so the aggregate resolver can still find their assets. Registries owned by
a Meta are picked up automatically through `IMetaWithRegistry`.

**Why is the identity a 32-char string in YAML instead of two numbers?**
Readable diffs, greppable, copy-pasteable into the Inspect window and into server logs. The
runtime never touches the string after the first parse.

**Can two projects share identities?**
Yes, two ways. Random ids do not collide in practice (UUIDv4 space). For ids that must be
*computed* on both sides, share a `UidNamespace` id and use the same canonical names (§7).

**What happens if I hand-copy an asset file in Explorer?**
Both files carry the same `_id`. On import the authority detects the collision, logs once, and
lists it in Resolve Collisions. Nothing is auto-changed — the tool cannot know which one your
saves mean. You choose; the other gets a fresh identity.

**What about assets that came from another project with `Provenance = Imported`?**
Their identity is authoritative elsewhere. The collision resolver refuses to re-mint them; fix the
import instead.

**Why can't I compare `Uid` to `string` with `==`?**
Deliberately. Every conversion is explicit (`Uid.Parse`, `.ToString()`) so that a string key and
an identity can never be confused in code review.

**Does `Uid<T>` cost anything at runtime?**
No. It is the same 16 bytes as `Uid`; the type argument exists only at compile time.

**How do I find the asset for an id in a log line?**
`Tools → UGFW → UID → Inspect Identity…` and paste the full hex (any text form). If the log
only has the 8-char `ToShortString` prefix, search the project for `_value: <prefix>` — the
identity is plain text in the asset YAML.
