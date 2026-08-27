using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MurkysManySaves
{
    /// <summary>
    /// Detects the active save slot and hooks PlayerStore.SaveGame/LoadGame, raising events
    /// instead of requiring callers to poll or track pending state. The slot always comes from
    /// PlayerStore.saveSlotId - the same field the game itself uses to build "save_{id}.es3".
    /// </summary>
    public static class Save_Slot_Handler
    {
        private static bool isPatched = false;

        /// <summary>Raised after PlayerStore.SaveGame runs, with the save file name and slot number.</summary>
        public static event Action<string, int> SaveCompleted;

        /// <summary>Raised after PlayerStore.LoadGame runs, with the save file name and slot number.</summary>
        public static event Action<string, int> LoadCompleted;

        /// <summary>Locates PlayerStore and patches its Save/Load methods. Safe to call more than once.</summary>
        public static void Initialize(Harmony harmony)
        {
            if (isPatched)
                return;

            Type playerStoreType = ES3_Reflection_Handler.FindType("PlayerStore");
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

                MethodInfo postfix = typeof(Save_Slot_Handler).GetMethod(postfixName, BindingFlags.NonPublic | BindingFlags.Static);
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

        /// <summary>
        /// Best-effort lookup of the active save file for callers that need it outside a
        /// save/load event (e.g. an API called at an arbitrary time).
        /// </summary>
        public static string GetCurrentSaveFile()
        {
            Type playerStoreType = ES3_Reflection_Handler.FindType("PlayerStore");
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
