using System.Collections.Generic;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>
    /// Per-content-pack character settings (persisted in config.json).
    /// </summary>
    public class CharacterConfig
    {
        /// <summary>Whether this character's animated portraits are enabled.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Locked variant name, or "Auto" for normal heart-based progression.
        /// Examples: "Auto", "Hearts2", "Hearts4", "Hearts6", "Hearts8", "Hearts10", "" (base/no variant).
        /// </summary>
        public string LockedVariant { get; set; } = "Auto";
    }

    /// <summary>
    /// Settings for a single content pack (keyed by UniqueID).
    /// </summary>
    public class PackConfig
    {
        /// <summary>Whether this entire content pack is enabled.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Whether the heart-based growth/variant system is active.</summary>
        public bool GrowthEnabled { get; set; } = true;

        /// <summary>Whether body-change transition animations play on variant triggers.</summary>
        public bool ShowTransitions { get; set; } = true;

        /// <summary>Per-character settings. Key = NPC internal name.</summary>
        public Dictionary<string, CharacterConfig> Characters { get; set; } = new();
    }

    /// <summary>
    /// Root config model saved to config.json. Keyed by content pack UniqueID.
    /// </summary>
    public class ModConfig
    {
        /// <summary>Per-content-pack settings. Key = content pack UniqueID.</summary>
        public Dictionary<string, PackConfig> Packs { get; set; } = new();

        /// <summary>Whether each dynamically discovered overworld variant is enabled.</summary>
        public Dictionary<string, bool> VariantEnabled { get; set; } = new();

        public bool IsVariantEnabled(string variantId)
        {
            return !VariantEnabled.TryGetValue(variantId, out bool enabled) || enabled;
        }
    }
}
