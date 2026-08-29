# MurkysManySaves

A standalone save-management library for Probably Stolen mods. It lets any mod persist its own
per-save-slot data in a parallel `.es3` file, without touching the player's real save and without
a compile-time dependency on ES3 (it's resolved via reflection at runtime, since mods build against
`Assembly-CSharp` but ES3's assembly identity can vary).

Nothing in this assembly knows about any specific mod feature - callers supply their own namespace,
key, and (for the prompt) their own text and colors.

This is its own mod (see **Deployment** below) - it self-initializes as soon as the game's mod
loader enables it, so any mod that depends on it just needs `<Prerequisites><Mod>murkysmanysaves</Mod></Prerequisites>`
in its own `manifest.xml` and can start calling straight into `ParallelFileHandler`/
`SaveSlotHandler`/`PromptDialogHandler` without initializing anything itself.

## SaveSlotHandler

Detects the active save slot (from `PlayerStore.saveSlotId`, the same field the game itself uses
to build `save_{id}.es3`) and hooks the game's save/load calls, so callers don't have to guess
which file is currently in play or track pending state themselves. Patching happens automatically
via this mod's own `Init` - callers only need to subscribe:

```csharp
SaveSlotHandler.SaveCompleted += (saveFile, slot) => { /* e.g. write a flag */ };
SaveSlotHandler.LoadCompleted += (saveFile, slot) => { /* e.g. read a flag */ };

// For callers that need the active save file without waiting for the next event:
string current = SaveSlotHandler.GetCurrentSaveFile(); // e.g. "save_5.es3", or null if no game is active
```

## ParallelFileHandler

Reads and writes arbitrary values in a parallel file under `MMS/{namespace}/`, named
`{save}_{namespace}.es3` (relative to wherever ES3 resolves paths by default, typically
`Application.persistentDataPath`). Pick a namespace unique to your feature so different
mods/features don't collide. Pre-update flat files (`{save}_{namespace}.es3` sitting next to
the real save) are auto-migrated into the new location the first time they're touched.

```csharp
ParallelFileHandler.SaveValue("save_5.es3", "myfeature", "enabled", true);
bool enabled = ParallelFileHandler.LoadValue("save_5.es3", "myfeature", "enabled", false);
bool exists = ParallelFileHandler.FileExists("save_5.es3", "myfeature");
ParallelFileHandler.DeleteFile("save_5.es3", "myfeature");
```

## IPersistedStore / PersistedStoreRegistry

`IPersistedStore` is a two-method interface (`Save(string saveFile)` / `Load(string saveFile)`).
Register an instance with `PersistedStoreRegistry.Register(this)` and it saves/loads alongside
every other registered store - across every mod that uses this library - off the single
`SaveSlotHandler` subscription the registry owns internally. One store throwing is logged and
skipped rather than breaking every other mod's store. Most callers won't implement this directly;
it exists so `PersistedItemData<T>` (and anything else with its own save-worthy state) doesn't
need its own `SaveSlotHandler` subscription.

```csharp
public class MyStore : IPersistedStore
{
    public MyStore() => PersistedStoreRegistry.Register(this);
    public void Save(string saveFile) => ParallelFileHandler.SaveValue(saveFile, "myfeature", "data", myData);
    public void Load(string saveFile) => myData = ParallelFileHandler.LoadValue(saveFile, "myfeature", "data", default);
}
```

## PersistedNamespace

A convenience factory that holds your mod's ES3 namespace and mod ID once, so you don't repeat
both strings at every `PersistedValue<T>`/`PersistedItemData<T>`/`FeatureOptIn` call site (and
risk them drifting out of sync between files). Purely a shorthand - equivalent to constructing
those types directly with the same namespace.

```csharp
private static readonly PersistedNamespace MyFeature = new PersistedNamespace("mymodid", "myfeature");

private static readonly PersistedItemData<int> spoilageLevel = MyFeature.ItemData<int>("itemSpoilageLevel");
private static readonly PersistedValue<bool> trackingEnabled = MyFeature.Value<bool>("spoilageTrackingEnabled");
private static readonly FeatureOptIn trackingOptIn = MyFeature.OptIn("spoilageTrackingDecision");

string prefix = MyFeature.Localize("spoilage_prefix"); // ModHelper.GetLocalized("mymodid", "spoilage_prefix")
```

The rest of this doc shows the underlying types constructed directly, since that's what
`PersistedNamespace` calls into - use whichever reads better for your mod.

## PersistedItemData<T>

A generic per-item persisted data store, keyed by `GameItem.uniqueId` - the game's own stable
per-instance ID (the same one `EmporiumEntry.FindItemByUniqueID` uses to re-find items after a
load). An `IPersistedStore` itself, so it needs no wiring beyond construction. Caller supplies
its own namespace/key, same as `ParallelFileHandler` - nothing here is specific to any one mod's
feature, and any number of mods can each declare their own.

