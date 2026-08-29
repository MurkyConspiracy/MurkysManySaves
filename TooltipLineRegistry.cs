using System;
using System.Collections.Generic;
using UnityEngine;

namespace MurkysManySaves
{
    /// <summary>
    /// Text and styling for one registered tooltip line. GetText gets the item whose tooltip
    /// is being built and returns that line's text, or null/empty to show nothing for that item.
    ///
    /// PrefixProvider, if set, is called fresh for every tooltip and its result prepended to
    /// GetText's result verbatim - pass a delegate that resolves through your own mod's
    /// localization, since this assembly doesn't know the caller's mod ID and can't localize it
    /// itself. It's a delegate rather than a plain string because configs are typically built at
    /// mod Init time, before localization tables are guaranteed to be loaded - resolving lazily,
    /// only when a tooltip is actually built, avoids that ordering problem.
    /// </summary>
    public class TooltipLineConfig
    {
        public Func<GameItem, string> GetText;
        public Func<string> PrefixProvider;
        public RenderHandler.ColorPalette Color = RenderHandler.ColorPalette.None;
        public bool Bold = false;
        public bool Italic = false;
    }

    /// <summary>
    /// Central registry of tooltip-line providers from any mod. Subscribes to
    /// ModHook.OnCreateTooltipLate once and runs every registered config against the item being
    /// built, so N mods don't each need their own hook. A throwing config is logged and skipped
    /// rather than breaking the rest of that tooltip. Most callers should use
    /// PersistedItemData&lt;T&gt;.ShowInTooltip instead of this directly.
    /// </summary>
    public static class TooltipLineRegistry
    {
        private static readonly List<TooltipLineConfig> configs = new List<TooltipLineConfig>();
        private static bool isHooked = false;

        public static void Register(TooltipLineConfig config)
        {
            configs.Add(config);
            EnsureHooked();
        }

        public static void Unregister(TooltipLineConfig config)
        {
            configs.Remove(config);
        }

        private static void EnsureHooked()
        {
            if (isHooked)
            {
                return;
            }
            isHooked = true;
            ModHook.OnCreateTooltipLate += HandleTooltip;
        }

        private static void HandleTooltip(RichTextBuilder builder, GameItem item)
        {
            foreach (TooltipLineConfig config in configs.ToArray())
            {
                try
                {
                    string text = config.GetText(item);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    string prefix = config.PrefixProvider?.Invoke();
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        text = prefix + text;
                    }

                    builder.AddLine(text, color: config.Color, bold: config.Bold, italic: config.Italic);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MurkysManySaves] TooltipLineConfig threw: {ex}");
                }
            }
        }
    }
}
