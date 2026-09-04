using System;
using System.Collections.Generic;
using UnityEngine;

namespace MurkysManySaves
{
    /// <summary>
    /// Central registry of IPersistedStore instances from any mod. Subscribes to
    /// SaveSlotHandler once and runs every registered store's Save/Load together, so N mods
    /// don't each need their own subscription. A throwing store is logged and skipped rather
    /// than breaking everyone else's.
    ///
    /// Load runs off SaveSlotHandler.LoadStarting rather than LoadCompleted, so every store is
    /// populated before any mod's ModHook.OnGameLoadedInit/Early/Normal/Late handler runs - see
    /// LoadStarting's own doc for why LoadCompleted (tied to the LoadGame Harmony postfix) is too
    /// late for that.
    /// </summary>
    public static class PersistedStoreRegistry
    {
        private static readonly List<IPersistedStore> stores = new List<IPersistedStore>();
        private static bool isHooked = false;

        public static void Register(IPersistedStore store)
        {
            stores.Add(store);
            EnsureHooked();
        }

        public static void Unregister(IPersistedStore store)
        {
            stores.Remove(store);
        }

        private static void EnsureHooked()
        {
            if (isHooked)
            {
                return;
            }
            isHooked = true;
            SaveSlotHandler.SaveCompleted += (saveFile, slot) => RunAll(store => store.Save(saveFile));
            SaveSlotHandler.LoadStarting += (saveFile, slot) => RunAll(store => store.Load(saveFile));
        }

        private static void RunAll(Action<IPersistedStore> action)
        {
            foreach (IPersistedStore store in stores.ToArray())
            {
                try
                {
                    action(store);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MurkysManySaves] IPersistedStore '{store.GetType()}' threw: {ex}");
                }
            }
        }
    }
}
