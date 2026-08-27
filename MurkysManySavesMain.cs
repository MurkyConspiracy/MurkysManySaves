using HarmonyLib;
using UnityEngine;

namespace MurkysManySaves
{
    /// <summary>Mod entry point. Self-initializes the save-slot hooks so any other mod can use this library standalone.</summary>
    public class MurkysManySavesMain : IMod
    {
        private const string HARMONY_ID = "com.murkysmanysaves.mod";
        private Harmony _harmony;

        public void Init(ModManifest manifest)
        {
            _harmony = new Harmony(HARMONY_ID);
            Parallel_File_Handler.EnsureRootFolder();
            Save_Slot_Handler.Initialize(_harmony);
            Debug.Log($"[MurkysManySaves] v{manifest.ModVersion} initialized");
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
            _harmony?.UnpatchAll(HARMONY_ID);
        }
    }
}
