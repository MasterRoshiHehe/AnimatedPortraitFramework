using DialogueDisplayFramework.Data;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DialogueDisplayFramework.Framework
{
    public class DialogueBoxInterface
    {
        public static bool preventGetCurrentString;
        private static Dictionary<string, DialogueDisplayData> cachedDialogueData = new();

        public static Vector2? AppliedBoxPosition { get; set; }

        // Reflection
        public static IReflectedMethod shouldPortraitShake;

        /// <summary>
        /// Invalidates the display data cache.
        /// </summary>
        public static void InvalidateCache()
        {
            cachedDialogueData.Clear();
            AppliedBoxPosition = null;
        }

        /// <summary>
        /// Fetches the relevant dialogue display data to use when rendering an NPC's dialogue.
        /// Can return null if no default data exists.
        /// </summary>
        /// <param name="npc">The relevant NPC data.</param>
        /// <returns>A data dictionary entry.</returns>
        public static DialogueDisplayData GetCharacterDisplay(NPC npc)
        {
            // Cached object is reset when a new dialogue box is opened or the asset cache is invalidated
            if (cachedDialogueData.TryGetValue(npc.Name, out var cachedResult))
                return cachedResult;

            DialogueDisplayData result = null;
            bool queryResult = false;

            var dataDict = ModEntry.SHelper.GameContent.Load<Dictionary<string, DialogueDisplayData>>(ModEntry.DictAssetName);

            // Location-specific attire key, for legacy support
            var location = npc.currentLocation;
            if (location != null && location.TryGetMapProperty("UniquePortrait", out string uniquePortraitsProperty) && ArgUtility.SplitBySpace(uniquePortraitsProperty).Contains(npc.Name))
            {
                dataDict.TryGetValue(npc.Name + "_" + location.Name, out result);
                queryResult = GameStateQuery.CheckConditions(result?.Condition);
            }

            // KeyCharacter appearance key
            if ((result == null || result.Disabled || !queryResult) && npc.LastAppearanceId != null)
            {
                dataDict.TryGetValue(npc.Name + "_" + npc.LastAppearanceId, out result);
                queryResult = GameStateQuery.CheckConditions(result?.Condition);
            }

            // Beach attire key
            if ((result == null || result.Disabled || !queryResult) && ModEntry.SHelper.Reflection.GetField<bool>(npc, "isWearingIslandAttire").GetValue())
            {
                dataDict.TryGetValue(npc.Name + "_Beach", out result);
                queryResult = GameStateQuery.CheckConditions(result?.Condition);
            }

            // Regular character key
            if (result == null || result.Disabled || !queryResult)
            {
                dataDict.TryGetValue(npc.Name, out result);
                queryResult = GameStateQuery.CheckConditions(result?.Condition);
            }

            // Default key
            if (result == null || result.Disabled || !queryResult)
                dataDict.TryGetValue(ModEntry.DefaultKey, out result);

            // Fill empty values from the copy
            result = MergeResults(result, result.CopyFrom, dataDict);

            cachedDialogueData.Add(npc.Name, result);

            return result;
        }

        /// <summary>
        /// Fetches relevant dialogue display data to use when rendering a special dialogue, for example: Farmer Portraits.
        /// Can return null if no default data exists.
        /// </summary>
        /// <param name="name">The special key's name</param>
        /// <returns>A data dictionary entry.</returns>
        public static DialogueDisplayData GetSpecialDisplay(string name)
        {
            // Cached object is reset when a new dialogue box is opened or the asset cache is invalidated
            if (cachedDialogueData.TryGetValue(name, out var cachedResult))
                return cachedResult;

            var dataDict = ModEntry.SHelper.GameContent.Load<Dictionary<string, DialogueDisplayData>>(ModEntry.DictAssetName);
            dataDict.TryGetValue(name, out var result);

            if (result == null || result.Disabled)
                dataDict.TryGetValue(ModEntry.DefaultKey, out result);

            // Fill empty values from the copy
            result = MergeResults(result, result.CopyFrom, dataDict);

            cachedDialogueData.Add(name, result);

            return result;
        }

        private static DialogueDisplayData MergeResults(DialogueDisplayData result, string baseKey, Dictionary<string, DialogueDisplayData> dataDict)
        {
            if (baseKey is null or "" || !dataDict.TryGetValue(baseKey, out var baseData))
                return result;

            result = DataHelpers.MergeEntries(result, baseData);

            return MergeResults(result, baseData.CopyFrom, dataDict);
        }
    }
}
