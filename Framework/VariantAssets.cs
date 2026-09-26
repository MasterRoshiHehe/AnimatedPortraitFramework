using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>
    /// Remembers whether some mod provides a sub-variant's portrait (<c>Portraits/{NPC}_{Variant}</c>).
    /// In ContentPatcher mode, only sub-variants whose portrait exists are rolled, so turning an outfit off in the
    /// Content Patcher pack (e.g. with a config option) also stops APF from rolling it.
    /// </summary>
    internal static class VariantAssets
    {
        /// <summary>"npc|variant" → whether its portrait asset exists.</summary>
        private static readonly Dictionary<string, bool> _portraitExists = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether a mod provides <c>Portraits/{npc}_{variant}</c>. Cached until <see cref="Clear"/>.</summary>
        public static bool PortraitExists(IGameContentHelper content, string npc, string variant)
        {
            string key = $"{npc}|{variant}";
            if (_portraitExists.TryGetValue(key, out bool exists))
                return exists;

            try
            {
                exists = content.DoesAssetExist<Texture2D>(content.ParseAssetName($"Portraits/{npc}_{variant}"));
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor?.Log($"[ROLL] Couldn't check Portraits/{npc}_{variant}: {ex.Message}", LogLevel.Trace);
                exists = false;
            }

            _portraitExists[key] = exists;
            if (!exists)
                ModEntry.ModMonitor?.Log($"[ROLL] {npc}: '{variant}' skipped — Portraits/{npc}_{variant} isn't loaded by any mod.", LogLevel.Trace);

            return exists;
        }

        /// <summary>Forget all cached results (the "skipped" log lines will show again).</summary>
        public static void Clear()
        {
            _portraitExists.Clear();
        }
    }
}
