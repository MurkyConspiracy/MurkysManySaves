using System;
using System.Collections.Generic;
using UnityEngine;

namespace MurkysManySaves
{
    /// <summary>
    /// Central registry of FeatureOptIn instances from any mod. Subscribes to
    /// SessionHandler.SessionReset once and resets every registered instance's session guard
    /// together, so N mods don't each need their own HardReset patch just to clear "already asked
    /// this session". A throwing instance is logged and skipped rather than breaking the rest.
    /// </summary>
    internal static class FeatureOptInRegistry
    {
        private static readonly List<FeatureOptIn> instances = new List<FeatureOptIn>();
        private static bool isHooked = false;

        internal static void Register(FeatureOptIn optIn)
        {
            instances.Add(optIn);
            EnsureHooked();
        }

        private static void EnsureHooked()
        {
            if (isHooked)
            {
                return;
            }
            isHooked = true;
            SessionHandler.SessionReset += () =>
            {
                foreach (FeatureOptIn optIn in instances.ToArray())
                {
                    try
                    {
                        optIn.ResetSessionGuard();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MurkysManySaves] FeatureOptIn threw during session reset: {ex}");
                    }
                }
            };
        }
    }
}
