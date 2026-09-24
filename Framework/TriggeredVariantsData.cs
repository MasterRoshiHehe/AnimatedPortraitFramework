using System.Collections.Generic;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>
    /// Persisted per-save data tracking which "once" variant triggers have already fired.
    /// Stored via SMAPI SaveData API under the key "APF_TriggeredVariants".
    /// </summary>
    public class TriggeredVariantsData
    {
        /// <summary>
        /// Key = NPC internal name, Value = set of variant names that have been triggered.
        /// Example: { "Haley": ["Hearts2", "Hearts4"] }
        /// </summary>
        public Dictionary<string, List<string>> Triggered { get; set; } = new();

        public bool IsTriggered(string npcName, string variant)
        {
            return this.Triggered.TryGetValue(npcName, out var list) && list.Contains(variant);
        }

        public void MarkTriggered(string npcName, string variant)
        {
            if (!this.Triggered.ContainsKey(npcName))
                this.Triggered[npcName] = new List<string>();
            if (!this.Triggered[npcName].Contains(variant))
                this.Triggered[npcName].Add(variant);
        }
    }
}
