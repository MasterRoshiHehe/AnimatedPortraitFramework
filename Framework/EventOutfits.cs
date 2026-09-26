using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>The kind of event the local player is currently watching.</summary>
    internal enum EventKind
    {
        None,
        Cutscene,
        Festival
    }

    /// <summary>
    /// Tracks cutscenes and festivals and answers what a root variant should do during them
    /// (<see cref="PortraitDefinition.VariantEvents"/>).
    ///
    /// Everything is read from the local game state (the event this player is watching), so it's multiplayer-safe
    /// without messages or save data. The actual sprite swap is done by the asset hook in ModEntry: when the event
    /// kind changes, ModEntry invalidates the affected <c>Characters/{NPC}_{Root}</c> assets and SMAPI updates the
    /// already-loaded textures in place, so the overworld NPC and every event actor using that sheet change together.
    /// </summary>
    internal static class EventOutfits
    {
        /// <summary>Asset name "Characters/{NPC}_{Root}" → (NPC, Root), for every root with VariantEvents.</summary>
        private static readonly Dictionary<string, (string Npc, string Root)> _rootAssets = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The event kind the sprites were last refreshed for.</summary>
        public static EventKind Current { get; private set; } = EventKind.None;

        /// <summary>Whether any loaded portrait uses VariantEvents (if not, the per-tick check does nothing).</summary>
        public static bool HasAny => _rootAssets.Count > 0;

        /// <summary>Every (NPC, Root) with VariantEvents.</summary>
        public static IEnumerable<(string Npc, string Root)> Roots => _rootAssets.Values;

        /// <summary>Rebuild the root list after content packs were (re)loaded.</summary>
        public static void Rebuild(ContentPackManager packs)
        {
            _rootAssets.Clear();
            if (packs?.Portraits == null)
                return;

            foreach (var kvp in packs.Portraits)
            {
                var events = kvp.Value?.VariantEvents;
                if (events == null)
                    continue;

                foreach (var entry in events)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                        continue;
                    if (entry.Value.Cutscenes == null && entry.Value.Festivals == null)
                        continue;

                    _rootAssets[RootAsset(kvp.Key, entry.Key)] = (kvp.Key, entry.Key);
                }
            }
        }

        /// <summary>Forget the current event (save loaded / returned to title).</summary>
        public static void Reset()
        {
            Current = EventKind.None;
        }

        /// <summary>The overworld sprite asset of a root variant.</summary>
        public static string RootAsset(string npc, string root) => $"Characters/{npc}_{root}";

        /// <summary>Get the NPC and root if this asset is a root with VariantEvents.</summary>
        public static bool TryGetRoot(string assetName, out string npc, out string root)
        {
            if (assetName != null && _rootAssets.TryGetValue(assetName.Replace('\\', '/'), out var pair))
            {
                npc = pair.Npc;
                root = pair.Root;
                return true;
            }

            npc = null;
            root = null;
            return false;
        }

        /// <summary>
        /// The kind of event the local player is watching right now.
        /// A festival counts as a festival for its whole run (including its main event, e.g. the Flower Dance).
        /// Everything else (heart events, the wedding, cutscenes) is a cutscene.
        /// </summary>
        public static EventKind ReadLive()
        {
            if (!Context.IsWorldReady)
                return EventKind.None;

            Event ev = Game1.CurrentEvent;
            if (ev != null)
                return ev.isFestival ? EventKind.Festival : EventKind.Cutscene;

            // While an event moves the player between locations, CurrentEvent can briefly be null.
            // Keep the current kind as long as the game still says an event is up.
            return Game1.eventUp ? Current : EventKind.None;
        }

        /// <summary>Check for a change in event kind. Returns true (with the old kind) if it changed.</summary>
        public static bool Update(out EventKind previous)
        {
            previous = Current;
            EventKind now = ReadLive();
            if (now == Current)
                return false;

            Current = now;
            return true;
        }

        /// <summary>
        /// The settings that apply to this NPC + root during this kind of event, or null if there are none
        /// (no VariantEvents entry, no block for this event kind, or its RequiresMod isn't installed).
        /// </summary>
        public static VariantEventBehavior GetBehavior(PortraitDefinition portrait, string root, EventKind kind, IModRegistry mods)
        {
            if (kind == EventKind.None || portrait?.VariantEvents == null || string.IsNullOrWhiteSpace(root))
                return null;

            if (!portrait.VariantEvents.TryGetValue(root, out var settings) || settings == null)
                return null;

            var behavior = kind == EventKind.Festival ? settings.Festivals : settings.Cutscenes;
            if (behavior == null)
                return null;

            if (!string.IsNullOrWhiteSpace(behavior.RequiresMod) && mods?.IsLoaded(behavior.RequiresMod.Trim()) != true)
                return null;

            return behavior;
        }
    }
}
