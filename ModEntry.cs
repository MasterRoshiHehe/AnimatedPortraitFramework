using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using AnimatedPortraitFramework.Framework;

namespace AnimatedPortraitFramework
{
    /// <summary>
    /// Animated Portrait Framework — main entry point.
    /// Loads content packs, replaces portrait textures, configures DDF, and animates portraits via Harmony.
    /// Supports two modes:
    ///   - Single-sheet: one big texture per NPC (legacy, small expression counts)
    ///   - Per-expression: separate small PNG per expression (~80 MB VRAM each, scales to many NPCs)
    /// </summary>
    public class ModEntry : Mod
    {
        // ── Static fields for Harmony access ──
        internal static ContentPackManager PackManager;
        internal static Dictionary<string, AnimationState> ActiveAnimations = new(StringComparer.OrdinalIgnoreCase);
        internal static int CurrentAnimatedFrameIndex = -1;
        internal static string CurrentAnimatedNpc;
        internal static bool IsAnimating;
        internal static int LastVanillaPortraitIndex = -1;
        internal static IMonitor ModMonitor;
        internal static TextureManager TexManager;
        internal static ModEntry Instance;
        internal static ActiveVariantCache ActiveVariantCache = new();

        /// <summary>
        /// CarryOver rules (NPC + root) still in their "morning" stretch today: the NPC hasn't changed
        /// out of that root yet, so the roll uses yesterday's seed. Key: "npc|root" (case-insensitive).
        /// Filled at DayStarted, emptied as each NPC gets dressed. Recomputed from the world date, so
        /// nothing is saved and every multiplayer client reaches the same result.
        /// </summary>
        private static readonly Dictionary<string, (string Npc, string Root)> _carryOverMorning = new(StringComparer.OrdinalIgnoreCase);
        private static VariantEvaluator _evaluator;

        /// <summary>User configuration (GMCM settings, persisted to config.json).</summary>
        internal static ModConfig Config;

        /// <summary>Tracks how many ticks since the current dialogue started.</summary>
        private static int _dialogueTickCount;

        /// <summary>Reference to the current DialogueBox to detect new dialogue instances.</summary>
        private static DialogueBox _lastDialogueBox;

        /// <summary>
        /// The NPC's real portrait texture (the one the game/Content Patcher gave it) from before APF
        /// swapped in its own texture. Restored when the dialogue closes, and used to read the
        /// portrait suffix (e.g. Portraits/Marnie_Rainy → "Rainy").
        /// </summary>
        private static readonly Dictionary<string, (NPC Npc, Texture2D Texture)> _originalPortraits = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The APF texture currently applied to each speaking NPC.</summary>
        private static readonly Dictionary<string, Texture2D> _appliedTextures = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// True while APF itself asks the game for the vanilla portrait index, so the
        /// getPortraitIndex postfix returns the untouched vanilla value.
        /// </summary>
        private static bool _queryingVanillaIndex;

        /// <summary>NPC name for which we have patched DDF's cached ActiveData in the current dialogue session.</summary>
        private static string _ddfPatchedNpc;

        /// <summary>Session-level guard to prevent redundant DDF patching while the same dialogue box remains active.</summary>
        private static bool _ddfPatchedThisSession;

        /// <summary>Speaker that owns the current DDF patch in this session.</summary>
        private static string _ddfPatchedSessionNpc;

        /// <summary>Dialogue box currently associated with the DDF patch in this session.</summary>
        private static DialogueBox _ddfPatchedSessionBox;

        /// <summary>Reference to the DDF Portrait sub-object we last mutated (for identity check).</summary>
        private static object _ddfPatchedPortraitObj;

        /// <summary>Saved original DDF portrait values so we can restore them when dialogue closes.</summary>
        private static (int W, int H, float Scale, int XOffset, int YOffset, bool Bottom, bool Right, string TexturePath)? _ddfOriginalValues;

        // ── Cached DDF Reflection lookups (resolved once, reused every tick) ──
        private static bool _ddfReflectionResolved;
        private static System.Reflection.PropertyInfo _ddfActiveDataProp;
        private static System.Reflection.PropertyInfo _ddfPortraitProp;
        private static System.Reflection.MethodInfo _ddfCacheActiveDataMethod;
        private static Dictionary<string, System.Reflection.MemberInfo> _ddfPortraitMembers;

        // ── Variant trigger tracking ──
        private const string TRIGGER_SAVE_KEY = "APF_TriggeredVariants";
        private TriggeredVariantsData _triggeredData;

        /// <summary>NPCs with pending one-time trigger dialogue to inject on next conversation.</summary>
        private readonly Dictionary<string, string> _pendingTriggerDialogue = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>NPCs with pending trigger variant (so we know which variant just triggered).</summary>
        private readonly Dictionary<string, string> _pendingTriggerVariant = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>CP/event portrait suffix override detected at dialogue start (e.g. "Pyjamas" from Portraits/Haley_Pyjamas).</summary>
        private static readonly Dictionary<string, string> _cpVariantOverrides = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Body-change animations queued for NPCs whose trigger hasn't been injected yet.</summary>
        private readonly Dictionary<string, ExpressionDefinition> _pendingBodyChange = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Body-change animations ready to play on the next dialogue box for an NPC.</summary>
        private static readonly Dictionary<string, ExpressionDefinition> _activeBodyChange = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The normal dialogue which was temporarily covered by a one-time trigger.
        /// It is reopened on the next tick after the trigger dialogue closes.
        /// </summary>
        private NPC _deferredNormalSpeaker;
        private Dialogue _deferredNormalDialogue;
        private string _deferredNormalNpcName;
        private Game1.afterFadeFunction _deferredNormalAfterDialogues;

        /// <summary>Prevents the follow-up normal dialogue from being intercepted recursively.</summary>
        private bool _openingDeferredNormalDialogue;

        // ── DDF asset target ──
        private const string DDF_ASSET = "aedenthorn.DialogueDisplayFramework/dictionary";

        /// <summary>True if at least one portrait defines DDF settings to inject.</summary>
        private bool _hasDdfConfig;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            ModMonitor = this.Monitor;
            Config = helper.ReadConfig<ModConfig>();
            Config.PortraitLighting ??= new PortraitLightingConfig();
            PackManager = new ContentPackManager(this.Monitor);
            TexManager = PackManager.TextureManager;
            _evaluator = new VariantEvaluator(
                PackManager,
                new ConditionChecker(helper.ModRegistry),
                this.Monitor);