```csharp
private static readonly PersistedItemData<int> spoilageLevel = new PersistedItemData<int>("myfeature", "itemSpoilageLevel");

int level = spoilageLevel.Get(item); // 0 if never set
spoilageLevel.Set(item, level + 1);
```

Use `TryGet(item, out value)` instead of `Get` if you need to distinguish "never set" from a
stored value that happens to equal `default(T)`.

### ShowInTooltip

Displays a line in an item's tooltip whenever this store has a value for it, via the game's own
`ModHook.OnCreateTooltipLate` hook (fired for every `GameItem` tooltip, after all built-in
mechanic lines and before `shortDescription`/`flavorText`). `formatter` decides, per item,
whether to show a line and what it says - return `null` or `""` to show nothing for that item.

```csharp
private static readonly PersistedItemData<int> revealedSpoilage = new PersistedItemData<int>("myfeature", "revealedSpoilageLevel");
revealedSpoilage.ShowInTooltip((item, level) => $"{level}", prefix: () => ModHelper.GetLocalized(MOD_ID, "spoilage_prefix"), color: RenderHandler.ColorPalette.DarkOrange);
```

`prefix` is optional and, if given, is called fresh for every tooltip and its result prepended to
`formatter`'s text verbatim (e.g. `"Spoilage: "` before `formatter`'s `"3"`). It's a delegate
(`Func<string>`), not a plain string, because stores like this are typically declared as static
fields evaluated at mod `Init` time - before `ModHelper`'s localization tables are guaranteed to
be loaded. Passing `ModHelper.GetLocalized(...)` directly (instead of wrapped in `() => ...`)
would resolve it once, too early, and can log a "key not found" warning. MurkysManySaves doesn't
know the calling mod's ID, so it can't localize this itself - your delegate should resolve through
your own mod's localization (the same `ModHelper.GetLocalized(modId, key)` API, backed by a
per-mod `Localization/{lang}.csv` file) rather than a literal hardcoded to one language, the same
way callers already hand fully-resolved text to `PromptDialogHandler`'s `PromptDialogConfig`.

The displayed text always comes from whatever was last explicitly `Set` for that item - it's
never recalculated on its own. That makes it easy to gate *when* the tooltip updates
independently of some other, faster-changing "live" value: e.g. a piece of produce might have a
live spoilage value that ticks up constantly, but the tooltip should only reveal a reading after
the player checks it, and keep showing that stale reading (even if the live value has since
drifted) until the next check:

```csharp
// The mod's own live value - however it's tracked/computed, unrelated to tooltips.
int liveSpoilage = ComputeCurrentSpoilage(item);

// A store dedicated to "what the tooltip currently shows" - untouched by spoilage changing on its own.
private static readonly PersistedItemData<int> revealedSpoilage = new PersistedItemData<int>("myfeature", "revealedSpoilageLevel");
revealedSpoilage.ShowInTooltip((item, level) => $"{level}", prefix: () => ModHelper.GetLocalized(MOD_ID, "spoilage_prefix"));

// Only the mod's own "check this item" action snapshots the live value into the revealed store.
void OnItemChecked(GameItem item) => revealedSpoilage.Set(item, ComputeCurrentSpoilage(item));
```

Before an item is ever checked, `revealedSpoilage` has no stored value for it, so no tooltip line
is added at all.

`T` should keep its data in plain public fields (not auto-properties) if it's a custom class -
this game's ES3 settings (`safeReflection`) auto-serialize public fields but require an
`[ES3Serializable]` attribute on auto-properties (confirmed by decompiling
`ES3Internal.ES3Reflection.GetSerializableFields/Properties`). A primitive `T` (`int`, `float`,
`bool`, `string`) always round-trips with no caveats.

## PersistedValue<T>

A single save-wide persisted value - not tied to any `GameItem`, just one value per save slot
(e.g. a global int counter or a bool flag). An `IPersistedStore` itself, same as
`PersistedItemData<T>`, so it needs no wiring beyond construction. Caller supplies its own
namespace/key, same as `ParallelFileHandler`.

```csharp
private static readonly PersistedValue<bool> spoilageTrackingEnabled = new PersistedValue<bool>("myfeature", "spoilageTrackingEnabled");
private static readonly PersistedValue<int> globalSpoilageLevel = new PersistedValue<int>("myfeature", "globalSpoilageLevel", defaultValue: 1);

bool enabled = spoilageTrackingEnabled.Get(); // false until Set is called or a save is loaded
spoilageTrackingEnabled.Set(true);
```

The same `T` caveats as `PersistedItemData<T>` apply: plain public fields for a custom class,
no caveats for a primitive `T`.

## PromptDialogHandler

Shows a modal opt-in dialog: tries an existing game confirm-panel type first (via reflection),
falls back to a custom Canvas built from your config, falls back further to a console message
if UI can't be created at all. All text and colors come from `PromptDialogConfig` - nothing is
hardcoded to any one feature's theme. The color fields have sensible defaults (a neutral dark
panel, cyan accent, white title, green/gray buttons) so a minimal config still renders visibly -
override any of them for your own theme, as the example below does.

