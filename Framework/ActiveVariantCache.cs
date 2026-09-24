using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>Stores the winning sub-variant per NPC and root pair.</summary>
    public class ActiveVariantCache
    {
        private readonly Dictionary<(string NPC, string Root), string> _data =
            new(TupleComparer.Instance);

        public void Set(string npc, string root, string subVariant)
            => _data[(npc, root)] = subVariant;

        public bool TryGet(string npc, string root, out string subVariant)
            => _data.TryGetValue((npc, root), out subVariant);

        public void Remove(string npc, string root)
            => _data.Remove((npc, root));

        public void ClearNpc(string npc)
        {
            var keys = _data.Keys
                .Where(k => string.Equals(k.NPC, npc, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var key in keys)
                _data.Remove(key);
        }

        public void Clear() => _data.Clear();

        public IEnumerable<(string NPC, string Root)> Keys => _data.Keys;

        private sealed class TupleComparer : IEqualityComparer<(string NPC, string Root)>
        {
            public static readonly TupleComparer Instance = new();

            public bool Equals((string NPC, string Root) x, (string NPC, string Root) y)
                => string.Equals(x.NPC, y.NPC, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Root, y.Root, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((string NPC, string Root) obj)
                => HashCode.Combine(obj.NPC?.ToLowerInvariant() ?? "", obj.Root?.ToLowerInvariant() ?? "");
        }
    }
}
