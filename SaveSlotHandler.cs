using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MurkysManySaves
{
    /// <summary>
    /// Detects the active save slot and hooks PlayerStore's save/load calls, raising events
    /// instead of requiring callers to poll or track pending state themselves.
    /// </summary>
    public static class SaveSlotHandler
    {
        private static bool isPatched = false;

        /// <summary>Raised after a save completes, with the save file name and slot number.</summary>
        public static event Action<string, int> SaveCompleted;

        /// <summary>Raised after a load completes, with the save file name and slot number.</summary>
        public static event Action<string, int> LoadCompleted;

        /// <summary>
        /// Raised as early as a save's slot can be known during a load - specifically from
        /// ModHook.OnGameLoadedInit, which PlayerStore.LoadGame fires immediately after setting
        /// saveSlotId but before it applies any vanilla save data, and therefore before
        /// ModHook.OnGameLoadedEarly/Normal/Late (all fired later in that same call, all before
        /// this mod's own Harmony postfix - and thus LoadCompleted - ever runs). PersistedStoreRegistry
        /// uses this instead of LoadCompleted so any mod's ModHook.OnGameLoaded* handler can safely
        /// read IPersistedStore data. Most callers should use LoadCompleted instead - this exists for
        /// callers that specifically need to run before those ModHook hooks.
        /// </summary>
        public static event Action<string, int> LoadStarting;

        /// <summary>Patches PlayerStore's save/load methods. Safe to call more than once.</summary>
        public static void Initialize(Harmony harmony)
        {
            if (isPatched)
                return;

            Type playerStoreType = Es3ReflectionHandler.FindType("PlayerStore");
            if (playerStoreType == null)
            {
                Debug.LogWarning("[MurkysManySaves] Could not find PlayerStore - save hooks will not work");
                return;
            }

            PatchMethod(harmony, playerStoreType, "SaveGame", "Save", nameof(SaveGamePostfix));
            PatchMethod(harmony, playerStoreType, "LoadGame", "Load", nameof(LoadGamePostfix));
            ModHook.OnGameLoadedInit += RaiseLoadStarting;
            isPatched = true;
        }

        private static void PatchMethod(Harmony harmony, Type playerStoreType, string preferredName, string shortAlias, string postfixName)
        {
            try
            {
                MethodInfo target = playerStoreType.GetMethod(preferredName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? playerStoreType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == shortAlias);

                if (target == null)
                {
                    Debug.LogWarning($"[MurkysManySaves] Could not find PlayerStore.{preferredName}");
                    return;
                }

                MethodInfo postfix = typeof(SaveSlotHandler).GetMethod(postfixName, BindingFlags.NonPublic | BindingFlags.Static);
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MurkysManySaves] Failed to patch PlayerStore.{preferredName}: {ex.Message}");
            }
        }

        private static void SaveGamePostfix(object __instance)
        {
            int? slot = GetSlotId(__instance);
            if (slot.HasValue)
                SaveCompleted?.Invoke($"save_{slot.Value}.es3", slot.Value);
        }

        private static void LoadGamePostfix(object __instance)
        {
            int? slot = GetSlotId(__instance);
            if (slot.HasValue)
                LoadCompleted?.Invoke($"save_{slot.Value}.es3", slot.Value);
        }

        /// <summary>Best-effort lookup of the active save file for callers that can't wait for the next event.</summary>
        public static string GetCurrentSaveFile()
        {
            int? slot = GetCurrentSlotId();
            return slot.HasValue ? $"save_{slot.Value}.es3" : null;
        }

        /// <summary>
        /// Fired by ModHook.OnGameLoadedInit - see LoadStarting's own doc for why this needs to be
        /// this early rather than riding on the LoadGame Harmony postfix like LoadCompleted does.
        /// </summary>
        private static void RaiseLoadStarting()
        {
            int? slot = GetCurrentSlotId();
            if (slot.HasValue)
                LoadStarting?.Invoke($"save_{slot.Value}.es3", slot.Value);
        }

        private static int? GetCurrentSlotId()
        {
            Type playerStoreType = Es3ReflectionHandler.FindType("PlayerStore");
            object instance = playerStoreType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            return instance != null ? GetSlotId(instance) : null;
        }

        private static int? GetSlotId(object playerStoreInstance)
        {
            FieldInfo field = playerStoreInstance.GetType().GetField("saveSlotId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                return null;

            int slot = (int)field.GetValue(playerStoreInstance);
            return slot >= 0 ? slot : (int?)null;
        }
    }
}