```csharp
PromptDialogHandler.Show(new PromptDialogConfig
{
    Title = "Enable My Feature?",
    BodyLines = new[] { "This save doesn't have My Feature enabled yet.", "Turn it on now?" },
    AcceptLabel = "Enable",
    DeclineLabel = "Not now",
    PanelColor = new Color(0.2f, 0.2f, 0.2f, 0.98f),
    AccentColor = Color.cyan,
    TitleColor = Color.white,
    AcceptButtonColor = Color.green,
    DeclineButtonColor = Color.gray,
    OnAccept = () => { /* persist the decision, e.g. via ParallelFileHandler */ },
    OnDecline = () => { /* persist the decision */ },
});
```

## FeatureOptIn

The one-time opt-in pattern above - show a prompt once per save, persist whatever the player
chose, and don't ask again once a save has a "declined" as well as an "enabled" decision - needs
a three-state flag (never decided / declined / enabled) plus a "don't ask twice this session"
guard. `FeatureOptIn` provides both, built on `ParallelFileHandler` so the decision is written to
disk immediately (not deferred to the next natural game save):

```csharp
private static readonly FeatureOptIn trackingOptIn = new FeatureOptIn("myfeature", "trackingDecision");

// Call this wherever the opt-in should be offered (e.g. on load, or first time the feature is relevant).
// No-ops if this save already has a decision, or this was already asked this session.
trackingOptIn.RequestIfNeeded(new PromptDialogConfig
{
    Title = "Enable My Feature?",
    BodyLines = new[] { "This save doesn't have My Feature enabled yet.", "Turn it on now?" },
    AcceptLabel = "Enable",
    DeclineLabel = "Not now",
    OnAccept = () => { /* any additional mod-specific reaction, e.g. flip a live flag */ },
    OnDecline = () => { /* optional */ },
});

// Elsewhere:
bool active = trackingOptIn.IsEnabled(saveFile);
```

`RequestIfNeeded` composes with whatever `OnAccept`/`OnDecline` you supply - it wraps them to also
persist the decision, then calls `PromptDialogHandler.Show`. The "already asked this session"
guard resets itself automatically whenever the game hard-resets (e.g. returning to the main menu)
- every `FeatureOptIn` you construct registers with `FeatureOptInRegistry`, which listens for that
once for the whole library. You only need to call `ResetSessionGuard()` yourself if you want an
earlier reset than that.

If your opt-in should be offered shortly after every load rather than from your own trigger, call
`AutoRequestOnLoad` once (e.g. during mod `Init`) instead of calling `RequestIfNeeded` yourself:

```csharp
trackingOptIn.AutoRequestOnLoad(BuildPromptConfig); // BuildPromptConfig: Func<PromptDialogConfig>
```

This subscribes to `SaveSlotHandler.LoadCompleted` and, after `delaySeconds` (1 second by
default - enough for the scene to settle after a load before a modal pops up), calls
`RequestIfNeeded` with a freshly-built config. `configFactory` is called fresh on every load
rather than once, so it can resolve localized text at call time. The delay runs on a small
persistent runner this assembly owns and manages itself, rather than requiring your mod to hunt
for a live `MonoBehaviour` in the scene to host a coroutine.

## Es3ReflectionHandler

Internal helper used by the classes above to find ES3's type/methods at runtime and invoke them,
including ES3 overloads with optional trailing parameters that `Type.GetMethod` can't match
directly. Not part of the public API.

## Why events instead of "pending save" state

An earlier version of this logic, before it was split out into its own mod, required callers to
call `SetPendingSaveFile`/`ClearPendingSave` around every save, and track whether that had been
done yet for the current session. `SaveSlotHandler.SaveCompleted`/`LoadCompleted` remove that
bookkeeping entirely - the save file name is handed to you exactly when it's known, and your
handler can decide from scratch each time whether it needs to write anything.

## Deployment

This is a dedicated mod with its own `manifest.xml` and `MurkysManySavesMain : IMod` entry
point, deployed to its own `Mods\MurkysManySaves\` folder by this project's own build target -
it is not bundled inside any other mod's folder. The game's mod loader loads every `.dll` it
finds into the same process, but only treats a `.dll` as an actual mod (with its own `Init`,
enable/disable, etc.) if it implements `IMod` - so this must ship as its own folder to show up
as a real mod rather than a dependency riding along inside someone else's.

Other mods reference `MurkysManySaves.csproj` at compile time only (`Private="false"`,
`ExcludeAssets="runtime"` on the `<ProjectReference>`) - never copy `MurkysManySaves.dll` into
another mod's own output folder, or the game ends up with two separately-loaded copies of these
types, which breaks event subscriptions silently (a subscriber on one copy never hears from the
other).

## See also

[EXAMPLE.md](EXAMPLE.md) walks through a single feature end-to-end, showing how these pieces fit
together in context rather than in isolated snippets.
