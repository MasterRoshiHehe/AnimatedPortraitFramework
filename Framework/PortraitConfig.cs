using System.Collections.Generic;
using System.IO;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>
    /// Root model for a content pack's content.json.
    /// </summary>
    public class ContentPackData
    {
        /// <summary>Format version string (e.g. "1.0.0").</summary>
        public string Format { get; set; } = "1.0.0";

        /// <summary>
        /// Who decides which variant (outfit) an NPC uses. Applies to every portrait in this pack
        /// unless a portrait sets its own <see cref="PortraitDefinition.VariantControl"/>.
        /// Only read from the pack's main content.json, not from included files.
        /// <list type="bullet">
        ///   <item><c>"APF"</c> (default): APF picks variants itself from season, weather, location and hearts, with heart triggers and the GMCM variant lock.</item>
        ///   <item><c>"ContentPatcher"</c>: the game / Content Patcher decides the variant. APF looks at the portrait the
        ///   game gave the NPC (e.g. <c>Portraits/Marnie_Rainy</c> from an Appearance entry → variant <c>Rainy</c>) and shows
        ///   its own image for it. APF's season/weather/location/hearts logic, heart triggers and the GMCM variant lock
        ///   are switched off. Rules (weighted random sub-variants) still apply.</item>
        /// </list>
        /// </summary>
        public string VariantControl { get; set; }

        /// <summary>
        /// Optional list of additional JSON files to load, relative to the content pack folder
        /// (e.g. "assets/Patches/Haley.json"). Each file uses the same format as content.json
        /// and may itself include further files. Portraits from included files are appended
        /// to this pack's Portraits list.
        /// </summary>
        public List<string> Include { get; set; } = new();

        /// <summary>List of portrait definitions in this pack.</summary>
        public List<PortraitDefinition> Portraits { get; set; } = new();

        /// <summary>Overworld sprite variant rules in this pack.</summary>
        public List<VariantRule> Rules { get; set; } = new();
    }

    /// <summary>
    /// A single NPC portrait animation definition.
    /// </summary>
    public class PortraitDefinition
    {
        /// <summary>NPC internal name (e.g. "Haley").</summary>
        public string Target { get; set; } = "";

        /// <summary>
        /// Who decides which variant this NPC uses: "APF" (default) or "ContentPatcher".
        /// If omitted, the pack-level <see cref="ContentPackData.VariantControl"/> is used.
        /// </summary>
        public string VariantControl { get; set; }

        /// <summary>Whether variants for this NPC are chosen by Content Patcher instead of APF's built-in logic.</summary>
        public bool IsContentPatcherControlled =>
            string.Equals(this.VariantControl?.Trim(), "ContentPatcher", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Path to a single combined spritesheet PNG (legacy single-sheet mode).
        /// Used only when per-expression Sprite paths are NOT set.
        /// </summary>
        public string Sprite { get; set; }

        /// <summary>True if any expression defines its own Sprite path (set at load time, not serialized).</summary>
        public bool IsPerExpressionMode { get; set; }

        /// <summary>Frame width and height in pixels (square frames).</summary>
        public int FrameSize { get; set; } = 1024;

        /// <summary>Number of columns in the spritesheet grid.</summary>
        public int Columns { get; set; } = 5;

        /// <summary>DDF display settings. If null, no DDF config is injected.</summary>
        public DdfSettings Ddf { get; set; }

        /// <summary>
        /// When true, APF will override DDF's portrait sizing for this NPC. Set to false to let
        /// Content Patcher / DDFC / SVO control the portrait layout without APF stomping it.
        /// </summary>
        public bool OverrideDdf { get; set; } = true;

        /// <summary>
        /// Expression animation configs. Key is the portrait index as string ("0", "1", "2", ...).
        /// Any expression index NOT in this dictionary passes through to static vanilla/DDF behavior.
        /// There is no upper limit — define as many expressions as your spritesheet contains.
        /// </summary>
        public Dictionary<string, ExpressionDefinition> Expressions { get; set; } = new();

        /// <summary>
        /// Available variant names for this NPC (e.g. ["Summer", "Winter", "Beach"]).
        /// When a variant is active, the framework looks for sprites in a subfolder:
        ///   Portraits/Haley_0.png  →  Portraits/Summer/Haley_0.png
        /// Falls back to default sprite if the variant file doesn't exist.
        /// </summary>
        public List<string> Variants { get; set; } = new();

        /// <summary>
        /// Independent expression definitions per variant.
        /// Outer key = variant name (e.g. "Hearts2", "Summer"), inner key = expression index.
        /// When a variant is active AND has an entry here, its Frames/FPS/Mode/Sprite are used
        /// instead of the default Expressions dictionary. This allows each variant to have
        /// completely different animation parameters.
        /// </summary>
        public Dictionary<string, Dictionary<string, ExpressionDefinition>> VariantExpressions { get; set; } = new();

        /// <summary>
        /// Trigger configuration per variant. Variants not listed here default to "persistent".
        /// Allows one-time trigger dialogues when a heart threshold is first reached.
        /// </summary>
        public Dictionary<string, VariantTrigger> VariantTriggers { get; set; } = new();

        /// <summary>
        /// What happens to a variant (root) during cutscenes and festivals.
        /// Key = root variant name (e.g. "Sweetheart_Summer"). Works whether or not the root has Rules.
        /// </summary>
        public Dictionary<string, VariantEventSettings> VariantEvents { get; set; } = new();

        /// <summary>
        /// Resolves the sprite path for an expression, taking the active variant into account.
        /// If a variant subfolder version exists, returns that; otherwise returns the base sprite.
        /// </summary>
        public string ResolveSpritePath(string baseSprite, string activeVariant, string packDir)
        {
            if (string.IsNullOrWhiteSpace(activeVariant) || string.IsNullOrWhiteSpace(baseSprite))
                return baseSprite;

            // Insert variant folder before the filename:
            //   "Portraits/Haley_0.png" + "Summer" → "Portraits/Summer/Haley_0.png"
            string dir = Path.GetDirectoryName(baseSprite) ?? "";
            string file = Path.GetFileName(baseSprite);
            string variantPath = Path.Combine(dir, activeVariant, file).Replace('\\', '/');

            // Check if the variant file actually exists in the content pack
            string fullPath = Path.Combine(packDir, variantPath);
            return File.Exists(fullPath) ? variantPath : baseSprite;
        }
    }

    /// <summary>
    /// Trigger configuration for a single variant.
    /// Determines when and how a variant's special dialogue is shown.
    /// </summary>
    public class VariantTrigger
    {
        /// <summary>
        /// "persistent" = always active when condition is met (default).
        /// "once" = show Dialogue the first time the threshold is reached, then become persistent.
        /// </summary>
        public string Mode { get; set; } = "persistent";

        /// <summary>
        /// Dialogue text shown once when the variant triggers for the first time.
        /// Only used when Mode is "once". Supports Stardew dialogue commands.
        /// </summary>
        public string Dialogue { get; set; } = "";

        /// <summary>
        /// Translation key loaded from the owning content pack's i18n folder.
        /// When set, this takes priority over Dialogue; Dialogue remains as a
        /// backwards-compatible fallback for older content packs.
        /// </summary>
        public string DialogueKey { get; set; } = "";

        /// <summary>
        /// Optional portrait expression index to use for the trigger dialogue.
        /// Kept outside the translated text so translators don't need to preserve
        /// Stardew dialogue commands like "$12".
        /// </summary>
        public int? Expression { get; set; }

        /// <summary>
        /// Dedicated body-change animation played during the trigger dialogue.
        /// Separate from normal expression indices — only used for the one-time trigger moment.
        /// If null, no body-change animation is played.
        /// </summary>
        public ExpressionDefinition BodyChange { get; set; }
    }

    /// <summary>
    /// Event settings for one root variant: separate behaviour for cutscenes (heart events, the wedding,
    /// any non-festival event) and festivals (Egg Festival, Flower Dance, Spirit's Eve, ...).
    /// </summary>
    public class VariantEventSettings
    {
        /// <summary>Behaviour during cutscenes (any event that isn't a festival, including the wedding).</summary>
        public VariantEventBehavior Cutscenes { get; set; }

        /// <summary>Behaviour during festivals.</summary>
        public VariantEventBehavior Festivals { get; set; }
    }

    /// <summary>What APF does with one root variant during one kind of event.</summary>
    public class VariantEventBehavior
    {
        /// <summary>
        /// Don't paint the rolled Rules sub-variant over the overworld sprite: the NPC uses the root's own sheet
        /// (whatever Content Patcher loaded for <c>Characters/{NPC}_{Root}</c>).
        /// </summary>
        public bool SkipRolledSprite { get; set; }

        /// <summary>Don't use the rolled Rules sub-variant for the dialogue portrait: show the root's own portrait.</summary>
        public bool SkipRolledPortrait { get; set; }

        /// <summary>
        /// Optional sprite sheet asset (e.g. "Characters/Caroline_Summer") painted over the root sheet during this
        /// kind of event, instead of the rolled sub-variant. Use it when the events need animation frames that only
        /// that sheet has. Ignored if the asset doesn't exist.
        /// </summary>
        public string Sprite { get; set; }

        /// <summary>Optional mod UniqueID. If set, this whole block only applies while that mod is installed.</summary>
        public string RequiresMod { get; set; }
    }

    /// <summary>
    /// Animation definition for a single expression.
    /// </summary>
    public class ExpressionDefinition
    {
        /// <summary>Number of unique animation frames for this expression (before pingpong expansion).</summary>
        public int Frames { get; set; } = 10;

        /// <summary>Frames per second.</summary>
        public int Fps { get; set; } = 8;

        /// <summary>
        /// Animation mode: "loop", "pingpong", "once", "once-pingpong".
        /// </summary>
        public string Mode { get; set; } = "loop";

        /// <summary>
        /// Path to per-expression spritesheet PNG, relative to the content pack folder.
        /// When set, the framework loads only this small texture instead of a giant combined sheet.
        /// </summary>
        public string Sprite { get; set; }

        /// <summary>
        /// Per-expression variant overrides. Maps variant name → sprite path.
        /// Takes priority over the NPC-level Variants subfolder convention.
        /// Example: { "Beach": "Portraits/Beach/Haley_special_0.png" }
        /// </summary>
        public Dictionary<string, string> Variants { get; set; }

        // --- Auto-calculated at load time ---

        /// <summary>Total frames after mode expansion (e.g. pingpong: frames + frames - 2).</summary>
        public int TotalFrames { get; set; }

        /// <summary>Frame delay in milliseconds (1000 / fps).</summary>
        public double FrameDelayMs { get; set; }

        /// <summary>Compute derived fields from user-provided values.</summary>
        public void Resolve()
        {
            this.FrameDelayMs = 1000.0 / this.Fps;

            switch (this.Mode?.ToLowerInvariant())
            {
                case "pingpong":
                case "once-pingpong":
                    this.TotalFrames = this.Frames > 2 ? this.Frames * 2 - 2 : this.Frames;
                    break;
                default: // "loop", "once"
                    this.TotalFrames = this.Frames;
                    break;
            }
        }
    }

    /// <summary>
    /// DDF (Dialogue Display Framework) layout settings for the portrait.
    /// These are injected into DDF's data asset so the modder doesn't need a separate CP mod.
    /// </summary>
    public class DdfSettings
    {
        public int XOffset { get; set; } = -950;
        public int YOffset { get; set; } = -950;
        public float Scale { get; set; } = 1f;
        public string Position { get; set; } = "bottom";

        // Optional overrides for the dialogue box itself
        public int? BoxWidth { get; set; }
        public int? BoxHeight { get; set; }
        public int? BoxXOffset { get; set; }
        public int? BoxYOffset { get; set; }
    }
}
