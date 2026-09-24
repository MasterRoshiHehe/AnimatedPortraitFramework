using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>
    /// Manages per-expression texture loading, caching, and disposal.
    /// In per-expression mode, only the current expression's texture is loaded into VRAM,
    /// keeping memory usage minimal (~80 MB per active expression instead of 1+ GB for a full sheet).
    /// Supports seasonal/location variants — cache key includes the active variant.
    /// </summary>
    public class TextureManager
    {
        private readonly IMonitor _monitor;

        /// <summary>Cached textures: cache key → Texture2D. Key format: \"NPC:expr:variant\".</summary>
        private readonly Dictionary<string, Texture2D> _cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Content pack references for loading: NPC name → IContentPack.</summary>
        private readonly Dictionary<string, IContentPack> _packs = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Currently active variant per NPC.</summary>
        private readonly Dictionary<string, string> _activeVariants = new(StringComparer.OrdinalIgnoreCase);

        public TextureManager(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Register which content pack provides sprites for a given NPC.</summary>
        public void RegisterPack(string npc, IContentPack pack)
        {
            _packs[npc] = pack;
        }

        /// <summary>Get the currently active variant for an NPC (empty string = default).</summary>
        public string GetActiveVariant(string npc)
        {
            return _activeVariants.TryGetValue(npc, out var v) ? v : "";
        }

        /// <summary>Normalize a variant string before comparing or caching it.</summary>
        public static string NormalizeVariant(string variant)
        {
            if (string.IsNullOrWhiteSpace(variant))
                return "";

            return variant.Trim();
        }

        /// <summary>Set the active variant for an NPC. Returns true if it changed.</summary>
        public bool SetActiveVariant(string npc, string variant)
        {
            string current = GetActiveVariant(npc);
            string next = NormalizeVariant(variant);
            if (string.Equals(current, next, StringComparison.OrdinalIgnoreCase))
                return false;

            _activeVariants[npc] = next;
            // Don't dispose textures — they stay cached for fast switching
            _monitor.Log($"Variant changed for {npc}: \"{current}\" → \"{next}\"", LogLevel.Debug);
            return true;
        }

        private string CacheKey(string npc, int expressionIndex, string variant)
        {
            return $"{npc}:{expressionIndex}:{variant ?? ""}";
        }

        /// <summary>
        /// Get (or load) the texture for a specific expression + variant.
        /// Returns null if the sprite path is missing or loading fails.
        /// The loaded spritesheet is repacked to 2 columns so the game's
        /// source rect calculation (tile = texture.Width / 2) gives the correct frame size.
        /// </summary>
        public Texture2D GetOrLoad(string npc, int expressionIndex, string spritePath, int frameSize, int columns)
        {
            if (string.IsNullOrWhiteSpace(spritePath))
                return null;

            string variant = GetActiveVariant(npc);
            string key = CacheKey(npc, expressionIndex, variant);

            if (_cache.TryGetValue(key, out var existing) && existing != null && !existing.IsDisposed)
                return existing;

            if (!_packs.TryGetValue(npc, out var pack))
                return null;

            string fullPath = Path.Combine(pack.DirectoryPath, spritePath);
            if (!File.Exists(fullPath))
            {
                _monitor.Log($"Expression sprite not found: {fullPath}", LogLevel.Error);
                return null;
            }

            try
            {
                Texture2D tex = null;
                string normalizedSpritePath = spritePath.Replace('\\', '/');

                try
                {
                    tex = pack.ModContent.Load<Texture2D>(normalizedSpritePath);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"ModContent.Load failed for {npc} expr {expressionIndex} ({normalizedSpritePath}): {ex.Message}", LogLevel.Trace);
                }

                if (tex == null)
                {
                    using var stream = File.OpenRead(fullPath);
                    tex = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                }

                PremultiplyAlpha(tex);
                tex = RepackToTwoColumns(tex, frameSize, columns);
                _cache[key] = tex;
                string variantLabel = string.IsNullOrEmpty(variant) ? "default" : variant;
                _monitor.Log($"Loaded expression texture: {npc} expr {expressionIndex} variant={variantLabel} ({tex.Width}x{tex.Height})", LogLevel.Trace);
                return tex;
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to load expression texture for {npc} expr {expressionIndex}: {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Repack a spritesheet from N columns to 2 columns so that
        /// texture.Width / 2 == frameSize, matching the game's source rect calculation.
        /// </summary>
        private Texture2D RepackToTwoColumns(Texture2D original, int frameSize, int srcColumns)
        {
            // Already 2 columns or fewer — no repacking needed
            if (srcColumns <= 2 || original.Width <= frameSize * 2)
                return original;

            int srcRows = original.Height / frameSize;
            int totalFrames = srcColumns * srcRows;
            int dstRows = (int)Math.Ceiling(totalFrames / 2.0);
            int newWidth = frameSize * 2;
            int newHeight = dstRows * frameSize;

            // Safety: don't exceed common GPU max texture dimension
            if (newHeight > 16384)
            {
                _monitor.Log($"Cannot repack to 2 columns (height {newHeight} exceeds 16384). Portrait rendering may be incorrect.", LogLevel.Warn);
                return original;
            }

            int origW = original.Width, origH = original.Height;
            Color[] srcPixels = new Color[origW * origH];
            original.GetData(srcPixels);

            Color[] dstPixels = new Color[newWidth * newHeight];

            for (int frame = 0; frame < totalFrames; frame++)
            {
                int sc = frame % srcColumns;
                int sr = frame / srcColumns;
                int dc = frame % 2;
                int dr = frame / 2;

                for (int y = 0; y < frameSize; y++)
                {
                    int srcOffset = (sr * frameSize + y) * origW + sc * frameSize;
                    int dstOffset = (dr * frameSize + y) * newWidth + dc * frameSize;
                    Array.Copy(srcPixels, srcOffset, dstPixels, dstOffset, frameSize);
                }
            }

            var repacked = new Texture2D(Game1.graphics.GraphicsDevice, newWidth, newHeight);
            repacked.SetData(dstPixels);
            original.Dispose();

            _monitor.Log($"Repacked spritesheet from {origW}x{origH} ({srcColumns} cols) to {newWidth}x{newHeight} (2 cols)", LogLevel.Trace);
            return repacked;
        }

        /// <summary>
        /// Convert straight alpha (from PNG loading via FromStream) to premultiplied alpha
        /// for correct rendering with MonoGame's SpriteBatch / BlendState.AlphaBlend.
        /// No thresholding — preserves original alpha values as-is.
        /// </summary>
        private static void PremultiplyAlpha(Texture2D texture)
        {
            Color[] data = new Color[texture.Width * texture.Height];
            texture.GetData(data);

            for (int i = 0; i < data.Length; i++)
            {
                byte a = data[i].A;
                if (a == 0 || a == 255) continue;

                data[i] = new Color(
                    (byte)(data[i].R * a / 255),
                    (byte)(data[i].G * a / 255),
                    (byte)(data[i].B * a / 255),
                    a
                );
            }

            texture.SetData(data);
        }

        /// <summary>Dispose all cached textures for an NPC (all variants).</summary>
        public void DisposeNpc(string npc)
        {
            string prefix = npc + ":";
            var keysToRemove = new List<string>();
            foreach (var kvp in _cache)
            {
                if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (kvp.Value != null && !kvp.Value.IsDisposed)
                        kvp.Value.Dispose();
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
                _cache.Remove(key);
            _activeVariants.Remove(npc);
        }

        /// <summary>Dispose all cached textures.</summary>
        public void DisposeAll()
        {
            foreach (var tex in _cache.Values)
                if (tex != null && !tex.IsDisposed) tex.Dispose();
            _cache.Clear();
            _activeVariants.Clear();
            _packs.Clear();
        }
    }
}
