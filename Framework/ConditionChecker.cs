using System;
using StardewModdingAPI;
using StardewValley;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>Evaluates a single variant condition against the current game state.</summary>
    public class ConditionChecker
    {
        private readonly IModRegistry _modRegistry;

        public ConditionChecker(IModRegistry modRegistry)
        {
            _modRegistry = modRegistry;
        }

        public bool Check(ConditionDefinition condition, string npcName, string rootVariant)
        {
            return condition.Type.ToLowerInvariant() switch
            {
                "hearts" => CheckHearts(condition, npcName),
                "weather" => CheckWeather(condition),
                "season" => CheckSeason(condition),
                "time" => CheckTime(condition),
                "location" => CheckLocation(condition),
                "dayofweek" => CheckDayOfWeek(condition),
                "random" => CheckRandom(condition, npcName, rootVariant),
                "hasflag" => CheckHasFlag(condition),
                "mod" => CheckMod(condition),
                _ => false
            };
        }

        private static bool CheckHearts(ConditionDefinition condition, string npcName)
        {
            int hearts = Game1.player.getFriendshipHeartLevelForNPC(npcName);
            return (!condition.Min.HasValue || hearts >= condition.Min.Value)
                && (!condition.Max.HasValue || hearts <= condition.Max.Value);
        }

        private static bool CheckWeather(ConditionDefinition condition)
        {
            if (condition.Value == null) return false;
            string current = Game1.isLightning ? "Storm"
                : Game1.isRaining ? "Rain"
                : Game1.isSnowing ? "Snow"
                : Game1.isDebrisWeather ? "Wind"
                : "Sun";
            return string.Equals(current, condition.Value, StringComparison.OrdinalIgnoreCase);
        }

        private static bool CheckSeason(ConditionDefinition condition)
            => condition.Value != null && string.Equals(Game1.currentSeason, condition.Value, StringComparison.OrdinalIgnoreCase);

        private static bool CheckTime(ConditionDefinition condition)
        {
            int time = Game1.timeOfDay;
            return (!condition.Min.HasValue || time >= condition.Min.Value)
                && (!condition.Max.HasValue || time <= condition.Max.Value);
        }

        private static bool CheckLocation(ConditionDefinition condition)
            => condition.Name != null && (Game1.currentLocation?.Name ?? "").StartsWith(condition.Name, StringComparison.OrdinalIgnoreCase);

        private static bool CheckDayOfWeek(ConditionDefinition condition)
        {
            if (condition.Value == null) return false;
            string[] days = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            return string.Equals(days[(Game1.dayOfMonth - 1) % 7], condition.Value, StringComparison.OrdinalIgnoreCase);
        }

        private static bool CheckRandom(ConditionDefinition condition, string npcName, string rootVariant)
        {
            // Salted so a Random condition doesn't roll the same number as the weighted pick.
            int seed = VariantEvaluator.DailySeed(npcName, rootVariant + "|random-condition");
            return new Random(seed).NextDouble() <= condition.Chance;
        }

        private static bool CheckHasFlag(ConditionDefinition condition)
            => condition.Flag != null
                && (Game1.player.mailReceived.Contains(condition.Flag) || Game1.player.eventsSeen.Contains(condition.Flag));

        private bool CheckMod(ConditionDefinition condition)
            => condition.ModId != null && _modRegistry.IsLoaded(condition.ModId);
    }
}
