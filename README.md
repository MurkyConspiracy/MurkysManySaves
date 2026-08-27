# MurkysManySaves

A standalone mod and generic save-management library for Probably Stolen mods. It lets any
mod feature persist its own per-save-slot data in a parallel `.es3` file, without touching the
player's real save and without a compile-time dependency on ES3 (it's resolved via reflection
at runtime, since mods build against `Assembly-CSharp` but ES3's assembly identity can vary).

Nothing in this assembly knows about "Rust Mode" or any other specific feature — callers
supply their own namespace, key, and (for the prompt) their own text and colors.

This is its own mod (see **Deployment** below) - it self-initializes as soon as the game's mod
loader enables it, so any mod that depends on it just needs `<Prerequisites><Mod>murkysmanysaves</Mod></Prerequisites>`
in its own `manifest.xml` and can start calling straight into `Parallel_File_Handler`/
`Save_Slot_Handler`/`Prompt_Dialog_Handler` without initializing anything itself.

## Save_Slot_Handler

Detects the active save slot (from `PlayerStore.saveSlotId`, the same field the game itself
uses to build `save_{id}.es3`) and hooks the game's save/load calls, so callers don't have to
guess which file is currently in play or track pending state themselves. Patching happens
automatically via this mod's own `Init` - callers only need to subscribe:

```csharp
Save_Slot_Handler.SaveCompleted += (saveFile, slot) => { /* e.g. write a flag */ };
Save_Slot_Handler.LoadCompleted += (saveFile, slot) => { /* e.g. read a flag */ };

// For callers that need the active save file without waiting for the next event:
string current = Save_Slot_Handler.GetCurrentSaveFile(); // e.g. "save_5.es3", or null if no game is active
```

## Parallel_File_Handler

Reads and writes arbitrary values in a parallel file under `MMS/{namespace}/`, named
`{save}_{namespace}.es3` (relative to wherever ES3 resolves paths by default, typically
`Application.persistentDataPath`). Pick a namespace unique to your feature so different
mods/features don't collide. Pre-update flat files (`{save}_{namespace}.es3` sitting next to
the real save) are auto-migrated into the new location the first time they're touched.

```csharp
Parallel_File_Handler.SaveValue("save_5.es3", "myfeature", "enabled", true);
bool enabled = Parallel_File_Handler.LoadValue("save_5.es3", "myfeature", "enabled", false);
bool exists = Parallel_File_Handler.FileExists("save_5.es3", "myfeature");
Parallel_File_Handler.DeleteFile("save_5.es3", "myfeature");
```

## Prompt_Dialog_Handler

Shows a modal opt-in dialog: tries an existing game confirm-panel type first (via reflection),
falls back to a custom Canvas built from your config, falls back further to a console message
if UI can't be created at all. All text and colors come from `PromptDialogConfig` — nothing is
hardcoded to any one feature's theme.

```csharp
Prompt_Dialog_Handler.Show(new PromptDialogConfig
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
    OnAccept = () => { /* persist the decision, e.g. via Parallel_File_Handler */ },
    OnDecline = () => { /* persist the decision */ },
});
```

## ES3_Reflection_Handler

Internal helper used by the classes above to find ES3's type/methods at runtime and invoke
them, including ES3 overloads with optional trailing parameters that `Type.GetMethod` can't
match directly. Not part of the public API.

## Why events instead of "pending save" state

An earlier version of this logic (inside TheRust directly) required callers to call
`SetPendingSaveFile`/`ClearPendingSave` around every save, and track whether that had been
done yet for the current session. `Save_Slot_Handler.SaveCompleted`/`LoadCompleted` remove
that bookkeeping entirely — the save file name is handed to you exactly when it's known, and
your handler can decide from scratch each time whether it needs to write anything.

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
