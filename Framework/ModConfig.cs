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

        /// <summary>Darken dialogue portraits to match the world's lighting.</summary>
        public PortraitLightingConfig PortraitLighting { get; set; } = new();

        public bool IsVariantEnabled(string variantId)
        {
            return !VariantEnabled.TryGetValue(variantId, out bool enabled) || enabled;
        }
    }

    /// <summary>Settings for darkening dialogue portraits at night, in caves, etc.</summary>
    public class PortraitLightingConfig
    {
        /// <summary>Whether portraits follow the world's lighting at all.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>How strongly portraits follow the world's darkness, in percent (100 = as dark as the world).</summary>
        public int Strength { get; set; } = 60;

        /// <summary>Portraits never get darker than this, in percent brightness.</summary>
        public int MinimumBrightness { get; set; } = 35;

        /// <summary>Extra multiplier for indoor darkness, in percent (100 = same as outdoors).</summary>
        public int IndoorStrength { get; set; } = 100;

        /// <summary>Darken evenly instead of taking on the game's (bluish) night colour.</summary>
        public bool NeutralColors { get; set; } = false;

        /// <summary>Apply to every portrait DDFC draws (true) or only portraits APF is showing (false).</summary>
        public bool AllPortraits { get; set; } = true;
    }
}
