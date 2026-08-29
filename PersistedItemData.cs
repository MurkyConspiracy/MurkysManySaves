using System;
using System.Collections.Generic;

namespace MurkysManySaves
{
    /// <summary>
    /// Generic per-item persisted data store, keyed by GameItem.uniqueId - the game's own
    /// stable per-instance ID. Registers itself with PersistedStoreRegistry, so any number of
    /// these, across any number of mods, save and load together off one shared subscription.
    ///
    /// T should use plain public fields, not auto-properties, if it's a custom class - this
    /// game's ES3 settings auto-serialize fields but require an [ES3Serializable] attribute on
    /// auto-properties. Primitive T (int, float, bool, string) always round-trips fine.
    /// </summary>
    public class PersistedItemData<T> : IPersistedStore
    {
        private readonly string ns;
        private readonly string key;
        private Dictionary<int, T> dataByUniqueId = new Dictionary<int, T>();

        public PersistedItemData(string ns, string key)
        {
            this.ns = ns;
            this.key = key;
            PersistedStoreRegistry.Register(this);
        }

        /// <summary>This item's persisted value, or defaultValue if never set.</summary>
        public T Get(GameItem item, T defaultValue = default)
        {
            return dataByUniqueId.TryGetValue(item.uniqueId, out T value) ? value : defaultValue;
        }

        /// <summary>Same as Get, but distinguishes "never set" from a stored value equal to default(T).</summary>
        public bool TryGet(GameItem item, out T value)
        {
            return dataByUniqueId.TryGetValue(item.uniqueId, out value);
        }

        public void Set(GameItem item, T value)
        {
            dataByUniqueId[item.uniqueId] = value;
        }

        /// <summary>
        /// Shows a tooltip line for any item with a stored value. formatter gets the item and its
        /// value and returns the line text, or null/empty to show nothing for that item. Since the
        /// text always comes from whatever was last Set (never recalculated on its own), callers
        /// can gate exactly when the display updates - e.g. keep showing a stale reading until some
        /// other action calls Set again. Call once per store, e.g. next to its declaration.
        ///
        /// prefix, if given, is called fresh for every tooltip and its result prepended to
        /// formatter's result verbatim - pass a delegate that resolves through your own mod's
        /// localization (e.g. <c>() => ModHelper.GetLocalized(MOD_ID, "key")</c>), since this
        /// assembly can't localize it itself without knowing the caller's mod ID. It's a delegate
        /// rather than a plain string because stores are typically declared as static fields at
        /// mod Init time, before localization tables are guaranteed to be loaded - resolving
        /// lazily, only when a tooltip is actually built, avoids that ordering problem.
        /// </summary>
        public void ShowInTooltip(Func<GameItem, T, string> formatter, Func<string> prefix = null, RenderHandler.ColorPalette color = RenderHandler.ColorPalette.None, bool bold = false, bool italic = false)
        {
            TooltipLineRegistry.Register(new TooltipLineConfig
            {
                GetText = item => TryGet(item, out T value) ? formatter(item, value) : null,
                PrefixProvider = prefix,
                Color = color,
                Bold = bold,
                Italic = italic,
            });
        }

        void IPersistedStore.Save(string saveFile)
        {
            ParallelFileHandler.SaveValue(saveFile, ns, key, dataByUniqueId);
        }

        void IPersistedStore.Load(string saveFile)
        {
            dataByUniqueId = ParallelFileHandler.LoadValue(saveFile, ns, key, new Dictionary<int, T>());
        }
    }
}
