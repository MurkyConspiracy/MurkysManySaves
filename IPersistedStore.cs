namespace MurkysManySaves
{
    /// <summary>
    /// Implement for any data that should save/load alongside every other registered store.
    /// Register via PersistedStoreRegistry.Register - most callers won't need this directly,
    /// since PersistedItemData&lt;T&gt; already covers per-item data.
    /// </summary>
    public interface IPersistedStore
    {
        void Save(string saveFile);
        void Load(string saveFile);
    }
}
