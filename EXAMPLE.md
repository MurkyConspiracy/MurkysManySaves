# Example: a produce-spoilage feature

A worked example putting the pieces in [README.md](README.md) together for one imagined mod
feature: items slowly spoil, but the player only sees a spoilage reading after they check the
item, and can opt in or out of tracking altogether.

## 1. Declare a namespace, then the stores

One `PersistedNamespace` for the whole feature, so the ES3 namespace and mod ID are each written
once instead of repeated at every store. Two `PersistedItemData<int>` stores per item: one for the
mod's own live value, one for what the tooltip currently shows. Declaring either is enough - no
manual save/load wiring needed, since `PersistedItemData<T>` registers itself with
`PersistedStoreRegistry` on construction.

```csharp
private static readonly PersistedNamespace MyFeature = new PersistedNamespace(MOD_ID, "myfeature");

private static readonly PersistedItemData<int> spoilageLevel = MyFeature.ItemData<int>("itemSpoilageLevel");
private static readonly PersistedItemData<int> revealedSpoilage = MyFeature.ItemData<int>("revealedSpoilageLevel");
```

## 2. Show it in the tooltip - once

`prefix` is a delegate, resolved fresh each time a tooltip is built, rather than a plain string -
these stores are declared as static fields evaluated at mod `Init` time, before `ModHelper`'s
localization tables are guaranteed to be loaded, so resolving eagerly here would risk a "key not
found" warning.

```csharp
revealedSpoilage.ShowInTooltip(
    (item, level) => $"{level}",
    prefix: () => MyFeature.Localize("spoilage_prefix"),
    color: RenderHandler.ColorPalette.DarkOrange);
```

Nothing shows up until `revealedSpoilage` has a value for that item - which only happens in step 3.

## 3. Update the live value, reveal it only on request

```csharp
// Runs continuously, e.g. once per in-game hour. Unrelated to what the player sees.
void OnSpoilageTick(GameItem item)
{
    int current = spoilageLevel.Get(item);
    spoilageLevel.Set(item, current + 1);
}

// Runs only when the player actively checks the item.
void OnItemChecked(GameItem item)
{
    revealedSpoilage.Set(item, spoilageLevel.Get(item));
}
```

The tooltip keeps showing whatever was last revealed, even as the live value keeps ticking up
underneath it.

## 4. Let the player opt in

`FeatureOptIn` already knows not to ask twice: once a save has a recorded decision (accepted or
declined), or this was already asked this session, `RequestIfNeeded` no-ops.

```csharp
private static readonly FeatureOptIn spoilageTrackingOptIn = MyFeature.OptIn("spoilageTrackingDecision");

void OnFeatureFirstSeen()
{
    spoilageTrackingOptIn.RequestIfNeeded(new PromptDialogConfig
    {
        Title = "Track Spoilage?",
        BodyLines = new[] { "This save doesn't have spoilage tracking enabled yet.", "Turn it on now?" },
        AcceptLabel = "Enable",
        DeclineLabel = "Not now",
    });
}

bool trackingIsOn = spoilageTrackingOptIn.IsEnabled(SaveSlotHandler.GetCurrentSaveFile());
```

That's the whole feature: one shared namespace, two per-item stores, one tooltip line, one opt-in
decision - all persisted automatically, all keyed to whatever namespace this mod picked for
itself.
