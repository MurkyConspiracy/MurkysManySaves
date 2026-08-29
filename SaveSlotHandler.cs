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
            Type playerStoreType = Es3ReflectionHandler.FindType("PlayerStore");
            object instance = playerStoreType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            int? slot = instance != null ? GetSlotId(instance) : null;
            return slot.HasValue ? $"save_{slot.Value}.es3" : null;
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
