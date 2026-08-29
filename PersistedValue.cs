namespace MurkysManySaves
{
    /// <summary>
    /// A single save-wide value - not tied to any GameItem, just one value per save slot
    /// (e.g. a global counter or flag). Registers itself with PersistedStoreRegistry, so it
    /// needs no wiring beyond construction. For per-item data, use PersistedItemData&lt;T&gt;.
    ///
    /// T should use plain public fields, not auto-properties, if it's a custom class - this
    /// game's ES3 settings auto-serialize fields but require an [ES3Serializable] attribute on
    /// auto-properties. Primitive T (int, float, bool, string) always round-trips fine.
    /// </summary>
    public class PersistedValue<T> : IPersistedStore
    {
        private readonly string ns;
        private readonly string key;
        private readonly T defaultValue;
        private T value;

        public PersistedValue(string ns, string key, T defaultValue = default)
        {
            this.ns = ns;
            this.key = key;
            this.defaultValue = defaultValue;
            this.value = defaultValue;
            PersistedStoreRegistry.Register(this);
        }

        /// <summary>The current value - defaultValue until Set is called or a save is loaded.</summary>
        public T Get()
        {
            return value;
        }

        public void Set(T newValue)
        {
            value = newValue;
        }

        void IPersistedStore.Save(string saveFile)
        {
            ParallelFileHandler.SaveValue(saveFile, ns, key, value);
        }

        void IPersistedStore.Load(string saveFile)
        {
            value = ParallelFileHandler.LoadValue(saveFile, ns, key, defaultValue);
        }
    }
}
