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

        public string Evaluate(string npcName, string rootVariant)
        {
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
                            && !_conditionChecker.Check(condition, npcName, rootVariant))
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
            int seed = HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(npcName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(rootVariant),
                Context.IsWorldReady ? (int)Game1.stats.DaysPlayed : 0);
            double roll = new Random(seed).NextDouble() * totalWeight;

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

            _monitor.Log($"[APF] {npcName}/{rootVariant} -> {winner.Id} (priority {winner.Priority}, weight {winner.Weight}/{totalWeight})", LogLevel.Trace);
            return winner.Id;
        }
    }
}
