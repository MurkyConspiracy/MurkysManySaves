using HarmonyLib;
using UnityEngine;

namespace MurkysManySaves
{
    /// <summary>Mod entry point. Self-initializes the save-slot hooks so other mods can use this library standalone.</summary>
    public class MurkysManySavesMain : IMod
    {
        private const string HarmonyId = "com.murkysmanysaves.mod";
        private Harmony _harmony;

        public void Init(ModManifest manifest)
        {
            _harmony = new Harmony(HarmonyId);
            ParallelFileHandler.EnsureRootFolder();
            SaveSlotHandler.Initialize(_harmony);
            SessionHandler.Initialize(_harmony);
            Debug.Log($"[MurkysManySaves] v{manifest.ModVersion} initialized");
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
            _harmony?.UnpatchAll(HarmonyId);
        }
    }
}
