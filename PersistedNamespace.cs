namespace MurkysManySaves
{
    /// <summary>
    /// A mod's own ES3 namespace and mod ID, declared once, so PersistedValue&lt;T&gt;,
    /// PersistedItemData&lt;T&gt;, and FeatureOptIn don't each need the same two strings repeated
    /// at every call site. Purely a convenience factory - equivalent to constructing those types
    /// directly with the same ns.
    /// </summary>
    public class PersistedNamespace
    {
        private readonly string modId;
        private readonly string ns;

        public PersistedNamespace(string modId, string ns)
        {
            this.modId = modId;
            this.ns = ns;
        }

        /// <summary>The raw ES3 namespace, for callers that need to fall back to ParallelFileHandler directly.</summary>
        public string Ns => ns;

        public PersistedValue<T> Value<T>(string key, T defaultValue = default)
        {
            return new PersistedValue<T>(ns, key, defaultValue);
        }

        public PersistedItemData<T> ItemData<T>(string key)
        {
            return new PersistedItemData<T>(ns, key);
        }

        public FeatureOptIn OptIn(string key)
        {
            return new FeatureOptIn(ns, key);
        }

        /// <summary>Shorthand for ModHelper.GetLocalized(modId, key, args), using the mod ID given at construction.</summary>
        public string Localize(string key, params object[] args)
        {
            return ModHelper.GetLocalized(modId, key, args);
        }
    }
}
