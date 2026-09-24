using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>
    /// Discovers, loads, and validates content packs registered with this framework.
    /// </summary>
    public class ContentPackManager
    {
        private readonly IMonitor Monitor;

        /// <summary>All loaded portrait definitions keyed by NPC name (case-insensitive).</summary>
        public Dictionary<string, PortraitDefinition> Portraits { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Maps NPC name → content pack that provides the sprite (for texture loading).</summary>
        public Dictionary<string, IContentPack> SpriteProviders { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Maps NPC name to the content pack that owns its definition and translations.</summary>
        public Dictionary<string, IContentPack> ContentProviders { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Maps NPC name → content pack UniqueID that defines it.</summary>
        public Dictionary<string, string> NpcPackIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Loaded content pack metadata (UniqueID → pack manifest name).</summary>
        public Dictionary<string, string> PackNames { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Loaded content pack manifests (UniqueID → IManifest).</summary>
        public Dictionary<string, IManifest> PackManifests { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Loaded overworld variant rules keyed by NPC name.</summary>
        public Dictionary<string, List<VariantRule>> Rules { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Manages per-expression texture loading/disposal.</summary>
        public TextureManager TextureManager { get; }

        public ContentPackManager(IMonitor monitor)
        {
            this.Monitor = monitor;
            this.TextureManager = new TextureManager(monitor);
        }

        /// <summary>Loads all content packs and registers their portrait definitions.</summary>
        public void LoadAll(IEnumerable<IContentPack> contentPacks)
        {
            this.Portraits.Clear();
            this.SpriteProviders.Clear();
            this.ContentProviders.Clear();
            this.NpcPackIds.Clear();
            this.PackNames.Clear();
            this.PackManifests.Clear();
            this.Rules.Clear();
            this.TextureManager.DisposeAll();

            foreach (var pack in contentPacks)
            {
                this.Monitor.Log($"Loading content pack: {pack.Manifest.Name} ({pack.Manifest.UniqueID})", LogLevel.Info);

                ContentPackData data;
                try
                {
                    data = pack.ReadJsonFile<ContentPackData>("content.json");
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"  Error reading content.json: {ex.Message}", LogLevel.Error);
                    continue;
                }

                if (data == null)
                {
                    this.Monitor.Log("  No content.json found, skipping.", LogLevel.Warn);
                    continue;
                }

                var rules = this.CollectRules(pack, data);
                foreach (var rule in rules)
                {
                    if (!this.ValidateAndSanitize(rule, pack.Manifest.Name))
                        continue;

                    if (!this.Rules.TryGetValue(rule.NPC, out var npcRules))
                    {
                        npcRules = new List<VariantRule>();
                        this.Rules[rule.NPC] = npcRules;
                    }
                    npcRules.Add(rule);
                }

                List<PortraitDefinition> portraits = this.CollectPortraits(pack, data);

                if (portraits.Count == 0)
                {
                    this.Monitor.Log("  No portraits defined, skipping.", LogLevel.Warn);
                    continue;
                }

                foreach (var portraitEntry in portraits)
                {
                    PortraitDefinition portrait = portraitEntry;
                    if (string.IsNullOrWhiteSpace(portrait.Target))
                    {
                        this.Monitor.Log("  Skipping portrait with empty target.", LogLevel.Warn);
                        continue;
                    }

                    // Resolve computed fields for each expression
                    foreach (var kvp in portrait.Expressions)
                    {
                        kvp.Value.Resolve();
                        this.Monitor.Log(
                            $"    Expr {kvp.Key}: {kvp.Value.Frames} frames → {kvp.Value.TotalFrames} total, " +
                            $"{kvp.Value.Fps} FPS ({kvp.Value.FrameDelayMs:F0}ms), mode={kvp.Value.Mode}",
                            LogLevel.Trace
                        );
                    }

                    // Resolve variant expressions (independent Frames/FPS/Mode per variant)
                    if (portrait.VariantExpressions != null)
                    {
                        foreach (var veKvp in portrait.VariantExpressions)
                        {
                            foreach (var exprKvp in veKvp.Value)
                            {
                                exprKvp.Value.Resolve();
                                this.Monitor.Log(
                                    $"    Variant '{veKvp.Key}' Expr {exprKvp.Key}: {exprKvp.Value.Frames} frames → {exprKvp.Value.TotalFrames} total, " +
                                    $"{exprKvp.Value.Fps} FPS ({exprKvp.Value.FrameDelayMs:F0}ms), mode={exprKvp.Value.Mode}",
                                    LogLevel.Trace
                                );
                            }
                        }
                    }

                    // Resolve BodyChange animations in variant triggers
                    if (portrait.VariantTriggers != null)
                    {
                        foreach (var vtKvp in portrait.VariantTriggers)
                        {
                            if (vtKvp.Value.BodyChange != null)
                            {
                                vtKvp.Value.BodyChange.Resolve();
                                this.Monitor.Log(
                                    $"    Trigger '{vtKvp.Key}' BodyChange: {vtKvp.Value.BodyChange.Frames} frames → {vtKvp.Value.BodyChange.TotalFrames} total, " +
                                    $"{vtKvp.Value.BodyChange.Fps} FPS, mode={vtKvp.Value.BodyChange.Mode}, sprite={vtKvp.Value.BodyChange.Sprite}",
                                    LogLevel.Trace
                                );
                            }
                        }
                    }

                    if (this.Portraits.TryGetValue(portrait.Target, out var existingPortrait)
                        && this.NpcPackIds.TryGetValue(portrait.Target, out string prevPackId)
                        && prevPackId == pack.Manifest.UniqueID)
                    {
                        // Merge Variants (avoid duplicates)
                        if (portrait.Variants != null)
                        {
                            existingPortrait.Variants ??= new List<string>();
                            foreach (var v in portrait.Variants)
                            {
                                if (!existingPortrait.Variants.Contains(v))
                                    existingPortrait.Variants.Add(v);
                            }
                        }

                        // Merge Expressions
                        if (portrait.Expressions != null)
                        {
                            existingPortrait.Expressions ??= new Dictionary<string, ExpressionDefinition>();
                            foreach (var kvp in portrait.Expressions)
                                existingPortrait.Expressions[kvp.Key] = kvp.Value;
                        }

                        // Merge VariantExpressions
                        if (portrait.VariantExpressions != null)
                        {
                            existingPortrait.VariantExpressions ??= new Dictionary<string, Dictionary<string, ExpressionDefinition>>();
                            foreach (var variantKvp in portrait.VariantExpressions)
                            {
                                if (!existingPortrait.VariantExpressions.ContainsKey(variantKvp.Key))
                                    existingPortrait.VariantExpressions[variantKvp.Key] = new Dictionary<string, ExpressionDefinition>();

                                foreach (var exprKvp in variantKvp.Value)
                                    existingPortrait.VariantExpressions[variantKvp.Key][exprKvp.Key] = exprKvp.Value;
                            }
                        }

                        // Merge VariantTriggers
                        if (portrait.VariantTriggers != null)
                        {
                            existingPortrait.VariantTriggers ??= new Dictionary<string, VariantTrigger>();
                            foreach (var triggerKvp in portrait.VariantTriggers)
                                existingPortrait.VariantTriggers[triggerKvp.Key] = triggerKvp.Value;
                        }

                        // Continue resolving the merged definition rather than the discarded fragment.
                        portrait = existingPortrait;
                        this.Monitor.Log($"  Merged additional definitions into existing '{portrait.Target}'.", LogLevel.Trace);
                    }
                    else
                    {
                        // First time encountering this target, or a later content pack override.
                        this.Portraits[portrait.Target] = portrait;
                        this.ContentProviders[portrait.Target] = pack;
                        this.NpcPackIds[portrait.Target] = pack.Manifest.UniqueID;
                    }
                    if (!this.PackNames.ContainsKey(pack.Manifest.UniqueID))
                    {
                        this.PackNames[pack.Manifest.UniqueID] = pack.Manifest.Name;
                        this.PackManifests[pack.Manifest.UniqueID] = pack.Manifest;
                    }

                    // Detect per-expression mode: check if ANY base expression OR variant expression defines its own Sprite path
                    // Preserve existing true state if we are operating on a merged object
                    if (!portrait.IsPerExpressionMode)
                        portrait.IsPerExpressionMode = false;

                    // 1. Check base Expressions
                    foreach (var kvp2 in portrait.Expressions)
                    {
                        if (!string.IsNullOrWhiteSpace(kvp2.Value.Sprite))
                        {
                            portrait.IsPerExpressionMode = true;
                            break;
                        }
                    }

                    // 2. Check VariantExpressions if base check did not find a Sprite path
                    if (!portrait.IsPerExpressionMode && portrait.VariantExpressions != null)
                    {
                        foreach (var veKvp in portrait.VariantExpressions)
                        {
                            if (veKvp.Value == null)
                                continue;

                            foreach (var exprKvp in veKvp.Value)
                            {
                                if (exprKvp.Value != null && !string.IsNullOrWhiteSpace(exprKvp.Value.Sprite))
                                {
                                    portrait.IsPerExpressionMode = true;
                                    break;
                                }
                            }

                            if (portrait.IsPerExpressionMode)
                                break;
                        }
                    }

                    // Track sprite provider for texture loading
                    if (portrait.IsPerExpressionMode)
                    {
                        this.SpriteProviders[portrait.Target] = pack;
                        this.TextureManager.RegisterPack(portrait.Target, pack);

                        string variantInfo = portrait.Variants != null && portrait.Variants.Count > 0
                            ? $", variants: [{string.Join(", ", portrait.Variants)}]"
                            : "";
                        this.Monitor.Log(
                            $"  {portrait.Target}: {portrait.Expressions.Count} expression(s), " +
                            $"{portrait.Columns} cols, {portrait.FrameSize}px — PER-EXPRESSION sprites{variantInfo}",
                            LogLevel.Info
                        );
                    }
                    else if (!string.IsNullOrWhiteSpace(portrait.Sprite))
                    {
                        this.SpriteProviders[portrait.Target] = pack;
                        this.TextureManager.RegisterPack(portrait.Target, pack);
                        this.Monitor.Log(
                            $"  {portrait.Target}: {portrait.Expressions.Count} expression(s), " +
                            $"{portrait.Columns} cols, {portrait.FrameSize}px — single sheet: {portrait.Sprite}",
                            LogLevel.Info
                        );
                    }
                    else
                    {
                        this.Monitor.Log(
                            $"  {portrait.Target}: {portrait.Expressions.Count} expression(s), " +
                            $"{portrait.Columns} cols — using external texture (requires separate CP mod)",
                            LogLevel.Info
                        );
                    }
                }
            }

            this.Monitor.Log($"Loaded {this.Portraits.Count} animated portrait(s) total.", LogLevel.Info);
            this.Monitor.Log($"Loaded rules for {this.Rules.Count} NPC(s).", LogLevel.Info);
        }

        private List<VariantRule> CollectRules(IContentPack pack, ContentPackData root)
        {
            var rules = new List<VariantRule>();
            var activeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var loadedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddFrom(ContentPackData data, string sourceFile)
            {
                if (loadedFiles.Add(sourceFile))
                    rules.AddRange(data.Rules ?? new List<VariantRule>());

                foreach (string rawPath in data.Include ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(rawPath))
                        continue;

                    string path = rawPath.Trim().Replace('\\', '/').TrimStart('/');
                    if (!activeFiles.Add(path))
                    {
                        this.Monitor.Log($"  Include cycle detected at '{path}', skipping rules.", LogLevel.Warn);
                        continue;
                    }

                    ContentPackData included;
                    try
                    {
                        included = pack.ReadJsonFile<ContentPackData>(path);
                    }
                    catch (Exception ex)
                    {
                        this.Monitor.Log($"  Error reading included rules file '{path}': {ex.Message}", LogLevel.Error);
                        activeFiles.Remove(path);
                        continue;
                    }

                    if (included != null)
                        AddFrom(included, path);
                    activeFiles.Remove(path);
                }
            }

            activeFiles.Add("content.json");
            AddFrom(root, "content.json");
            return rules;
        }

        private bool ValidateAndSanitize(VariantRule rule, string packName)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.NPC) || string.IsNullOrWhiteSpace(rule.Root)
                || rule.Variants == null || rule.Variants.Count == 0)
            {
                this.Monitor.Log($"[{packName}] Invalid variant rule skipped.", LogLevel.Warn);
                return false;
            }

            for (int i = rule.Variants.Count - 1; i >= 0; i--)
            {
                var variant = rule.Variants[i];
                if (variant == null || string.IsNullOrWhiteSpace(variant.Id))
                {
                    rule.Variants.RemoveAt(i);
                    continue;
                }

                foreach (var condition in variant.Conditions ?? new List<ConditionDefinition>())
                {
                    if (string.Equals(condition.Type, "Random", StringComparison.OrdinalIgnoreCase))
                        condition.Chance = Math.Clamp(condition.Chance, 0.0, 1.0);
                }
            }

            return rule.Variants.Count > 0;
        }

        /// <summary>
        /// Collects the portraits from a pack's content.json plus any files listed in its
        /// Include field. Included files use the same format and may include further files;
        /// each file is loaded at most once per pack, so cycles are ignored safely.
        /// </summary>
        private List<PortraitDefinition> CollectPortraits(IContentPack pack, ContentPackData root)
        {
            var portraits = new List<PortraitDefinition>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "content.json" };

            void AddFrom(ContentPackData data, string sourceFile)
            {
                if (data.Portraits != null)
                    portraits.AddRange(data.Portraits);

                if (data.Include == null)
                    return;

                foreach (string rawPath in data.Include)
                {
                    if (string.IsNullOrWhiteSpace(rawPath))
                        continue;

                    string path = rawPath.Trim().Replace('\\', '/');
                    if (!visited.Add(path))
                    {
                        this.Monitor.Log($"  '{sourceFile}' includes '{path}', which was already loaded; skipping duplicate.", LogLevel.Warn);
                        continue;
                    }

                    ContentPackData included;
                    try
                    {
                        included = pack.ReadJsonFile<ContentPackData>(path);
                    }
                    catch (Exception ex)
                    {
                        this.Monitor.Log($"  Error reading included file '{path}': {ex.Message}", LogLevel.Error);
                        continue;
                    }

                    if (included == null)
                    {
                        this.Monitor.Log($"  Included file '{path}' not found, skipping.", LogLevel.Warn);
                        continue;
                    }

                    this.Monitor.Log($"  Including '{path}' ({included.Portraits?.Count ?? 0} portrait(s)).", LogLevel.Info);
                    AddFrom(included, path);
                }
            }

            AddFrom(root, "content.json");
            return portraits;
        }
    }
}