            // Harmony: override portrait index
            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(Dialogue), nameof(Dialogue.getPortraitIndex)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Dialogue_getPortraitIndex_Postfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.drawDialogue), new[] { typeof(NPC) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Game1_drawDialogue_Prefix))
            );

            // Portrait lighting: tint DDFC's portrait draw to match the world's darkness.
            PortraitLighting.Apply(harmony, this.Monitor);

            try
            {
                ResolveDdfReflection();
                if (_ddfCacheActiveDataMethod != null)
                {
                    harmony.Patch(
                        original: _ddfCacheActiveDataMethod,
                        postfix: new HarmonyMethod(typeof(ModEntry), nameof(Ddf_CacheActiveData_Postfix))
                    );
                    this.Monitor.Log("DDFC ActiveData initialization patched for dynamic portrait textures.", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"DDFC ActiveData patch unavailable: {ex.Message}", LogLevel.Trace);
            }

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.UpdateTicking += this.OnUpdateTicking;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Player.Warped += this.OnWarped;
            helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.Content.AssetsInvalidated += this.OnAssetsInvalidated;

            this.Monitor.Log("Animated Portrait Framework initialized.", LogLevel.Info);
        }

        // ====================================================================
        // LIFECYCLE
        // ====================================================================

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            this.LoadContentPacks();
            this.RegisterGmcm();
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            _carryOverMorning.Clear(); // DayStarted refills it
            EventOutfits.Reset();
            VariantAssets.Clear();
            _variantAssetsDirty = false;
            // Reload packs and invalidate caches so textures + DDF data refresh
            this.LoadContentPacks();
            this.InvalidateAssets();

            // Load triggered variants data from save
            _triggeredData = this.Helper.Data.ReadSaveData<TriggeredVariantsData>(TRIGGER_SAVE_KEY)
                ?? new TriggeredVariantsData();
            _pendingTriggerDialogue.Clear();
            _pendingTriggerVariant.Clear();
        }

        private void LoadContentPacks()
        {
            PackManager.LoadAll(this.Helper.ContentPacks.GetOwned());
            ActiveAnimations.Clear();
            ActiveVariantCache.Clear();
            _resolvedSpriteCache.Clear();
            EventOutfits.Rebuild(PackManager);

            // Check if any portrait has DDF settings to inject
            _hasDdfConfig = false;
            foreach (var kvp in PackManager.Portraits)
            {
                if (kvp.Value.Ddf != null)
                {
                    _hasDdfConfig = true;
                    break;
                }
            }
        }

        private void InvalidateAssets()
        {
            foreach (var kvp in PackManager.Portraits)
            {
                string npc = kvp.Key;
                var portrait = kvp.Value;

                // Per-expression mode manages textures directly — skip content pipeline invalidation
                if (portrait.IsPerExpressionMode)
                    continue;

                if (PackManager.SpriteProviders.ContainsKey(npc))
                    this.Helper.GameContent.InvalidateCache($"Portraits/{npc}");
            }

            // Only invalidate DDF if we actually inject config — otherwise we'd
            // destroy another mod's (e.g. CP) DDF settings for no reason.
            if (_hasDdfConfig)
                this.Helper.GameContent.InvalidateCache(DDF_ASSET);
        }

        // ====================================================================
        // VARIANT TRIGGER SYSTEM
        // ====================================================================

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // Re-check which outfits Content Patcher loads today, before anything rolls (incl. CarryOver's yesterday roll).
            VariantAssets.Clear();
            _variantAssetsDirty = false;

            ActiveVariantCache.Clear();
            _pendingTriggerDialogue.Clear();
            _pendingTriggerVariant.Clear();
            _pendingBodyChange.Clear();
            _activeBodyChange.Clear();
            _cpVariantOverrides.Clear();
            _deferredNormalDialogue = null;
            _deferredNormalSpeaker = null;
            _deferredNormalNpcName = null;
            _deferredNormalAfterDialogues = null;
            _openingDeferredNormalDialogue = false;

            // Before EvaluateAllOverworldSprites, so this morning's sprites use yesterday's roll.
            this.StartCarryOverMornings();

            this.Monitor.Log($"[TRIGGER-DIAG] OnDayStarted fired. _triggeredData={_triggeredData != null}, Portraits={PackManager?.Portraits?.Count ?? -1}", LogLevel.Debug);

            if (_triggeredData == null || PackManager?.Portraits == null)
            {
                this.EvaluateAllOverworldSprites();
                return;
            }

            foreach (var kvp in PackManager.Portraits)
            {
                string npcName = kvp.Key;
                var portrait = kvp.Value;

                if (portrait.VariantTriggers == null || portrait.VariantTriggers.Count == 0)
                    continue;

                // Heart triggers belong to APF's own variant logic.
                if (portrait.IsContentPatcherControlled)
                    continue;

                if (!IsNpcEnabled(npcName) || !IsGrowthEnabled(npcName))
                    continue;

                // Resolve from the real game state, ignoring any visual-only GMCM lock.
                string variant = DetermineVariant(portrait, ignoreLockedVariant: true);
                this.Monitor.Log($"[TRIGGER-DIAG] {npcName}: triggers={portrait.VariantTriggers.Count}, variant='{variant}', hearts={Game1.player.getFriendshipHeartLevelForNPC(npcName)}", LogLevel.Debug);
                if (string.IsNullOrEmpty(variant))
                    continue;

                // Check if this variant has a "once" trigger that hasn't fired yet
                bool hasTrigger = portrait.VariantTriggers.TryGetValue(variant, out var trigger);
                string triggerText = hasTrigger ? this.ResolveTriggerDialogue(npcName, trigger) : "";
                this.Monitor.Log($"[TRIGGER-DIAG] {npcName}: hasTrigger={hasTrigger}, mode={trigger?.Mode}, isTriggered={_triggeredData.IsTriggered(npcName, variant)}, dialogueKey='{trigger?.DialogueKey}'", LogLevel.Debug);
                if (hasTrigger
                    && string.Equals(trigger.Mode, "once", StringComparison.OrdinalIgnoreCase)
                    && !_triggeredData.IsTriggered(npcName, variant)
                    && !string.IsNullOrWhiteSpace(triggerText))
                {
                    // Also handle combined: if variant is "Summer_Hearts4", the trigger key
                    // might be just "Hearts4". Check the individual parts too.
                    _pendingTriggerDialogue[npcName] = triggerText;
                    _pendingTriggerVariant[npcName] = variant;
                    if (trigger.BodyChange != null && IsTransitionEnabled(npcName))
                        _pendingBodyChange[npcName] = trigger.BodyChange;
                    MarkLowerHeartTriggersAsSeen(portrait, npcName);
                    this.Monitor.Log($"[TRIGGER] Queued one-time dialogue for {npcName} variant={variant} (bodyChange={trigger.BodyChange != null})", LogLevel.Debug);
                    continue;
                }

                // If the full variant didn't match, check component parts for combined variants
                int sep = variant.IndexOf('_');
                if (sep > 0)
                {
                    string heartsPart = variant.Substring(sep + 1);
                    string contextPart = variant.Substring(0, sep);

                    // Hearts triggers are the primary use case for "once"
                    if (portrait.VariantTriggers.TryGetValue(heartsPart, out trigger)
                        && string.Equals(trigger.Mode, "once", StringComparison.OrdinalIgnoreCase)
                        && !_triggeredData.IsTriggered(npcName, heartsPart)
                        && !string.IsNullOrWhiteSpace(triggerText = this.ResolveTriggerDialogue(npcName, trigger)))
                    {
                        _pendingTriggerDialogue[npcName] = triggerText;
                        _pendingTriggerVariant[npcName] = heartsPart;
                        if (trigger.BodyChange != null && IsTransitionEnabled(npcName))
                            _pendingBodyChange[npcName] = trigger.BodyChange;
                        MarkLowerHeartTriggersAsSeen(portrait, npcName);
                        this.Monitor.Log($"[TRIGGER] Queued one-time dialogue for {npcName} variant={heartsPart} (from combined {variant}, bodyChange={trigger.BodyChange != null})", LogLevel.Debug);
                        continue;
                    }

                    if (portrait.VariantTriggers.TryGetValue(contextPart, out trigger)
                        && string.Equals(trigger.Mode, "once", StringComparison.OrdinalIgnoreCase)
                        && !_triggeredData.IsTriggered(npcName, contextPart)
                        && !string.IsNullOrWhiteSpace(triggerText = this.ResolveTriggerDialogue(npcName, trigger)))
                    {
                        _pendingTriggerDialogue[npcName] = triggerText;
                        _pendingTriggerVariant[npcName] = contextPart;
                        if (trigger.BodyChange != null && IsTransitionEnabled(npcName))
                            _pendingBodyChange[npcName] = trigger.BodyChange;
                        this.Monitor.Log($"[TRIGGER] Queued one-time dialogue for {npcName} variant={contextPart} (from combined {variant}, bodyChange={trigger.BodyChange != null})", LogLevel.Debug);
                    }
                }

                // Mark all lower heart thresholds as triggered (catch-up for loaded saves)
                MarkLowerHeartTriggersAsSeen(portrait, npcName);
            }

            this.EvaluateAllOverworldSprites();
        }

        private void OnWarped(object sender, WarpedEventArgs e)
        {
            this.EvaluateAllOverworldSprites();
            this.CheckCarryOverMornings();
        }

        private void OnTimeChanged(object sender, TimeChangedEventArgs e)
        {
            this.CheckCarryOverMornings();
        }

        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            _carryOverMorning.Clear();
            VariantAssets.Clear();
            _variantAssetsDirty = false;

            // Left mid-event: put the event roots back to normal.
            EventKind previous = EventOutfits.Current;
            EventOutfits.Reset();
            if (previous != EventKind.None)
                this.RefreshEventRoots(previous, EventKind.None);
        }

        /// <summary>Set when a portrait asset was invalidated mid-day: re-roll the overworld sprites on the next tick.</summary>
        private static bool _variantAssetsDirty;

        /// <summary>
        /// A portrait asset changed (e.g. Content Patcher turned an outfit on or off after a GMCM change): forget which
        /// sub-variants exist, and re-roll the overworld sprites once on the next tick so they follow.
        /// Doesn't loop: re-rolling only ever invalidates Characters/* assets.
        /// </summary>
        private void OnAssetsInvalidated(object sender, AssetsInvalidatedEventArgs e)
        {
            foreach (IAssetName name in e.NamesWithoutLocale)
            {
                if (name.StartsWith("Portraits/"))
                {
                    VariantAssets.Clear();
                    _variantAssetsDirty = true;
                    return;
                }
            }
        }

        // ====================================================================
        // EVENTS (VariantEvents: cutscene / festival behaviour per root)
        // ====================================================================

        /// <summary>
        /// Before the game updates: if the player started or finished watching a cutscene or festival, refresh the
        /// sprite roots that behave differently during it. Only a reference/flag check per tick.
        /// </summary>
        private void OnUpdateTicking(object sender, UpdateTickingEventArgs e)
        {
            // Loaded textures are shared by all split-screen players, so only the main screen drives them.
            if (!EventOutfits.HasAny || !Context.IsWorldReady || Context.ScreenId != 0)
                return;

            if (EventOutfits.Update(out EventKind previous))
                this.RefreshEventRoots(previous, EventOutfits.Current);
        }

        /// <summary>
        /// Invalidate every Characters/{NPC}_{Root} whose VariantEvents settings differ between the two event kinds.
        /// SMAPI reloads each one (running <see cref="OnAssetRequested"/> with the new event kind) and copies the result
        /// into the already-loaded texture, so the overworld NPC and every event actor using that sheet update together,
        /// even if the new sheet has a different size.
        /// </summary>
        private void RefreshEventRoots(EventKind previous, EventKind current)
        {
            int refreshed = 0;
            foreach (var (npc, root) in EventOutfits.Roots)
            {
                if (!PackManager.Portraits.TryGetValue(npc, out var portrait))
                    continue;

                if (EventOutfits.GetBehavior(portrait, root, previous, this.Helper.ModRegistry) == null
                    && EventOutfits.GetBehavior(portrait, root, current, this.Helper.ModRegistry) == null)
                    continue;

                if (this.Helper.GameContent.InvalidateCache(EventOutfits.RootAsset(npc, root)))
                    refreshed++;
            }

            string eventId = Game1.CurrentEvent?.id;
            this.Monitor.Log(
                $"[EVENTS] {previous} → {current}{(current != EventKind.None && eventId != null ? $" (event '{eventId}')" : "")}: refreshed {refreshed} loaded sprite root(s).",
                LogLevel.Debug);
        }

        /// <summary>Last portrait-skip log line key, so APF mode's per-tick check doesn't spam the log.</summary>
        private static string _lastPortraitSkipLog;

        /// <summary>
        /// Pick the portrait variant for a root: the rolled Rules sub-variant, or the root itself while a
        /// cutscene/festival with <c>SkipRolledPortrait</c> is running.
        /// </summary>
        private static string ResolvePortraitRoll(PortraitDefinition portrait, string root)
        {
            if (!string.IsNullOrEmpty(root))
            {
                EventKind kind = EventOutfits.ReadLive();
                if (EventOutfits.GetBehavior(portrait, root, kind, Instance.Helper.ModRegistry)?.SkipRolledPortrait == true)
                {
                    // APF mode re-checks every tick, so only log when it changes.
                    string logKey = $"{portrait.Target}|{root}|{kind}";
                    if (logKey != _lastPortraitSkipLog)
                    {
                        _lastPortraitSkipLog = logKey;
                        ModMonitor.Log($"[EVENTS] {portrait.Target}/{root}: {kind}, SkipRolledPortrait → using the root's own portrait.", LogLevel.Debug);
                    }
                    return root;
                }
            }

            return ApplyEvaluatedVariant(portrait, root);
        }

        // ====================================================================
        // CARRY-OVER (e.g. pyjamas: wake up in what you went to bed in)
        // ====================================================================

        private static string CarryOverKey(string npc, string root) => $"{npc}|{root}";

        /// <summary>
        /// At the start of a day, put every CarryOver rule (NPC + root) into its morning stretch, so
        /// the first time that root is active today it uses yesterday's roll. Must run before
        /// <see cref="EvaluateAllOverworldSprites"/> so the overworld sprite gets yesterday's roll too.
        /// </summary>
        private void StartCarryOverMornings()
        {
            _carryOverMorning.Clear();
            if (PackManager?.Rules == null || _evaluator == null)
                return;

            foreach (var kvp in PackManager.Rules)
            {
                string npc = kvp.Key;
                if (!PackManager.Portraits.ContainsKey(npc))
                    continue;

                foreach (var rule in kvp.Value)
                {
                    if (!rule.CarryOver || string.IsNullOrWhiteSpace(rule.Root))
                        continue;

                    string key = CarryOverKey(npc, rule.Root);
                    if (_carryOverMorning.ContainsKey(key))
                        continue;

                    _carryOverMorning[key] = (npc, rule.Root);

                    if (VariantEvaluator.NormalizeDayOffset(-1) == 0)
                    {
                        this.Monitor.Log($"[CARRY-OVER] {npc}/{rule.Root}: first day of the save, morning uses today's roll.", LogLevel.Debug);
                    }
                    else
                    {
                        string yesterday = _evaluator.Evaluate(npc, rule.Root, -1) ?? rule.Root;
                        this.Monitor.Log($"[CARRY-OVER] {npc}/{rule.Root}: morning uses yesterday's roll '{yesterday}'", LogLevel.Debug);
                    }
                }
            }
        }

        /// <summary>
        /// End the morning stretch for every CarryOver NPC who is no longer wearing that root
        /// (their overworld sprite isn't Characters/{NPC}_{Root} any more), and switch them to today's roll.
        /// Cheap: only looks at pairs still in their morning, so it does nothing once everyone is dressed.
        /// </summary>
        private void CheckCarryOverMornings()
        {
            if (_carryOverMorning.Count == 0 || !Context.IsWorldReady || PackManager == null)
                return;

            // Event and festival sprites don't mean "she got dressed".
            if (Game1.eventUp || Game1.isFestival())
                return;

            foreach (var entry in _carryOverMorning.ToList())
            {
                var (npcName, root) = entry.Value;

                if (!PackManager.Portraits.TryGetValue(npcName, out var portrait))
                {
                    _carryOverMorning.Remove(entry.Key);
                    continue;
                }

                NPC npc = Game1.getCharacterFromName(npcName);
                if (npc?.currentLocation == null)
                    continue;

                if (IsWearingRoot(npc, root))
                    continue;

                _carryOverMorning.Remove(entry.Key);
                string today = ApplyEvaluatedVariant(portrait, root);
                this.Monitor.Log($"[CARRY-OVER] {npcName}/{root}: morning over → today's roll '{today}'", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Whether the NPC's overworld sprite is currently this root: Characters/{TextureName}_{Root},
        /// or one of its sub-variants (Characters/{TextureName}_{Root}_X).
        /// </summary>
        private static bool IsWearingRoot(NPC npc, string root)
        {
            string spriteAsset = npc.Sprite?.textureName?.Value;
            string suffix = StripPrefix(spriteAsset, "Characters", NPC.getTextureNameForCharacter(npc.Name))
                ?? StripPrefix(spriteAsset, "Characters", npc.Name);

            if (suffix == null)
                return false;

            return string.Equals(suffix, root, StringComparison.OrdinalIgnoreCase)
                || suffix.StartsWith(root + "_", StringComparison.OrdinalIgnoreCase);
        }

        private void EvaluateAllOverworldSprites()
        {
            if (PackManager == null || PackManager.Rules.Count == 0)
                return;

            foreach (var kvp in PackManager.Rules)
            {
                string npcName = kvp.Key;
                if (!IsNpcEnabled(npcName))
                    continue;

                if (!PackManager.Portraits.TryGetValue(npcName, out var portrait))
                    continue;

                var roots = kvp.Value
                    .Select(rule => rule.Root)
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                foreach (string root in roots)
                    ApplyEvaluatedVariant(portrait, root);
            }
        }

        /// <summary>
        /// When a player loads a save with e.g. 6 hearts, mark Hearts2 and Hearts4 as triggered
        /// so only the current threshold's dialogue fires (if applicable).
        /// </summary>
        private void MarkLowerHeartTriggersAsSeen(PortraitDefinition portrait, string npcName)
        {
            int playerHearts = Game1.player.getFriendshipHeartLevelForNPC(npcName);

            // Find the highest matching heart threshold
            int highestThreshold = -1;
            foreach (var vtKvp in portrait.VariantTriggers)
            {
                string key = vtKvp.Key;
                if (key.StartsWith("Hearts", StringComparison.OrdinalIgnoreCase) && key.Length > 6
                    && int.TryParse(key.Substring(6), out int threshold)
                    && threshold > 0 && playerHearts >= threshold
                    && threshold > highestThreshold)
                {
                    highestThreshold = threshold;
                }
            }

            // Mark all thresholds below the highest as triggered (they were "passed")
            if (highestThreshold > 0)
            {
                foreach (var vtKvp in portrait.VariantTriggers)
                {
                    string key = vtKvp.Key;
                    if (key.StartsWith("Hearts", StringComparison.OrdinalIgnoreCase) && key.Length > 6
                        && int.TryParse(key.Substring(6), out int threshold)
                        && threshold > 0 && threshold < highestThreshold
                        && !_triggeredData.IsTriggered(npcName, key))
                    {
                        _triggeredData.MarkTriggered(npcName, key);
                        this.Monitor.Log($"[TRIGGER] Auto-marked {npcName} {key} as seen (below current threshold {highestThreshold})", LogLevel.Trace);
                    }
                }
            }
        }

        /// <summary>Resolve a trigger's localized text and apply its presentation-only expression.</summary>
        private string ResolveTriggerDialogue(string npcName, VariantTrigger trigger)
        {
            if (trigger == null)
                return "";

            string text = "";
            if (!string.IsNullOrWhiteSpace(trigger.DialogueKey))
            {
                if (PackManager.ContentProviders.TryGetValue(npcName, out var pack))
                {
                    if (pack.Translation.ContainsKey(trigger.DialogueKey))
                        text = pack.Translation.Get(trigger.DialogueKey).ToString();
                    else
                        this.Monitor.Log($"[TRIGGER] Missing i18n key '{trigger.DialogueKey}' in content pack '{pack.Manifest.UniqueID}'.", LogLevel.Warn);
                }
                else
                {
                    this.Monitor.Log($"[TRIGGER] Can't resolve i18n key '{trigger.DialogueKey}' for {npcName}: content pack provider not found.", LogLevel.Warn);
                }
            }

            if (string.IsNullOrWhiteSpace(text))
                text = trigger.Dialogue ?? "";

            if (!string.IsNullOrWhiteSpace(text) && trigger.Expression is >= 0)
                text = $"{text} ${trigger.Expression.Value}";

            return text;
        }

        /// <summary>
        /// Re-check variant triggers for a single NPC at dialogue time.
        /// Catches hearts that changed after DayStarted (e.g. via CJB Cheats or gifting).
        /// </summary>
        private void CheckTriggersForNpc(string npcName, PortraitDefinition portrait)
        {
            if (_triggeredData == null || portrait.VariantTriggers == null || portrait.VariantTriggers.Count == 0)
                return;

            if (portrait.IsContentPatcherControlled)
                return;

            if (!IsNpcEnabled(npcName) || !IsGrowthEnabled(npcName))
                return;

            string variant = DetermineVariant(portrait, ignoreLockedVariant: true);
            if (string.IsNullOrEmpty(variant))
                return;

            // Try full variant key, then component parts
            string[] keysToTry;
            int sep = variant.IndexOf('_');
            if (sep > 0)
                keysToTry = new[] { variant, variant.Substring(sep + 1), variant.Substring(0, sep) };
            else
                keysToTry = new[] { variant };

            foreach (string key in keysToTry)
            {
                string triggerText;
                if (portrait.VariantTriggers.TryGetValue(key, out var trigger)
                    && string.Equals(trigger.Mode, "once", StringComparison.OrdinalIgnoreCase)
                    && !_triggeredData.IsTriggered(npcName, key)
                    && !string.IsNullOrWhiteSpace(triggerText = this.ResolveTriggerDialogue(npcName, trigger)))
                {
                    _pendingTriggerDialogue[npcName] = triggerText;
                    _pendingTriggerVariant[npcName] = key;
                    if (trigger.BodyChange != null && IsTransitionEnabled(npcName))
                        _pendingBodyChange[npcName] = trigger.BodyChange;
                    this.Monitor.Log($"[TRIGGER] Late-queued trigger for {npcName} variant={key} (hearts changed after DayStarted)", LogLevel.Info);

                    MarkLowerHeartTriggersAsSeen(portrait, npcName);
                    return;
                }
            }
        }

        /// <summary>
        /// Intercept the game's normal NPC dialogue opening so a newly reached
        /// variant trigger is placed on top of the existing dialogue stack first.
        /// </summary>
        private static void Game1_drawDialogue_Prefix(NPC speaker)
        {
            Instance?.InjectTriggerBeforeNormalDialogue(speaker);
        }

        private void InjectTriggerBeforeNormalDialogue(NPC speaker)
        {
            if (_openingDeferredNormalDialogue
                || _deferredNormalDialogue != null
                || speaker == null
                || !Context.IsWorldReady
                || Game1.eventUp
                || speaker.CurrentDialogue.Count == 0)
            {
                return;
            }

            string npcName = speaker.Name;
            if (!PackManager.Portraits.TryGetValue(npcName, out var portrait)
                || portrait.IsContentPatcherControlled
                || !IsNpcEnabled(npcName)
                || !IsGrowthEnabled(npcName))
            {
                return;
            }

            if (!_pendingTriggerDialogue.ContainsKey(npcName))
                this.CheckTriggersForNpc(npcName, portrait);

            if (!_pendingTriggerDialogue.TryGetValue(npcName, out string triggerText))
                return;

            string triggerVariant = _pendingTriggerVariant.GetValueOrDefault(npcName, "");
            Dialogue normalDialogue = speaker.CurrentDialogue.Peek();

            try
            {
                var triggerDialogue = new Dialogue(speaker, $"apf_trigger_{triggerVariant}", triggerText);

                // Keep the original dialogue underneath the trigger. DialogueBox.closeDialogue
                // pops the trigger, then OnUpdateTicked explicitly opens the original next tick.
                speaker.CurrentDialogue.Push(triggerDialogue);
                _deferredNormalSpeaker = speaker;
                _deferredNormalDialogue = normalDialogue;
                _deferredNormalNpcName = npcName;
                _deferredNormalAfterDialogues = Game1.afterDialogues;

                // The game normally invokes this callback when the dialogue box closes.
                // Hold it back so it runs after the preserved normal dialogue, not after
                // the newly inserted one-time trigger.
                Game1.afterDialogues = null;

                if (_pendingBodyChange.TryGetValue(npcName, out var bodyChange))
                {
                    _activeBodyChange[npcName] = bodyChange;
                    _pendingBodyChange.Remove(npcName);
                }

                if (!string.IsNullOrEmpty(triggerVariant))
                    _triggeredData?.MarkTriggered(npcName, triggerVariant);

                _pendingTriggerDialogue.Remove(npcName);
                _pendingTriggerVariant.Remove(npcName);

                this.Monitor.Log($"[TRIGGER] Inserted one-time dialogue before the normal dialogue for {npcName} (variant={triggerVariant}).", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"[TRIGGER] Failed to insert dialogue before the normal dialogue for {npcName}: {ex.Message}", LogLevel.Warn);
            }
        }

        private void OnSaving(object sender, SavingEventArgs e)
        {
            if (_triggeredData != null)
                this.Helper.Data.WriteSaveData(TRIGGER_SAVE_KEY, _triggeredData);
        }

        // ====================================================================
        // CONTENT API — Texture loading + DDF injection
        // ====================================================================

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            // Merge an active SVO-style winner over the root character sheet late in
            // the pipeline so extended animation rows from other edits are preserved.
            if (e.NameWithoutLocale.Name.StartsWith("Characters/", StringComparison.OrdinalIgnoreCase))
            {
                // Cutscene / festival running: this root may use its own sheet or an event sheet instead.
                if (this.TryApplyEventSprite(e))
                    return;

                foreach (var (npc, root) in ActiveVariantCache.Keys)
                {
                    string rootAsset = $"Characters/{npc}_{root}";
                    if (!e.NameWithoutLocale.Name.Equals(rootAsset, StringComparison.OrdinalIgnoreCase)
                        || !ActiveVariantCache.TryGet(npc, root, out string winner)
                        || string.Equals(root, winner, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string winnerAsset = $"Characters/{npc}_{winner}";
                    var winnerName = this.Helper.GameContent.ParseAssetName(winnerAsset);
                    if (!this.Helper.GameContent.DoesAssetExist<Texture2D>(winnerName))
                        continue;

                    e.Edit(asset =>
                    {
                        var image = asset.AsImage();
                        var variant = this.Helper.GameContent.Load<Texture2D>(winnerAsset);
                        image.ExtendImage(
                            minWidth: Math.Max(image.Data.Width, variant.Width),
                            minHeight: Math.Max(image.Data.Height, variant.Height));
                        image.PatchImage(variant);
                    }, AssetEditPriority.Late);
                    return;
                }
            }

            // ── DDF data injection (only if any content pack defines DDF settings) ──
            if (_hasDdfConfig && e.NameWithoutLocale.IsEquivalentTo(DDF_ASSET))
            {
                e.Edit(asset =>
                {
                    // Try the dictionary-of-object approach first (older DDF versions).
                    // DDF Continued may use typed values (DialogueDisplayData) which cannot
                    // be cast to IDictionary<string, object>.  In that case we fall back to
                    // reflection so we don't crash or block other mods' DDF edits.
                    try
                    {
                        var data = asset.AsDictionary<string, object>();
                        InjectDdfEntries(data.Data);
                    }
                    catch (InvalidCastException)
                    {
                        InjectDdfReflection(asset.Data);
                    }
                }, AssetEditPriority.Early);
            }
        }

        /// <summary>
        /// If a cutscene or festival is running and this is a root with VariantEvents for it, apply that behaviour:
        /// paint the event <c>Sprite</c> over the root, or leave the root untouched (<c>SkipRolledSprite</c>).
        /// Returns true if the rolled sub-variant overlay must not be applied.
        /// </summary>
        private bool TryApplyEventSprite(AssetRequestedEventArgs e)
        {
            if (!EventOutfits.HasAny || !EventOutfits.TryGetRoot(e.NameWithoutLocale.Name, out string npc, out string root))
                return false;

            // The tracked kind (main screen), not the live one: the sheets are shared between split-screen players,
            // and RefreshEventRoots reloads them as soon as the kind changes.
            EventKind kind = EventOutfits.Current;
            if (kind == EventKind.None || !PackManager.Portraits.TryGetValue(npc, out var portrait))
                return false;

            var behavior = EventOutfits.GetBehavior(portrait, root, kind, this.Helper.ModRegistry);
            if (behavior == null)
                return false;

            string sprite = behavior.Sprite?.Trim().Replace('\\', '/');
            if (!string.IsNullOrEmpty(sprite) && !sprite.Equals(e.NameWithoutLocale.Name, StringComparison.OrdinalIgnoreCase))
            {
                var spriteName = this.Helper.GameContent.ParseAssetName(sprite);
                if (this.Helper.GameContent.DoesAssetExist<Texture2D>(spriteName))
                {
                    e.Edit(asset =>
                    {
                        var image = asset.AsImage();
                        var eventSheet = this.Helper.GameContent.Load<Texture2D>(spriteName);
                        image.ExtendImage(
                            minWidth: Math.Max(image.Data.Width, eventSheet.Width),
                            minHeight: Math.Max(image.Data.Height, eventSheet.Height));
                        image.PatchImage(eventSheet);
                    }, AssetEditPriority.Late);

                    this.Monitor.Log($"[EVENTS] {npc}/{root}: {kind} → using event sheet '{sprite}'.", LogLevel.Trace);
                    return true;
                }

                this.Monitor.Log($"[EVENTS] {npc}/{root}: event sheet '{sprite}' doesn't exist, ignoring it.", LogLevel.Trace);
            }

            if (behavior.SkipRolledSprite)
            {
                this.Monitor.Log($"[EVENTS] {npc}/{root}: {kind} → SkipRolledSprite, using the root's own sheet.", LogLevel.Trace);
                return true;
            }

            return false;
        }

        /// <summary>Inject DDF entries using the plain dictionary approach (old DDF API).</summary>
        private void InjectDdfEntries(IDictionary<string, object> data)
        {
            foreach (var kvp in PackManager.Portraits)
            {
                var portrait = kvp.Value;
                if (portrait.Ddf == null)
                    continue;

                var ddf = portrait.Ddf;
                bool bottom = ddf.Position?.ToLowerInvariant() == "bottom";

                // Build a COMPLETE DDF entry (portrait + dialogue box + name + hearts + jewel + button).
                // If only "portrait" is set, DDF Continued overrides the "default" layout for this NPC
                // with a minimal config, losing the dialogue box, name label, and hearts display.
                var entry = new Dictionary<string, object>
                {
                    ["packName"] = this.ModManifest.UniqueID,
                    ["height"] = 384,
                    ["width"] = 900,
                    ["xOffset"] = 508,
                    ["yOffset"] = 0,
                    ["portrait"] = new Dictionary<string, object>
                    {
                        ["xOffset"] = ddf.XOffset,
                        ["yOffset"] = ddf.YOffset,
                        ["w"] = portrait.FrameSize,
                        ["h"] = portrait.FrameSize,
                        ["scale"] = ddf.Scale,
                        ["bottom"] = bottom,
                        ["right"] = false
                    },
                    ["dialogue"] = new Dictionary<string, object>
                    {
                        ["xOffset"] = ddf.BoxXOffset ?? 24,
                        ["yOffset"] = ddf.BoxYOffset ?? 12,
                        ["width"] = ddf.BoxWidth ?? 880
                    },
                    ["name"] = new Dictionary<string, object>
                    {
                        ["xOffset"] = 180,
                        ["yOffset"] = -440,
                        ["scroll"] = true,
                        ["right"] = false,
                        ["bottom"] = true,
                        ["centered"] = true,
                        ["scrollType"] = 0
                    },
                    ["jewel"] = new Dictionary<string, object>
                    {
                        ["xOffset"] = -48,
                        ["yOffset"] = -60,
                        ["right"] = true
                    },
                    ["button"] = new Dictionary<string, object>
                    {
                        ["xOffset"] = -40,
                        ["yOffset"] = -48,
                        ["right"] = true,
                        ["bottom"] = true
                    },
                    ["hearts"] = new Dictionary<string, object>
                    {
                        ["xOffset"] = 8,
                        ["yOffset"] = -24,
                        ["bottom"] = true,
                        ["showEmptyHearts"] = true,
                        ["heartsPerRow"] = 14
                    },
                    ["scrollTexts"] = new List<object>(),
                    ["images"] = new List<object>(),
                    ["texts"] = new List<object> { new Dictionary<string, object> { ["disabled"] = true } },
                    ["dividers"] = new List<object> { new Dictionary<string, object> { ["disabled"] = true, ["xOffset"] = 0, ["height"] = 0 } }
                };

                if (ddf.BoxHeight.HasValue)
                    ((Dictionary<string, object>)entry["dialogue"])["height"] = ddf.BoxHeight.Value;

                data[kvp.Key] = entry;
                this.Monitor.Log($"Injected DDF config for {kvp.Key}", LogLevel.Trace);
            }
        }

        /// <summary>Inject DDF entries via reflection for DDF Continued's typed dictionary.</summary>
        private void InjectDdfReflection(object rawDict)
        {
            try
            {
                var dictType = rawDict.GetType();
                var genericArgs = dictType.GetGenericArguments();
                if (genericArgs.Length < 2) return;

                var valueType = genericArgs[1];
                // Use the non-generic IDictionary interface to add entries
                var dict = rawDict as System.Collections.IDictionary;
                if (dict == null) return;

                foreach (var kvp in PackManager.Portraits)
                {
                    var portrait = kvp.Value;
                    if (portrait.Ddf == null)
                        continue;

                    var ddf = portrait.Ddf;
                    bool bottom = ddf.Position?.ToLowerInvariant() == "bottom";

                    // Create a typed DDF entry via reflection with FULL layout
                    var entry = Activator.CreateInstance(valueType);

                    // Top-level dialogue box dimensions
                    TrySetMember(entry, "packName", this.ModManifest.UniqueID);
                    TrySetMember(entry, "height", 384);
                    TrySetMember(entry, "width", 900);
                    TrySetMember(entry, "xOffset", 508);
                    TrySetMember(entry, "yOffset", 0);

                    // Portrait sub-object
                    PopulateDdfSubObject(entry, valueType, "portrait", sub =>
                    {
                        TrySetMember(sub, "xOffset", ddf.XOffset);
                        TrySetMember(sub, "yOffset", ddf.YOffset);
                        TrySetMember(sub, "w", portrait.FrameSize);
                        TrySetMember(sub, "h", portrait.FrameSize);
                        TrySetMember(sub, "scale", (float)ddf.Scale);
                        TrySetMember(sub, "bottom", bottom);
                        TrySetMember(sub, "right", false);
                    });

                    // Dialogue text area
                    PopulateDdfSubObject(entry, valueType, "dialogue", sub =>
                    {
                        TrySetMember(sub, "xOffset", ddf.BoxXOffset ?? 24);
                        TrySetMember(sub, "yOffset", ddf.BoxYOffset ?? 12);
                        TrySetMember(sub, "width", ddf.BoxWidth ?? 880);
                        if (ddf.BoxHeight.HasValue) TrySetMember(sub, "height", ddf.BoxHeight.Value);
                    });

                    // Name label (scroll banner)
                    PopulateDdfSubObject(entry, valueType, "name", sub =>
                    {
                        TrySetMember(sub, "xOffset", 180);
                        TrySetMember(sub, "yOffset", -440);
                        TrySetMember(sub, "scroll", true);
                        TrySetMember(sub, "right", false);
                        TrySetMember(sub, "bottom", true);
                        TrySetMember(sub, "centered", true);
                        TrySetMember(sub, "scrollType", 0);
                    });

                    // Jewel icon
                    PopulateDdfSubObject(entry, valueType, "jewel", sub =>
                    {
                        TrySetMember(sub, "xOffset", -48);
                        TrySetMember(sub, "yOffset", -60);
                        TrySetMember(sub, "right", true);
                    });

                    // Button
                    PopulateDdfSubObject(entry, valueType, "button", sub =>
                    {
                        TrySetMember(sub, "xOffset", -40);
                        TrySetMember(sub, "yOffset", -48);
                        TrySetMember(sub, "right", true);
                        TrySetMember(sub, "bottom", true);
                    });

                    // Hearts
                    PopulateDdfSubObject(entry, valueType, "hearts", sub =>
                    {
                        TrySetMember(sub, "xOffset", 8);
                        TrySetMember(sub, "yOffset", -24);
                        TrySetMember(sub, "bottom", true);
                        TrySetMember(sub, "showEmptyHearts", true);
                        TrySetMember(sub, "heartsPerRow", 14);
                    });

                    dict[kvp.Key] = entry;
                    this.Monitor.Log($"Injected DDF config for {kvp.Key} (via reflection)", LogLevel.Trace);
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"DDF reflection injection failed: {ex.Message}. Portrait display may use default settings.", LogLevel.Warn);
            }
        }

        private static System.Reflection.MemberInfo FindWritableMember(Type type, string name)
        {
            foreach (var p in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.CanWrite) return p;
            foreach (var f in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }

        private static Type GetMemberType(System.Reflection.MemberInfo m) => m switch
        {
            System.Reflection.PropertyInfo p => p.PropertyType,
            System.Reflection.FieldInfo f => f.FieldType,
            _ => typeof(object)
        };

        private static void SetMember(object obj, System.Reflection.MemberInfo m, object value)
        {
            switch (m)
            {
                case System.Reflection.PropertyInfo p: p.SetValue(obj, value); break;
                case System.Reflection.FieldInfo f: f.SetValue(obj, value); break;
            }
        }

        private static object GetMemberValue(object obj, System.Reflection.MemberInfo m) => m switch
        {
            System.Reflection.PropertyInfo p => p.GetValue(obj),
            System.Reflection.FieldInfo f => f.GetValue(obj),
            _ => null
        };

        private static void TrySetMember(object obj, string name, object value)
        {
            var m = FindWritableMember(obj.GetType(), name);
            if (m == null) return;
            try
            {
                var target = GetMemberType(m);
                var converted = Convert.ChangeType(value, target);
                SetMember(obj, m, converted);
            }
            catch { /* skip unknown fields silently */ }
        }

        /// <summary>Find a sub-object member on a DDF entry, instantiate it, populate via callback, and assign.</summary>
        private static void PopulateDdfSubObject(object entry, Type entryType, string memberName, Action<object> populate)
        {
            var member = FindWritableMember(entryType, memberName);
            if (member == null) return;
            var subType = GetMemberType(member);
            var sub = Activator.CreateInstance(subType);
            populate(sub);
            SetMember(entry, member, sub);
        }

        // ====================================================================
        // DDF RUNTIME PATCH — Override cached portrait W/H at runtime
        // ====================================================================

        /// <summary>
        /// One-time resolution of DDF Reflection targets. Called lazily on first use.
        /// Caches Type, PropertyInfo, and MemberInfo so subsequent ticks cost zero Reflection lookups.
        /// </summary>
        private static void ResolveDdfReflection()
        {
            if (_ddfReflectionResolved) return;
            _ddfReflectionResolved = true;

            try
            {
                var patchesType = AccessTools.TypeByName("DialogueDisplayFramework.Framework.DialogueBoxPatches");
                if (patchesType == null) return;

                _ddfActiveDataProp = patchesType.GetProperty("ActiveData",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                _ddfCacheActiveDataMethod = AccessTools.Method(
                    patchesType,
                    "UpdateDialogueBoxSize",
                    new[] { typeof(DialogueBox) });

                if (_ddfCacheActiveDataMethod == null)
                {
                    _ddfCacheActiveDataMethod = patchesType
                        .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                        .FirstOrDefault(method =>
                            (method.Name.IndexOf("CacheActiveData", StringComparison.OrdinalIgnoreCase) >= 0
                                || method.Name.IndexOf("UpdateDialogueBoxSize", StringComparison.OrdinalIgnoreCase) >= 0)
                            && method.GetParameters().Length == 1
                            && method.GetParameters()[0].ParameterType == typeof(DialogueBox));
                }
            }
            catch (Exception ex)
            {
                ModMonitor?.Log($"DDF reflection resolution failed: {ex.Message}", LogLevel.Trace);
            }
        }

        /// <summary>
        /// Cache MemberInfo lookups for the DDF Portrait sub-object type (once per type).
        /// </summary>
        private static void ResolveDdfPortraitMembers(object pd)
        {
            if (_ddfPortraitMembers != null) return;

            var type = pd.GetType();
            _ddfPortraitMembers = new Dictionary<string, System.Reflection.MemberInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in new[] { "W", "H", "Scale", "XOffset", "YOffset", "Bottom", "Right", "TexturePath" })
            {
                var m = FindWritableMember(type, name);
                if (m != null) _ddfPortraitMembers[name] = m;
            }
        }

        /// <summary>
        /// Get the current DDF Portrait sub-object via cached Reflection. Returns null if DDF
        /// is not installed or ActiveData is not yet available.
        /// </summary>
        private static object GetDdfPortraitObject()
        {
            ResolveDdfReflection();
            if (_ddfActiveDataProp == null) return null;

            var activeData = _ddfActiveDataProp.GetValue(null);
            if (activeData == null) return null;

            // Cache the Portrait property lookup on first successful access
            if (_ddfPortraitProp == null)
            {
                _ddfPortraitProp = activeData.GetType().GetProperty("Portrait");
                if (_ddfPortraitProp == null) return null;
            }

            return _ddfPortraitProp.GetValue(activeData);
        }

        private static void Ddf_CacheActiveData_Postfix()
        {
            try
            {
                if (Game1.activeClickableMenu is not DialogueBox dialogueBox
                    || dialogueBox.characterDialogue?.speaker == null
                    || PackManager == null)
                    return;

                string speakerName = dialogueBox.characterDialogue.speaker.Name;
                if (!PackManager.Portraits.ContainsKey(speakerName))
                    return;

                object portraitData = GetDdfPortraitObject();
                if (portraitData == null)
                    return;

                ResolveDdfPortraitMembers(portraitData);
                if (_ddfPortraitMembers.TryGetValue("TexturePath", out var texturePathMember))
                    SetMember(portraitData, texturePathMember, null);
            }
            catch (Exception ex)
            {
                ModMonitor?.Log($"DDFC TexturePath interception failed: {ex.Message}", LogLevel.Trace);
            }
        }

        /// <summary>
        /// Patch DDF's cached ActiveData.Portrait to use our frame size instead of
        /// whatever another CP mod (e.g. Skimpy Portraits) set. This ensures DDF's
        /// getSourceRectForStandardTileSheet call uses the correct W/H for our texture.
        /// </summary>
        private static void EnsureDdfPortraitSettings(PortraitDefinition portrait, string speakerName = null, DialogueBox dialogueBox = null)
        {
            if (portrait == null)
                return;

            if (_ddfPatchedThisSession && speakerName != null)
            {
                if (string.Equals(_ddfPatchedSessionNpc, speakerName, StringComparison.OrdinalIgnoreCase)
                    && (dialogueBox == null || ReferenceEquals(_ddfPatchedSessionBox, dialogueBox)))
                {
                    return;
                }
            }

            // Fast guard: if we already patched this exact object for this NPC, skip entirely.
            // Only costs one property getter + ReferenceEquals (no Reflection scanning).
            var pd = GetDdfPortraitObject();
            if (pd == null) return;

            if (_ddfPatchedNpc == portrait.Target && ReferenceEquals(pd, _ddfPatchedPortraitObj))
                return;

            try
            {
                // Ensure MemberInfo cache is populated for the portrait sub-object type
                ResolveDdfPortraitMembers(pd);

                // If we previously mutated a DIFFERENT Portrait object, restore it first
                // so we don't leave corrupted values in DDF's dictionary cache.
                RestoreDdfOriginals();

                // Save original values before mutating
                SaveDdfOriginals(pd);

                // Clear TexturePath so DDF uses speaker.Portrait (our texture)
                if (_ddfPortraitMembers.TryGetValue("TexturePath", out var tpMember))
                    SetMember(pd, tpMember, null);

                if (portrait.OverrideDdf && portrait.Ddf != null)
                {
                    // Override DDF portrait settings so source rect matches our frame size
                    SetCachedMember(pd, "W", portrait.FrameSize);
                    SetCachedMember(pd, "H", portrait.FrameSize);

                    // Auto-calculate correct scale: the displayed portrait should be ~1024 display pixels
                    // regardless of source frame size (512→Scale 2, 640→~1.6, 1024→Scale 1).
                    float correctScale = 1024f / portrait.FrameSize;
                    SetCachedMember(pd, "Scale", correctScale);
                    SetCachedMember(pd, "XOffset", portrait.Ddf.XOffset);
                    SetCachedMember(pd, "YOffset", portrait.Ddf.YOffset);

                    bool bottom = portrait.Ddf.Position?.ToLowerInvariant() == "bottom";
                    SetCachedMember(pd, "Bottom", bottom);
                    SetCachedMember(pd, "Right", false);
                    ModMonitor.Log($"Patched DDF ActiveData for {portrait.Target}: W={portrait.FrameSize}, H={portrait.FrameSize}, Scale={correctScale}", LogLevel.Debug);
                }
                else
                {
                    ModMonitor.Log($"Cleared DDF TexturePath for {portrait.Target} to enforce dynamic texture. Layout overrides skipped.", LogLevel.Trace);
                }

                _ddfPatchedNpc = portrait.Target;
                _ddfPatchedPortraitObj = pd;
                _ddfPatchedThisSession = true;
                _ddfPatchedSessionNpc = speakerName ?? portrait.Target;
                _ddfPatchedSessionBox = dialogueBox;
            }
            catch (Exception ex)
            {
                ModMonitor.Log($"DDF runtime patch failed: {ex.Message}", LogLevel.Trace);
            }
        }

        /// <summary>Set a value on the DDF Portrait object using cached MemberInfo.</summary>
        private static void SetCachedMember(object pd, string name, object value)
        {
            if (_ddfPortraitMembers == null || !_ddfPortraitMembers.TryGetValue(name, out var m))
                return;
            try
            {
                var target = GetMemberType(m);
                var converted = Convert.ChangeType(value, target);
                SetMember(pd, m, converted);
            }
            catch { /* skip unknown fields silently */ }
        }

        /// <summary>Save the original DDF portrait values before we mutate them.</summary>
        private static void SaveDdfOriginals(object pd)
        {
            if (_ddfPortraitMembers == null) return;

            try
            {
                int w = 64, h = 64;
                float scale = 1f;
                int xOff = 0, yOff = 0;
                bool bottom = false, right = false;
                string texPath = null;

                if (_ddfPortraitMembers.TryGetValue("W", out var wm)) w = Convert.ToInt32(GetMemberValue(pd, wm));
                if (_ddfPortraitMembers.TryGetValue("H", out var hm)) h = Convert.ToInt32(GetMemberValue(pd, hm));
                if (_ddfPortraitMembers.TryGetValue("Scale", out var sm)) scale = Convert.ToSingle(GetMemberValue(pd, sm));
                if (_ddfPortraitMembers.TryGetValue("XOffset", out var xm)) xOff = Convert.ToInt32(GetMemberValue(pd, xm));
                if (_ddfPortraitMembers.TryGetValue("YOffset", out var ym)) yOff = Convert.ToInt32(GetMemberValue(pd, ym));
                if (_ddfPortraitMembers.TryGetValue("Bottom", out var bm)) bottom = Convert.ToBoolean(GetMemberValue(pd, bm));
                if (_ddfPortraitMembers.TryGetValue("Right", out var rm)) right = Convert.ToBoolean(GetMemberValue(pd, rm));
                if (_ddfPortraitMembers.TryGetValue("TexturePath", out var tm)) texPath = GetMemberValue(pd, tm) as string;

                _ddfOriginalValues = (w, h, scale, xOff, yOff, bottom, right, texPath);
            }
            catch (Exception ex)
            {
                ModMonitor?.Log($"Failed to save DDF originals: {ex.Message}", LogLevel.Trace);
                _ddfOriginalValues = null;
            }
        }

        /// <summary>Restore the previously saved DDF portrait values on the object we mutated.</summary>
        private static void RestoreDdfOriginals()
        {
            if (_ddfPatchedPortraitObj == null || !_ddfOriginalValues.HasValue)
                return;

            var pd = _ddfPatchedPortraitObj;
            var orig = _ddfOriginalValues.Value;

            try
            {
                SetCachedMember(pd, "W", orig.W);
                SetCachedMember(pd, "H", orig.H);
                SetCachedMember(pd, "Scale", orig.Scale);
                SetCachedMember(pd, "XOffset", orig.XOffset);
                SetCachedMember(pd, "YOffset", orig.YOffset);
                SetCachedMember(pd, "Bottom", orig.Bottom);
                SetCachedMember(pd, "Right", orig.Right);

                if (_ddfPortraitMembers != null && _ddfPortraitMembers.TryGetValue("TexturePath", out var tpMember))
                    SetMember(pd, tpMember, orig.TexturePath);

                ModMonitor?.Log($"Restored DDF originals for {_ddfPatchedNpc}: W={orig.W}, H={orig.H}, Scale={orig.Scale}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                ModMonitor?.Log($"Failed to restore DDF originals: {ex.Message}", LogLevel.Trace);
            }

            _ddfPatchedPortraitObj = null;
            _ddfOriginalValues = null;
        }

        // ====================================================================
        // HARMONY — Portrait index override
        // ====================================================================

        private static void Dialogue_getPortraitIndex_Postfix(Dialogue __instance, ref int __result)
        {
            // APF is asking for the untouched vanilla index itself.
            if (_queryingVanillaIndex)
                return;

            string speakerName = __instance?.speaker?.Name;
            if (speakerName == null || PackManager == null || Instance == null)
                return;

            if (!PackManager.Portraits.TryGetValue(speakerName, out var portrait) || !IsNpcEnabled(speakerName))
                return;

            // Only handle the dialogue box that is actually showing this dialogue.
            if (Game1.activeClickableMenu is not DialogueBox dialogueBox
                || !ReferenceEquals(dialogueBox.characterDialogue, __instance))
                return;

            // The game (or DDFC) can draw the box before APF's first UpdateTicked for it.
            // Set the session up right now, so the NPC's real portrait is saved and its
            // suffix is read *before* APF swaps in its own texture.
            if (!IsAnimating
                || !string.Equals(CurrentAnimatedNpc, speakerName, StringComparison.OrdinalIgnoreCase)
                || !ReferenceEquals(_lastDialogueBox, dialogueBox))
            {
                Instance.ProcessDialogueBox(dialogueBox, advanceAnimation: false);
            }

            if (IsAnimating && CurrentAnimatedFrameIndex >= 0
                && string.Equals(CurrentAnimatedNpc, speakerName, StringComparison.OrdinalIgnoreCase))
            {
                // DDF may only create ActiveData on first draw, so retry the patch here.
                EnsureDdfPortraitSettings(portrait);
                __result = CurrentAnimatedFrameIndex;
            }
        }

        /// <summary>Get the vanilla portrait index for a dialogue, without APF's override.</summary>
        private static int GetVanillaPortraitIndex(Dialogue dialogue)
        {
            _queryingVanillaIndex = true;
            try
            {
                return dialogue.getPortraitIndex();
            }
            finally
            {
                _queryingVanillaIndex = false;
            }
        }

        // ====================================================================
        // ORIGINAL PORTRAIT TRACKING
        // ====================================================================

        /// <summary>
        /// Remember the NPC's real portrait before APF replaces it. If the NPC is somehow still holding
        /// an APF texture (or a disposed one), let the game pick its normal appearance again first.
        /// </summary>
        private static void CaptureOriginalPortrait(NPC speaker)
        {
            if (speaker == null || _originalPortraits.ContainsKey(speaker.Name))
                return;

            Texture2D current = speaker.Portrait;
            if (current == null || current.IsDisposed || TexManager.IsOwned(current))
            {
                speaker.Portrait = null;
                current = speaker.Portrait; // getter calls ChooseAppearance()
            }

            if (current != null && !TexManager.IsOwned(current))
                _originalPortraits[speaker.Name] = (speaker, current);
        }

        /// <summary>Put an NPC's real portrait back, if APF's texture is still on it.</summary>
        private static void RestoreOriginalPortrait(string npcName)
        {
            if (npcName == null)
                return;

            if (_originalPortraits.TryGetValue(npcName, out var original)
                && original.Npc != null
                && (original.Npc.Portrait == null || TexManager.IsOwned(original.Npc.Portrait)))
            {
                original.Npc.Portrait = original.Texture;
            }

            _appliedTextures.Remove(npcName);
        }

        /// <summary>Apply an APF texture to the speaker, noticing if the game replaced it in the meantime.</summary>
        private static void ApplyTexture(NPC speaker, Texture2D texture)
        {
            if (speaker == null || texture == null)
                return;

            // If the game swapped the portrait (e.g. ChooseAppearance on a location change),
            // the new one is the real portrait now.
            Texture2D current = speaker.Portrait;
            if (current != null && !current.IsDisposed && !TexManager.IsOwned(current))
                _originalPortraits[speaker.Name] = (speaker, current);

            if (!ReferenceEquals(current, texture))
                speaker.Portrait = texture;

            _appliedTextures[speaker.Name] = texture;
        }

        // ====================================================================
        // VARIANT RESOLUTION
        // ====================================================================

        /// <summary>
        /// Read the portrait suffix Content Patcher / an event gave this NPC
        /// (e.g. Portraits/Haley_Pyjamas → "Pyjamas"). Uses the NPC's real portrait, never APF's own texture.
        /// Returns null if the portrait is the base asset or not readable.
        /// </summary>
        private static string DetectCpPortraitSuffix(NPC speaker)
        {
            if (speaker == null)
                return null;

            string name = speaker.Name;
            string textureName = null;

            if (_originalPortraits.TryGetValue(name, out var original))
                textureName = original.Texture?.Name;
            else if (!TexManager.IsOwned(speaker.Portrait))
                textureName = speaker.Portrait?.Name;

            string suffix = StripPrefix(textureName, "Portraits", name);
            if (suffix != null)
                return suffix;

            // Fall back to the overworld texture when the portrait has no suffix.
            return StripPrefix(speaker.Sprite?.textureName?.Value, "Characters", name);
        }

        /// <summary>"Portraits/Marnie_Rainy" + ("Portraits", "Marnie") → "Rainy".</summary>
        private static string StripPrefix(string assetName, string folder, string npcName)
        {
            if (string.IsNullOrEmpty(assetName))
                return null;

            foreach (string prefix in new[] { $"{folder}/{npcName}_", $"{folder}\\{npcName}_" })
            {
                if (assetName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && assetName.Length > prefix.Length)
                    return assetName.Substring(prefix.Length);
            }

            return null;
        }

        /// <summary>
        /// ContentPatcher mode: work out which variant the game is currently showing for this NPC,
        /// from its real portrait texture (e.g. Portraits/Marnie_Rainy → "Rainy").
        /// Returns "" for the base portrait or anything APF doesn't recognise.
        /// </summary>
        private static string ReadGamePortraitVariant(NPC speaker, PortraitDefinition portrait)
        {
            string name = speaker.Name;

            Texture2D real = _originalPortraits.TryGetValue(name, out var original)
                ? original.Texture
                : (TexManager.IsOwned(speaker.Portrait) ? null : speaker.Portrait);
            if (real == null || real.IsDisposed)
                return "";

            // Portraits are named after the NPC's texture name, which is usually (not always) their internal name.
            string textureName = NPC.getTextureNameForCharacter(name);

            // 1. By asset name. The game names the texture when it loads a portrait from
            //    Data/Characters (Appearance entries or the default Portraits/{NPC}).
            string assetName = real.Name;
            if (!string.IsNullOrEmpty(assetName))
            {
                if (IsSameAsset(assetName, $"Portraits/{textureName}"))
                    return "";

                string suffix = StripPrefix(assetName, "Portraits", textureName) ?? StripPrefix(assetName, "Portraits", name);
                if (suffix != null)
                    return suffix;
            }

            // 2. By identity. An event's changePortrait loads Portraits/{NPC}_{suffix} without naming the
            //    texture, so compare it with the portrait asset of each variant this NPC has.
            foreach (string variant in GetKnownVariants(portrait))
            {
                string candidate = $"Portraits/{textureName}_{variant}";
                try
                {
                    if (Game1.content.DoesAssetExist<Texture2D>(candidate)
                        && ReferenceEquals(Game1.content.Load<Texture2D>(candidate), real))
                        return variant;
                }
                catch (Exception ex)
                {
                    ModMonitor.Log($"Couldn't check '{candidate}': {ex.Message}", LogLevel.Trace);
                }
            }

            return "";
        }

        /// <summary>Every variant name a portrait definition mentions (VariantExpressions, Variants, rule roots).</summary>
        private static IEnumerable<string> GetKnownVariants(PortraitDefinition portrait)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (portrait.VariantExpressions != null)
                foreach (string key in portrait.VariantExpressions.Keys)
                    if (seen.Add(key)) yield return key;

            if (portrait.Variants != null)
                foreach (string key in portrait.Variants)
                    if (!string.IsNullOrWhiteSpace(key) && seen.Add(key)) yield return key;

            if (PackManager.Rules.TryGetValue(portrait.Target, out var rules))
                foreach (var rule in rules)
                    if (!string.IsNullOrWhiteSpace(rule.Root) && seen.Add(rule.Root)) yield return rule.Root;
        }

        /// <summary>Compare two asset names, ignoring case and slash direction.</summary>
        private static bool IsSameAsset(string a, string b)
        {
            return string.Equals(a?.Replace('\\', '/'), b?.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// ContentPatcher mode: ask the game to re-pick the NPC's appearance (Data/Characters Appearance
        /// entries) right now. Normally it only does that when the NPC changes location, so a time-based
        /// outfit would lag behind. Skipped during events, which control portraits themselves.
        /// </summary>
        private static void RefreshAppearance(NPC speaker)
        {
            if (Game1.eventUp || !speaker.AllowDynamicAppearance || speaker.currentLocation == null)
                return;

            try
            {
                speaker.ChooseAppearance();
            }
            catch (Exception ex)
            {
                ModMonitor.Log($"Couldn't refresh {speaker.Name}'s appearance: {ex.Message}", LogLevel.Trace);
            }
        }

        /// <summary>
        /// Pick the variant for a new dialogue.
        /// ContentPatcher mode: whatever portrait the game/Content Patcher gave the NPC. Rules can then roll a sub-variant.
        /// APF mode: the original built-in logic (<see cref="DetermineVariant"/>).
        /// </summary>
        private static string ResolveSessionVariant(PortraitDefinition portrait, NPC speaker)
        {
            string name = speaker.Name;

            if (portrait.IsContentPatcherControlled)
            {
                string chosen = TextureManager.NormalizeVariant(ReadGamePortraitVariant(speaker, portrait));
                string result = chosen == "" ? "" : ResolvePortraitRoll(portrait, chosen);

                string realName = _originalPortraits.TryGetValue(name, out var original) ? original.Texture?.Name : null;
                ModMonitor.Log(
                    chosen == ""
                        ? $"[CP-SYNC] {name}: game portrait '{realName ?? "?"}' → base expressions."
                        : $"[CP-SYNC] {name}: game portrait '{realName ?? "?"}' → variant '{chosen}'" + (string.Equals(result, chosen, StringComparison.OrdinalIgnoreCase) ? "." : $", rolled '{result}'."),
                    LogLevel.Debug);
                return result;
            }

            string cpSuffix = DetectCpPortraitSuffix(speaker);
            if (cpSuffix != null)
            {
                _cpVariantOverrides[name] = cpSuffix;
                ModMonitor.Log($"[CP-SYNC] Detected CP portrait suffix '{cpSuffix}' for {name}", LogLevel.Debug);
            }
            else
            {
                _cpVariantOverrides.Remove(name);
            }

            return DetermineVariant(portrait, _cpVariantOverrides.GetValueOrDefault(name));
        }

        /// <summary>
        /// Determine which variant (if any) should be active for an NPC based on the
        /// current game state.  Two independent axes are resolved and combined:
        ///   Axis 1 — Context : Location → Weather → Season  (e.g. "Summer")
        ///   Axis 2 — Hearts  : highest matching HeartsN threshold (e.g. "Hearts8")
        /// When both are active the result is "{context}_{hearts}" (e.g. "Summer_Hearts8").
        /// ResolveVariantSprite handles the fallback chain.
        /// An optional <paramref name="cpSuffixOverride"/> from Content Patcher / event
        /// portrait changes takes priority when it matches a known variant name.
        /// </summary>
        private static string DetermineVariant(PortraitDefinition portrait, string cpSuffixOverride = null, bool ignoreLockedVariant = false)
        {
            if (portrait.Variants == null || portrait.Variants.Count == 0)
                return "";

            // ── CP / event portrait suffix override ──
            // If Content Patcher (or an event changePortrait command) changed the portrait
            // asset to e.g. Portraits/Haley_Pyjamas, and "Pyjamas" is a known variant,
            // honour that instead of the internal logic. This keeps APF compatible with
            // standard SDV 1.6 portrait-swapping without requiring custom C# triggers.
            if (!string.IsNullOrEmpty(cpSuffixOverride))
            {
                foreach (var v in portrait.Variants)
                {
                    if (v.Equals(cpSuffixOverride, StringComparison.OrdinalIgnoreCase))
                        return ResolvePortraitRoll(portrait, v);
                }

                // A suffixed variant such as "Beach_BlackTube" can inherit its parent variant.
                int separator = cpSuffixOverride.IndexOf('_');
                if (separator > 0)
                {
                    string parentVariant = cpSuffixOverride.Substring(0, separator);
                    foreach (var v in portrait.Variants)
                    {
                        if (v.Equals(parentVariant, StringComparison.OrdinalIgnoreCase))
                            return ResolvePortraitRoll(portrait, v);
                    }
                }

                // Suffix exists but doesn't match any variant → fall through to normal logic
            }

            // Config: check for locked variant. Trigger detection bypasses this because
            // a visual-only lock must never simulate reaching a friendship threshold.
            if (!ignoreLockedVariant)
            {
                string locked = GetLockedVariant(portrait.Target);
                if (locked != null)
                    return ResolvePortraitRoll(portrait, locked);  // "" = base, "Hearts10" = specific variant
            }

            // ── Axis 1: Context (Location / Weather / Season) ──
            string context = "";
            string locationName = Game1.currentLocation?.Name ?? "";

            bool isBeach = locationName.Equals("Beach", StringComparison.OrdinalIgnoreCase)
                || locationName.StartsWith("Island", StringComparison.OrdinalIgnoreCase)
                   && (locationName.Contains("South", StringComparison.OrdinalIgnoreCase)
                       || locationName.Contains("West", StringComparison.OrdinalIgnoreCase));

            if (isBeach)
            {
                foreach (var v in portrait.Variants)
                    if (v.Equals("Beach", StringComparison.OrdinalIgnoreCase)) { context = v; break; }
            }

            if (string.IsNullOrEmpty(context) && Game1.isRaining)
            {
                foreach (var v in portrait.Variants)
                    if (v.Equals("Rain", StringComparison.OrdinalIgnoreCase)) { context = v; break; }
            }

            if (string.IsNullOrEmpty(context))
            {
                string season = Game1.currentSeason ?? "";
                foreach (var v in portrait.Variants)
                    if (v.Equals(season, StringComparison.OrdinalIgnoreCase)) { context = v; break; }
            }

            // ── Axis 2: Heart level ──
            string hearts = ResolveHeartVariant(portrait);

            // ── Combine ──
            string resolvedRoot = "";
            if (!string.IsNullOrEmpty(context) && !string.IsNullOrEmpty(hearts))
                resolvedRoot = $"{context}_{hearts}";
            else if (!string.IsNullOrEmpty(context))
                resolvedRoot = context;
            else if (!string.IsNullOrEmpty(hearts))
                resolvedRoot = hearts;

            return ResolvePortraitRoll(portrait, resolvedRoot);
        }

        private static string ApplyEvaluatedVariant(PortraitDefinition portrait, string resolvedRoot)
        {
            if (_evaluator == null || string.IsNullOrEmpty(resolvedRoot))
                return resolvedRoot;

            // CarryOver root still in its morning stretch → yesterday's roll (what the NPC went to bed in).
            int dayOffset = _carryOverMorning.ContainsKey(CarryOverKey(portrait.Target, resolvedRoot)) ? -1 : 0;
            string winner = _evaluator.Evaluate(portrait.Target, resolvedRoot, dayOffset);
            if (!string.IsNullOrEmpty(winner))
            {
                ActiveVariantCache.TryGet(portrait.Target, resolvedRoot, out string currentWinner);

                if (!string.Equals(currentWinner, winner, StringComparison.OrdinalIgnoreCase))
                {
                    ActiveVariantCache.Set(portrait.Target, resolvedRoot, winner);

                    // Force SMAPI to dump the old sprite and run OnAssetRequested for the new overlay
                    Instance.Helper.GameContent.InvalidateCache($"Characters/{portrait.Target}");
                    Instance.Helper.GameContent.InvalidateCache($"Characters/{portrait.Target}_{resolvedRoot}");

                    if (Context.IsWorldReady)
                    {
                        var character = Game1.getCharacterFromName(portrait.Target);
                        character?.reloadSprite();
                    }

                    ModMonitor.Log($"[APF] Invalidated sprite cache for {portrait.Target} (New winner: {winner})", LogLevel.Debug);
                }

                return winner;
            }

            if (ActiveVariantCache.TryGet(portrait.Target, resolvedRoot, out _))
            {
                ActiveVariantCache.Remove(portrait.Target, resolvedRoot);

                Instance.Helper.GameContent.InvalidateCache($"Characters/{portrait.Target}");
                Instance.Helper.GameContent.InvalidateCache($"Characters/{portrait.Target}_{resolvedRoot}");

                if (Context.IsWorldReady)
                {
                    var character = Game1.getCharacterFromName(portrait.Target);
                    character?.reloadSprite();
                }
            }

            return resolvedRoot;
        }

        /// <summary>
        /// Check which heart-level variants this NPC has configured (e.g. Hearts2, Hearts8)
        /// and return the highest one the player qualifies for, or empty string.
        /// </summary>
        private static string ResolveHeartVariant(PortraitDefinition portrait)
        {
            int playerHearts = Game1.player.getFriendshipHeartLevelForNPC(portrait.Target);

            // Collect all heart thresholds from the variants list
            string best = null;
            int bestThreshold = -1;

            foreach (var v in portrait.Variants)
            {
                if (v.StartsWith("Hearts", StringComparison.OrdinalIgnoreCase) && v.Length > 6)
                {
                    if (int.TryParse(v.Substring(6), out int threshold) && threshold > 0)
                    {
                        if (playerHearts >= threshold && threshold > bestThreshold)
                        {
                            bestThreshold = threshold;
                            best = v;
                        }
                    }
                }
            }

            return best ?? "";
        }

        /// <summary>
        /// Resolve the sprite path for a given expression + variant.
        /// Supports the 2-axis combined format (e.g. "Summer_Hearts4") with a fallback chain:
        ///   1. Combined folder  (Summer_Hearts4/Haley_0.png)
        ///   2. Context-only     (Summer/Haley_0.png)
        ///   3. Hearts-only      (Hearts4/Haley_0.png)
        ///   4. Default           (Haley_0.png)
        /// Per-expression dictionary overrides bypass the folder convention.
        /// </summary>
        private static string ResolveVariantSprite(PortraitDefinition portrait, ExpressionDefinition exprDef, string variant)
        {
            if (string.IsNullOrEmpty(variant))
                return exprDef.Sprite;

            // Per-expression override takes top priority
            if (exprDef.Variants != null && exprDef.Variants.TryGetValue(variant, out var overridePath))
                return overridePath;

            if (!PackManager.SpriteProviders.TryGetValue(portrait.Target, out var pack))
                return exprDef.Sprite;

            string packDir = pack.DirectoryPath;

            // Try the full variant name first (works for both simple and combined)
            string resolved = portrait.ResolveSpritePath(exprDef.Sprite, variant, packDir);
            if (resolved != exprDef.Sprite)
                return resolved;

            // If it's a combined variant (contains "_"), try context-only then hearts-only
            int sep = variant.IndexOf('_');
            if (sep > 0)
            {
                string contextPart = variant.Substring(0, sep);   // e.g. "Summer"
                string heartsPart  = variant.Substring(sep + 1);  // e.g. "Hearts4"

                // Fallback 1: context-only folder
                resolved = portrait.ResolveSpritePath(exprDef.Sprite, contextPart, packDir);
                if (resolved != exprDef.Sprite)
                    return resolved;

                // Fallback 2: hearts-only folder
                resolved = portrait.ResolveSpritePath(exprDef.Sprite, heartsPart, packDir);
                if (resolved != exprDef.Sprite)
                    return resolved;
            }

            return exprDef.Sprite;
        }

        // ====================================================================
        // ANIMATION TICK
        // ====================================================================

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // Outfit assets changed mid-day: let the overworld sprites follow.
            if (_variantAssetsDirty)
            {
                _variantAssetsDirty = false;
                this.EvaluateAllOverworldSprites();
            }

            // Open the preserved normal dialogue after the one-time trigger closes.
            if (_deferredNormalDialogue != null && Game1.activeClickableMenu == null)
            {
                NPC speaker = _deferredNormalSpeaker;
                Dialogue normalDialogue = _deferredNormalDialogue;
                string npcName = _deferredNormalNpcName;
                Game1.afterFadeFunction normalAfterDialogues = _deferredNormalAfterDialogues;

                _deferredNormalDialogue = null;
                _deferredNormalSpeaker = null;
                _deferredNormalNpcName = null;
                _deferredNormalAfterDialogues = null;

                try
                {
                    if (speaker?.CurrentDialogue.Count > 0
                        && ReferenceEquals(speaker.CurrentDialogue.Peek(), normalDialogue))
                    {
                        _openingDeferredNormalDialogue = true;
                        Game1.afterDialogues = normalAfterDialogues + Game1.afterDialogues;
                        Game1.drawDialogue(speaker);
                        this.Monitor.Log($"[TRIGGER] Opened preserved normal dialogue for {npcName}.", LogLevel.Debug);
                    }
                    else
                    {
                        this.Monitor.Log($"[TRIGGER] Preserved normal dialogue for {npcName} was no longer on top of the dialogue stack; leaving the current stack untouched.", LogLevel.Warn);
                    }
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"[TRIGGER] Failed to reopen the normal dialogue for {npcName}: {ex.Message}", LogLevel.Warn);
                }
                finally
                {
                    _openingDeferredNormalDialogue = false;
                }
            }

            if (Game1.activeClickableMenu is DialogueBox lightingBox)
                PortraitLighting.Update(lightingBox, Game1.currentGameTime.ElapsedGameTime.TotalSeconds);
            else
                PortraitLighting.Reset();

            if (Game1.activeClickableMenu is DialogueBox dialogueBox
                && dialogueBox.characterDialogue?.speaker != null)
            {
                this.ProcessDialogueBox(dialogueBox, advanceAnimation: true);
            }
            else if (IsAnimating)
            {
                CleanupDialogue();
            }
        }

        /// <summary>
        /// Keep APF's portrait in sync with a dialogue box: start a session for a new box,
        /// pick the texture for the current expression and advance the animation.
        /// Called every tick, and from the getPortraitIndex postfix if the box is drawn before the first tick.
        /// </summary>
        /// <param name="dialogueBox">The dialogue box currently shown.</param>
        /// <param name="advanceAnimation">Whether to advance animation time (only once per tick).</param>
        internal void ProcessDialogueBox(DialogueBox dialogueBox, bool advanceAnimation)
        {
            NPC speaker = dialogueBox.characterDialogue.speaker;
            string speakerName = speaker.Name;

            if (!PackManager.Portraits.TryGetValue(speakerName, out var portrait) || !IsNpcEnabled(speakerName))
            {
                if (IsAnimating)
                    CleanupDialogue();
                return;
            }

            bool justStarted = false;

            // Detect NEW dialogue (first open, different NPC, or different DialogueBox instance)
            if (!IsAnimating
                || !string.Equals(CurrentAnimatedNpc, speakerName, StringComparison.OrdinalIgnoreCase)
                || !ReferenceEquals(_lastDialogueBox, dialogueBox))
            {
                // A different NPC was talking in the previous box: give them their portrait back.
                if (IsAnimating && !string.Equals(CurrentAnimatedNpc, speakerName, StringComparison.OrdinalIgnoreCase))
                    CleanupDialogue();

                justStarted = true;
                IsAnimating = true;
                CurrentAnimatedNpc = speakerName;
                CurrentAnimatedFrameIndex = -1;
                LastVanillaPortraitIndex = -1;
                _lastDialogueBox = dialogueBox;
                _dialogueTickCount = 0;

                if (!ActiveAnimations.ContainsKey(speakerName))
                    ActiveAnimations[speakerName] = new AnimationState(portrait);
                else
                    ActiveAnimations[speakerName].Reset();

                _ddfPatchedNpc = null;
                _ddfPatchedThisSession = false;
                _ddfPatchedSessionNpc = null;
                _ddfPatchedSessionBox = null;

                // ContentPatcher mode: let the game re-pick the outfit first, so it's current.
                if (portrait.IsContentPatcherControlled && !_originalPortraits.ContainsKey(speakerName))
                    RefreshAppearance(speaker);

                // Save the real portrait and read its suffix BEFORE APF touches the texture.
                CaptureOriginalPortrait(speaker);

                string variant = ResolveSessionVariant(portrait, speaker);
                TexManager.SetActiveVariant(speakerName, variant);

                string modeStr = portrait.IsPerExpressionMode ? "per-expr" : "single-sheet";
                string controlStr = portrait.IsContentPatcherControlled ? "ContentPatcher" : "APF";
                string variantLabel = string.IsNullOrEmpty(variant) ? "default" : variant;
                this.Monitor.Log($"[SETUP] New dialogue for {speakerName} (cols={portrait.Columns}, mode={modeStr}, control={controlStr}, variant={variantLabel})", LogLevel.Debug);

                // Activate an optional body-change animation on the trigger dialogue itself.
                if (_activeBodyChange.TryGetValue(speakerName, out var bodyChangeDef))
                {
                    ActiveAnimations[speakerName].BodyChangeOverride = bodyChangeDef;
                    _activeBodyChange.Remove(speakerName);
                    this.Monitor.Log($"[TRIGGER] Body-change animation activated for {speakerName} ({bodyChangeDef.TotalFrames} frames, {bodyChangeDef.Fps} FPS, mode={bodyChangeDef.Mode})", LogLevel.Info);
                }
            }
            else if (!portrait.IsContentPatcherControlled)
            {
                // APF mode: re-check variant each tick (handles rare mid-dialogue location change).
                // ContentPatcher mode keeps the variant chosen when the dialogue opened.
                string variant = DetermineVariant(portrait, _cpVariantOverrides.GetValueOrDefault(speakerName));
                TexManager.SetActiveVariant(speakerName, variant);
            }

            if (advanceAnimation)
                _dialogueTickCount++;

            var state = ActiveAnimations[speakerName];
            int vanillaIndex = GetVanillaPortraitIndex(dialogueBox.characterDialogue);
            LastVanillaPortraitIndex = vanillaIndex;
            string activeVariant = TexManager.GetActiveVariant(speakerName);
            state.SetExpression(vanillaIndex, activeVariant);

            if (!state.HasExpression)
            {
                // Expression not configured for this NPC/variant: show the game's own portrait.
                this.Passthrough(speakerName, justStarted, vanillaIndex, "expression not configured");
                return;
            }

            // Pick the texture for this expression + variant.
            string sprite = ResolveActiveSprite(portrait, state, vanillaIndex, activeVariant);
            Texture2D texture = sprite != null
                ? TexManager.GetOrLoad(speakerName, sprite, portrait.FrameSize, portrait.Columns)
                : null;

            if (texture == null)
            {
                // No sprite for this expression (or it failed to load): use the game's portrait.
                this.Passthrough(speakerName, justStarted, vanillaIndex, sprite == null ? "no sprite" : $"sprite '{sprite}' failed to load");
                return;
            }

            // Ensure DDF uses our dynamic texture; layout overrides remain optional.
            EnsureDdfPortraitSettings(portrait, speakerName, dialogueBox);
            ApplyTexture(speaker, texture);

            if (advanceAnimation && !justStarted)
                state.Update(Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds);

            // Per-expression: frame index within this expression's sheet. Single-sheet: index in the combined grid.
            // A body-change animation always has its own sheet.
            bool ownSheet = portrait.IsPerExpressionMode || state.BodyChangeOverride != null;
            CurrentAnimatedFrameIndex = ownSheet ? state.CurrentFrame : state.GetAbsoluteFrameIndex();

            if (justStarted || _dialogueTickCount <= 5)
                this.Monitor.Log($"[TICK {_dialogueTickCount}] {speakerName}: vanillaIdx={vanillaIndex} expr={state.CurrentExpression} variant={(string.IsNullOrEmpty(activeVariant) ? "default" : activeVariant)} sprite={sprite} frame={CurrentAnimatedFrameIndex}", LogLevel.Trace);
        }

        /// <summary>Show the game's own portrait for the current expression.</summary>
        private void Passthrough(string speakerName, bool justStarted, int vanillaIndex, string reason)
        {
            RestoreOriginalPortrait(speakerName);
            CurrentAnimatedFrameIndex = -1;
            if (justStarted)
                this.Monitor.Log($"[TICK 0] {speakerName}: vanillaIdx={vanillaIndex} — {reason}, showing the game's portrait", LogLevel.Debug);
        }

        /// <summary>
        /// Get the sprite path for the active expression.
        /// Order: body-change sprite → VariantExpressions sprite → the base expression's per-variant
        /// override / variant subfolder → the base expression's own sprite. Returns null if none.
        /// </summary>
        private static string ResolveActiveSprite(PortraitDefinition portrait, AnimationState state, int vanillaIndex, string variant)
        {
            var active = state.ActiveExpression;

            if (state.BodyChangeOverride != null)
                return string.IsNullOrWhiteSpace(active?.Sprite) ? null : active.Sprite;

            if (!portrait.IsPerExpressionMode)
            {
                if (string.IsNullOrWhiteSpace(portrait.Sprite))
                    return null;
                return ResolveVariantSprite(portrait, new ExpressionDefinition { Sprite = portrait.Sprite }, variant);
            }

            if (state.ActiveExpressionIsVariant && !string.IsNullOrWhiteSpace(active?.Sprite))
                return active.Sprite;

            if (portrait.Expressions.TryGetValue(vanillaIndex.ToString(), out var baseDef) && baseDef != null)
            {
                // ResolveVariantSprite checks the disk for variant subfolders, so cache the result.
                string cacheKey = $"{portrait.Target}|{vanillaIndex}|{variant}";
                if (!_resolvedSpriteCache.TryGetValue(cacheKey, out string resolved))
                {
                    resolved = ResolveVariantSprite(portrait, baseDef, variant);
                    if (string.IsNullOrWhiteSpace(resolved))
                        resolved = null;
                    _resolvedSpriteCache[cacheKey] = resolved;
                }
                return resolved;
            }

            return null;
        }

        /// <summary>Cached results of <see cref="ResolveVariantSprite"/> for base expressions. Key: "NPC|expression|variant".</summary>
        private static readonly Dictionary<string, string> _resolvedSpriteCache = new(StringComparer.OrdinalIgnoreCase);

        // ====================================================================
        // CONFIG HELPERS
        // ====================================================================

        /// <summary>Get or create config for a content pack.</summary>
        private static PackConfig GetPackConfig(string packId)
        {
            if (!Config.Packs.TryGetValue(packId, out var pc))
            {
                pc = new PackConfig();
                Config.Packs[packId] = pc;
            }
            return pc;
        }

        /// <summary>Get or create per-character config within a pack.</summary>
        private static CharacterConfig GetCharConfig(string packId, string npcName)
        {
            var pc = GetPackConfig(packId);
            if (!pc.Characters.TryGetValue(npcName, out var cc))
            {
                cc = new CharacterConfig();
                pc.Characters[npcName] = cc;
            }
            return cc;
        }

        /// <summary>Check if an NPC is enabled (pack enabled AND character enabled).</summary>
        internal static bool IsNpcEnabled(string npcName)
        {
            if (!PackManager.NpcPackIds.TryGetValue(npcName, out string packId))
                return true;
            var pc = GetPackConfig(packId);
            if (!pc.Enabled) return false;
            var cc = GetCharConfig(packId, npcName);
            return cc.Enabled;
        }

        /// <summary>Check whether heart-based variants and their change dialogues are enabled.</summary>
        internal static bool IsGrowthEnabled(string npcName)
        {
            if (!PackManager.NpcPackIds.TryGetValue(npcName, out string packId))
                return true;
            return GetPackConfig(packId).GrowthEnabled;
        }

        /// <summary>Get locked variant for an NPC, or null if "Auto".</summary>
        internal static string GetLockedVariant(string npcName)
        {
            if (!PackManager.NpcPackIds.TryGetValue(npcName, out string packId))
                return null;
            var pc = GetPackConfig(packId);
            if (!pc.GrowthEnabled) return "";  // growth disabled → base variant
            var cc = GetCharConfig(packId, npcName);
            if (string.IsNullOrEmpty(cc.LockedVariant) || cc.LockedVariant == "Auto")
                return null;  // auto mode
            return cc.LockedVariant;
        }

        /// <summary>Check if body-change transitions are enabled for an NPC's pack.</summary>
        internal static bool IsTransitionEnabled(string npcName)
        {
            if (!PackManager.NpcPackIds.TryGetValue(npcName, out string packId))
                return true;
            return GetPackConfig(packId).ShowTransitions;
        }

        // ====================================================================
        // GMCM INTEGRATION
        // ====================================================================

        /// <summary>Add the portrait lighting options to APF's own GMCM page.</summary>
        private void RegisterPortraitLightingGmcm(IGmcmApi gmcmApi)
        {
            var manifest = this.ModManifest;
            PortraitLightingConfig L() => Config.PortraitLighting;

            gmcmApi.AddSectionTitle(manifest, () => "Portrait Lighting");
            gmcmApi.AddParagraph(manifest, () =>
                "Darkens dialogue portraits to match how dark the world is (evenings, nights, rain, caves, the mines). "
                + "Follows the game's own lighting, so mods that change sunset times or indoor lighting are followed automatically.");
            gmcmApi.AddBoolOption(
                mod: manifest,
                name: () => "Enable",
                tooltip: () => "Darken dialogue portraits to match the world's lighting.",
                getValue: () => L().Enabled,
                setValue: v => L().Enabled = v
            );
            gmcmApi.AddNumberOption(
                mod: manifest,
                name: () => "Strength",
                tooltip: () => "How strongly portraits follow the world's darkness. 100% = as dark as the world itself.",
                getValue: () => L().Strength,
                setValue: v => L().Strength = v,
                min: 0, max: 100, interval: 5,
                formatValue: v => $"{v}%"
            );
            gmcmApi.AddNumberOption(
                mod: manifest,
                name: () => "Minimum brightness",
                tooltip: () => "Portraits never get darker than this, so they stay readable.",
                getValue: () => L().MinimumBrightness,
                setValue: v => L().MinimumBrightness = v,
                min: 0, max: 100, interval: 5,
                formatValue: v => $"{v}%"
            );
            gmcmApi.AddNumberOption(
                mod: manifest,
                name: () => "Indoor strength",
                tooltip: () => "How much indoor darkness counts, relative to outdoors. Lower this if portraits get too dark inside at night.",
                getValue: () => L().IndoorStrength,
                setValue: v => L().IndoorStrength = v,
                min: 0, max: 100, interval: 5,
                formatValue: v => $"{v}%"
            );
            gmcmApi.AddBoolOption(
                mod: manifest,
                name: () => "Neutral colors",
                tooltip: () => "Darken evenly instead of taking on the game's bluish night color.",
                getValue: () => L().NeutralColors,
                setValue: v => L().NeutralColors = v
            );
            gmcmApi.AddBoolOption(
                mod: manifest,
                name: () => "All portraits",
                tooltip: () => "On: every dialogue portrait is darkened. Off: only portraits shown by APF content packs.",
                getValue: () => L().AllPortraits,
                setValue: v => L().AllPortraits = v
            );

            gmcmApi.AddSectionTitle(manifest, () => "Light Sources");
            gmcmApi.AddParagraph(manifest, () =>
                "Nearby lights (lamps, torches, campfires, window light, lava, glowing sprites, and lights added by other mods) "
                + "give back brightness in their own color. The closer the light, the stronger. Lights only undo darkness; "
                + "they never make a portrait brighter than normal, so they do nothing in daylight.");
            gmcmApi.AddBoolOption(
                mod: manifest,
                name: () => "Enable light sources",
                tooltip: () => "Let nearby light sources brighten and tint the portrait.",
                getValue: () => L().LightSources,
                setValue: v => L().LightSources = v
            );
            gmcmApi.AddNumberOption(
                mod: manifest,
                name: () => "Light strength",
                tooltip: () => "How strongly nearby lights undo the darkness. 100% = a light right next to the NPC fully restores the portrait.",
                getValue: () => L().LightStrength,
                setValue: v => L().LightStrength = v,
                min: 0, max: 200, interval: 10,
                formatValue: v => $"{v}%"
            );
            gmcmApi.AddNumberOption(
                mod: manifest,
                name: () => "Light reach",
                tooltip: () => "How far lights reach, compared to how big they look in the game.",
                getValue: () => L().LightReach,
                setValue: v => L().LightReach = v,
                min: 10, max: 300, interval: 10,
                formatValue: v => $"{v}%"
            );
            gmcmApi.AddNumberOption(
                mod: manifest,
                name: () => "Light color",
                tooltip: () => "How much a light's color tints the portrait. 0% = plain white light, 100% = the light's full color.",
                getValue: () => L().LightHue,
                setValue: v => L().LightHue = v,
                min: 0, max: 100, interval: 5,
                formatValue: v => $"{v}%"
            );
            gmcmApi.AddNumberOption(
                mod: manifest,
                name: () => "Light saturation",
                tooltip: () => "How rich light colors are. 100% = the light's own color, higher = deeper (e.g. a warmer campfire or fireplace), lower = more washed out.",
                getValue: () => L().LightSaturation,
                setValue: v => L().LightSaturation = v,
                min: 0, max: 300, interval: 10,
                formatValue: v => $"{v}%"
            );
            gmcmApi.AddBoolOption(
                mod: manifest,
                name: () => "Player lights",
                tooltip: () => "Let lights carried by players (lantern, glow ring) light up the NPC you're talking to.",
                getValue: () => L().PlayerLights,
                setValue: v => L().PlayerLights = v
            );
        }

        private void RegisterGmcm()
        {
            var gmcmApi = this.Helper.ModRegistry.GetApi<IGmcmApi>("spacechase0.GenericModConfigMenu");
            if (gmcmApi == null)
            {
                this.Monitor.Log("GMCM not installed — skipping config menu registration.", LogLevel.Debug);
                return;
            }

            // Group by content pack — only include packs/NPCs that have variants
            var packNpcs = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in PackManager.NpcPackIds)
            {
                var portrait = PackManager.Portraits[kvp.Key];
                if (portrait.Variants == null || portrait.Variants.Count == 0)
                    continue; // skip NPCs without variants
                if (!packNpcs.ContainsKey(kvp.Value))
                    packNpcs[kvp.Value] = new List<string>();
                packNpcs[kvp.Value].Add(kvp.Key);
            }

            // Register GMCM under each content pack's own manifest
            // so it shows as "Curves of Affection" (or whatever the pack is named) in GMCM's mod list
            foreach (var pkKvp in packNpcs)
            {
                string packId = pkKvp.Key;
                var npcNames = pkKvp.Value.OrderBy(n => n).ToList();

                if (!PackManager.PackManifests.TryGetValue(packId, out var packManifest))
                    continue;

                string capturedPackId = packId; // capture for closures
                gmcmApi.Register(
                    mod: packManifest,
                    reset: () => { Config.Packs.Remove(capturedPackId); },
                    save: () => this.Helper.WriteConfig(Config)
                );

                // Pack-level toggles
                gmcmApi.AddBoolOption(
                    mod: packManifest,
                    name: () => "Enable Pack",
                    tooltip: () => "Enable or disable all animated portraits from this content pack.",
                    getValue: () => GetPackConfig(capturedPackId).Enabled,
                    setValue: val => GetPackConfig(capturedPackId).Enabled = val
                );
                // Growth/transition options only matter for NPCs using APF's own variant logic.
                bool hasApfControlled = npcNames.Any(n => !PackManager.Portraits[n].IsContentPatcherControlled);
                if (hasApfControlled)
                {
                    gmcmApi.AddBoolOption(
                        mod: packManifest,
                        name: () => "Growth System",
                        tooltip: () => "Enable heart-based variant progression. When off, all characters use their base look.",
                        getValue: () => GetPackConfig(capturedPackId).GrowthEnabled,
                        setValue: val => GetPackConfig(capturedPackId).GrowthEnabled = val
                    );
                    gmcmApi.AddBoolOption(
                        mod: packManifest,
                        name: () => "Transition Animations",
                        tooltip: () => "Show body-change animations when reaching a new heart threshold.",
                        getValue: () => GetPackConfig(capturedPackId).ShowTransitions,
                        setValue: val => GetPackConfig(capturedPackId).ShowTransitions = val
                    );
                }

                // Per-character settings
                foreach (string npcName in npcNames)
                {
                    var portrait = PackManager.Portraits[npcName];

                    gmcmApi.AddSectionTitle(packManifest, () => $"👤 {npcName}");

                    // Enable toggle
                    string npc = npcName; // capture for closure
                    gmcmApi.AddBoolOption(
                        mod: packManifest,
                        name: () => $"Enable {npc}",
                        tooltip: () => $"Enable or disable animated portraits for {npc}.",
                        getValue: () => GetCharConfig(capturedPackId, npc).Enabled,
                        setValue: val => GetCharConfig(capturedPackId, npc).Enabled = val
                    );

                    // Content Patcher picks the variant for these NPCs, so a lock would only fight it.
                    if (portrait.IsContentPatcherControlled)
                        continue;

                    // Lock Variant dropdown
                    var variantChoices = new List<string> { "Auto", "" };
                    if (portrait.Variants != null)
                        variantChoices.AddRange(portrait.Variants);
                    string[] choices = variantChoices.ToArray();

                    gmcmApi.AddTextOption(
                        mod: packManifest,
                        name: () => $"Lock Variant",
                        tooltip: () => $"Lock {npc}'s appearance to a specific variant.\nAuto = normal heart progression.\nEmpty = always base look.",
                        getValue: () =>
                        {
                            var val = GetCharConfig(capturedPackId, npc).LockedVariant;
                            return choices.Contains(val) ? val : "Auto";
                        },
                        setValue: val => GetCharConfig(capturedPackId, npc).LockedVariant = val,
                        allowedValues: choices,
                        formatAllowedValue: v => v switch
                        {
                            "Auto" => "🔄 Auto (Heart Progression)",
                            "" => "🖼️ Base (No Variant)",
                            _ => $"💎 {v}"
                        }
                    );
                }

                this.Monitor.Log($"GMCM registered for '{packManifest.Name}': {npcNames.Count} character(s).", LogLevel.Info);
            }

            // APF's own page: portrait lighting (always) + sprite variant toggles (if any rules).
            gmcmApi.Register(
                mod: this.ModManifest,
                reset: () =>
                {
                    Config.PortraitLighting = new PortraitLightingConfig();
                    Config.VariantEnabled.Clear();
                },
                save: () => this.Helper.WriteConfig(Config)
            );
            this.RegisterPortraitLightingGmcm(gmcmApi);

            if (PackManager.Rules.Count > 0)
            {
                foreach (var kvp in PackManager.Rules.OrderBy(k => k.Key))
                {
                    string npcName = kvp.Key;
                    gmcmApi.AddSectionTitle(this.ModManifest, () => $"Sprite Variant Toggles: {npcName}");

                    foreach (string variantId in kvp.Value
                        .SelectMany(rule => rule.Variants)
                        .Select(variant => variant.Id)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(id => id))
                    {
                        string capturedId = variantId;
                        gmcmApi.AddBoolOption(
                            mod: this.ModManifest,
                            name: () => capturedId,
                            tooltip: () => $"Enable or disable the {capturedId} overworld variant.",
                            getValue: () => Config.IsVariantEnabled(capturedId),
                            setValue: value => Config.VariantEnabled[capturedId] = value
                        );
                    }
                }
            }
        }

        /// <summary>Reset animation state and restore original portraits when dialogue closes.</summary>
        private void CleanupDialogue()
        {
            // Give every NPC APF touched in this dialogue their real portrait back.
            foreach (string npc in _originalPortraits.Keys.ToList())
                RestoreOriginalPortrait(npc);
            _originalPortraits.Clear();
            _appliedTextures.Clear();

            _cpVariantOverrides.Remove(CurrentAnimatedNpc ?? "");
            RestoreDdfOriginals();
            _ddfPatchedNpc = null;
            _ddfPatchedThisSession = false;
            _ddfPatchedSessionNpc = null;
            _ddfPatchedSessionBox = null;
            IsAnimating = false;
            CurrentAnimatedFrameIndex = -1;
            CurrentAnimatedNpc = null;
            LastVanillaPortraitIndex = -1;
            _lastDialogueBox = null;
        }
    }
}
