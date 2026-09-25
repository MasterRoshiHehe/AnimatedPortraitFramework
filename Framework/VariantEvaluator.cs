using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>Evaluates eligible variants and selects a weighted winner at the highest priority.</summary>
    public class VariantEvaluator
    {
        private readonly ContentPackManager _packManager;
        private readonly ConditionChecker _conditionChecker;
        private readonly IMonitor _monitor;

        public VariantEvaluator(ContentPackManager packManager, ConditionChecker conditionChecker, IMonitor monitor)
        {
            _packManager = packManager;
            _conditionChecker = conditionChecker;
            _monitor = monitor;
        }

        /// <summary>
        /// A seed that stays the same for one NPC + root variant for the whole in-game day, for every
        /// player in multiplayer, and after restarting the game.
        /// Uses the world date (not the per-player DaysPlayed stat, which differs for farmhands who joined
        /// later). string.GetHashCode and HashCode.Combine are randomised per process, so they aren't used.
        /// </summary>
        /// <param name="npcName">The NPC's internal name.</param>
        /// <param name="rootVariant">The root variant being rolled.</param>
        /// <param name="dayOffset">Days relative to today, e.g. -1 for yesterday's roll.</param>
        internal static int DailySeed(string npcName, string rootVariant, int dayOffset = 0)
        {
            int day = Context.IsWorldReady ? Game1.Date.TotalDays + NormalizeDayOffset(dayOffset) : 0;
            string key = $"{npcName?.ToLowerInvariant()}|{rootVariant?.ToLowerInvariant()}|{day}|{Game1.uniqueIDForThisGame}";
            return Game1.hash.GetDeterministicHashCode(key);
        }

        /// <summary>Use offset 0 if the offset would point before the first day of the save.</summary>
        internal static int NormalizeDayOffset(int dayOffset)
        {
            if (dayOffset == 0 || !Context.IsWorldReady)
                return 0;
            return Game1.Date.TotalDays + dayOffset < 0 ? 0 : dayOffset;
        }

        /// <summary>Whether any rule for this NPC + root has <see cref="VariantRule.CarryOver"/> enabled.</summary>
        public bool IsCarryOver(string npcName, string rootVariant)
        {
            if (!_packManager.Rules.TryGetValue(npcName, out var rules))
                return false;

            return rules.Any(rule => rule.CarryOver && string.Equals(rule.Root, rootVariant, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Pick the winning sub-variant for an NPC + root.</summary>
        /// <param name="npcName">The NPC's internal name.</param>
        /// <param name="rootVariant">The root variant (e.g. "Pyjamas").</param>
        /// <param name="dayOffset">Days relative to today, e.g. -1 to recompute yesterday's roll.</param>
        public string Evaluate(string npcName, string rootVariant, int dayOffset = 0)
        {
            dayOffset = NormalizeDayOffset(dayOffset);

            if (!_packManager.Rules.TryGetValue(npcName, out var rules))
                return null;

            var eligible = new List<(int Priority, double Weight, string Id)>();
            foreach (var rule in rules)
            {
                if (!string.Equals(rule.Root, rootVariant, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var variant in rule.Variants)
                {
                    if (!ModEntry.Config.IsVariantEnabled(variant.Id))
                        continue;

                    bool allPass = true;
                    foreach (var condition in variant.Conditions)
                    {
                        if (!string.Equals(condition.Type, "Random", StringComparison.OrdinalIgnoreCase)
                            && !_conditionChecker.Check(condition, npcName, rootVariant, dayOffset))
                        {
                            allPass = false;
                            break;
                        }
                    }

                    if (allPass)
                        eligible.Add((variant.Priority, variant.Weight, variant.Id));
                }
            }

            if (eligible.Count == 0)
                return null;

            int highestPriority = eligible.Max(e => e.Priority);
            var topPriority = eligible.Where(e => e.Priority == highestPriority && e.Weight > 0).ToList();
            if (topPriority.Count == 0)
                return null;

            double totalWeight = topPriority.Sum(e => Math.Max(0.0, e.Weight));
            double roll = new Random(DailySeed(npcName, rootVariant, dayOffset)).NextDouble() * totalWeight;

            double accumulated = 0.0;
            var winner = topPriority[^1];
            foreach (var candidate in topPriority)
            {
                accumulated += Math.Max(0.0, candidate.Weight);
                if (roll <= accumulated)
                {
                    winner = candidate;
                    break;
                }
            }

            _monitor.Log($"[APF] {npcName}/{rootVariant} -> {winner.Id} (priority {winner.Priority}, weight {winner.Weight}/{totalWeight}{(dayOffset != 0 ? $", day offset {dayOffset}" : "")})", LogLevel.Trace);
            return winner.Id;
        }
    }
}
