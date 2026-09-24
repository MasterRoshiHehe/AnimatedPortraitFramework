using System.Collections.Generic;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>One NPC and root variant combination with competing sub-variants.</summary>
    public class VariantRule
    {
        public string NPC { get; set; } = "";
        public string Root { get; set; } = "";
        public List<VariantDefinition> Variants { get; set; } = new();
    }

    /// <summary>A candidate sub-variant with its selection priority and conditions.</summary>
    public class VariantDefinition
    {
        public string Id { get; set; } = "";
        public int Priority { get; set; }
        public double Weight { get; set; } = 1.0;
        public List<ConditionDefinition> Conditions { get; set; } = new();
    }

    public class RuleDefinition : VariantRule
    {
    }

    /// <summary>A condition evaluated against the current game state.</summary>
    public class ConditionDefinition
    {
        public string Type { get; set; } = "";
        public string Value { get; set; }
        public int? Min { get; set; }
        public int? Max { get; set; }
        public double Chance { get; set; } = 1.0;
        public string Flag { get; set; }
        public string ModId { get; set; }
        public string Name { get; set; }
    }
}
