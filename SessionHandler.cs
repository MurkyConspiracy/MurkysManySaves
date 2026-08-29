using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MurkysManySaves
{
    /// <summary>
    /// Detects the boundary between one play session and the next - returning to the main menu /
    /// starting fresh - by hooking the game's own NewGameData.HardReset, the same way
    /// SaveSlotHandler hooks PlayerStore's save/load calls. Lets per-session state (like
    /// FeatureOptIn's "already asked this session" guard) reset automatically instead of every
    /// mod patching HardReset itself.
    /// </summary>
    public static class SessionHandler
    {
        private static bool isPatched = false;

        /// <summary>Raised when the game hard-resets (e.g. returning to the main menu).</summary>
        public static event Action SessionReset;

        /// <summary>Patches NewGameData.HardReset. Safe to call more than once.</summary>
        public static void Initialize(Harmony harmony)
        {
            if (isPatched)
                return;

            Type newGameDataType = Es3ReflectionHandler.FindType("NewGameData");
            if (newGameDataType == null)
            {
                Debug.LogWarning("[MurkysManySaves] Could not find NewGameData - session-reset hook will not work");
                return;
            }

            try
            {
                MethodInfo target = newGameDataType.GetMethod("HardReset",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                if (target == null)
                {
                    Debug.LogWarning("[MurkysManySaves] Could not find NewGameData.HardReset");
                    return;
                }

                MethodInfo postfix = typeof(SessionHandler).GetMethod(nameof(HardResetPostfix), BindingFlags.NonPublic | BindingFlags.Static);
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                isPatched = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MurkysManySaves] Failed to patch NewGameData.HardReset: {ex.Message}");
            }
        }

        private static void HardResetPostfix()
        {
            SessionReset?.Invoke();
        }
    }
}
