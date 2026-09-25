using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>
    /// Darkens dialogue portraits to match how dark the world currently is.
    ///
    /// It reads the game's own final light values (<see cref="Game1.outdoorLight"/>, <see cref="Game1.ambientLight"/>,
    /// mine lighting), so anything that changes the game's lighting (sunset-time mods like Dynamic Dusk, indoor
    /// lighting mods, rain, caves, the mines) is followed automatically.
    ///
    /// The result is applied as a draw tint: DDFC's portrait draw call normally uses <see cref="Color.White"/>; a small
    /// Harmony transpiler swaps that for <see cref="GetTint"/>. No extra draws, textures or shaders.
    /// </summary>
    internal static class PortraitLighting
    {
        /// <summary>Current per-channel brightness factors (1 = unchanged), smoothed over time.</summary>
        private static Vector3 _factors = Vector3.One;

        /// <summary>The dialogue box the current factors belong to (a new box snaps instead of fading).</summary>
        private static DialogueBox _box;

        /// <summary>How fast the tint follows a change in lighting (per second, exponential).</summary>
        private const float FadeRate = 3f;

        /// <summary>Whether the DDFC draw call was patched successfully.</summary>
        internal static bool IsActive { get; private set; }

        private static int _replacedCalls;

        // ====================================================================
        // PATCHING
        // ====================================================================

        /// <summary>Patch DDFC's portrait draw so it uses <see cref="GetTint"/> instead of Color.White.</summary>
        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            try
            {
                Type rendererType = AccessTools.TypeByName("DialogueDisplayFramework.Framework.DialogueBoxRenderer");
                var drawPortrait = rendererType != null ? AccessTools.Method(rendererType, "DrawPortrait") : null;
                if (drawPortrait == null)
                {
                    monitor.Log("Portrait lighting: couldn't find DDFC's DialogueBoxRenderer.DrawPortrait. Portrait lighting is disabled.", LogLevel.Warn);
                    return;
                }

                _replacedCalls = 0;
                harmony.Patch(drawPortrait, transpiler: new HarmonyMethod(typeof(PortraitLighting), nameof(DrawPortrait_Transpiler)));

                IsActive = _replacedCalls > 0;
                monitor.Log(
                    IsActive
                        ? "Portrait lighting: DDFC portrait draw patched."
                        : "Portrait lighting: DDFC's portrait draw no longer uses Color.White as expected. Portrait lighting is disabled.",
                    IsActive ? LogLevel.Trace : LogLevel.Warn);
            }
            catch (Exception ex)
            {
                monitor.Log($"Portrait lighting: failed to patch DDFC ({ex.Message}). Portrait lighting is disabled.", LogLevel.Warn);
            }
        }

        /// <summary>Replace every Color.White in DDFC's DrawPortrait with a call to <see cref="GetTint"/>.</summary>
        private static IEnumerable<CodeInstruction> DrawPortrait_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var getWhite = AccessTools.PropertyGetter(typeof(Color), nameof(Color.White));
            var getTint = AccessTools.Method(typeof(PortraitLighting), nameof(GetTint));

            foreach (var instruction in instructions)
            {
                if (instruction.Calls(getWhite))
                {
                    instruction.operand = getTint;
                    _replacedCalls++;
                }
                yield return instruction;
            }
        }

        // ====================================================================
        // RUNTIME
        // ====================================================================

        /// <summary>
        /// Called by DDFC's portrait draw (via the transpiler) instead of Color.White.
        /// Must be cheap and must never throw.
        /// </summary>
        public static Color GetTint()
        {
            try
            {
                var config = ModEntry.Config?.PortraitLighting;
                if (config == null || !config.Enabled || Game1.activeClickableMenu is not DialogueBox box)
                    return Color.White;

                if (!config.AllPortraits && !IsApfPortrait(box))
                    return Color.White;

                // First draw of a new box (can happen before the first tick): start at the right value.
                if (!ReferenceEquals(box, _box))
                {
                    _box = box;
                    _factors = ComputeTargetFactors();
                }

                return new Color(_factors.X, _factors.Y, _factors.Z, 1f);
            }
            catch
            {
                return Color.White;
            }
        }

        /// <summary>
        /// Move the tint towards the current lighting. Call once per tick while a dialogue box is open.
        /// </summary>
        public static void Update(DialogueBox box, double elapsedSeconds)
        {
            var config = ModEntry.Config?.PortraitLighting;
            if (!IsActive || config == null || !config.Enabled)
                return;

            Vector3 target = ComputeTargetFactors();

            if (!ReferenceEquals(box, _box))
            {
                // New dialogue box: no fade-in, start at the current lighting.
                _box = box;
                _factors = target;
                return;
            }

            float t = 1f - MathF.Exp(-FadeRate * (float)Math.Max(0, elapsedSeconds));
            _factors = Vector3.Lerp(_factors, target, t);
        }

        /// <summary>Forget the current dialogue box (call when no dialogue is open).</summary>
        public static void Reset()
        {
            _box = null;
        }

        /// <summary>Whether the dialogue box is showing a portrait APF is currently drawing.</summary>
        private static bool IsApfPortrait(DialogueBox box)
        {
            string speaker = box.characterDialogue?.speaker?.Name;
            return speaker != null
                && ModEntry.IsAnimating
                && ModEntry.CurrentAnimatedFrameIndex >= 0
                && string.Equals(ModEntry.CurrentAnimatedNpc, speaker, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Work out per-channel brightness factors (0..1) from the game's current lighting.
        ///
        /// Mirrors <c>Game1.DrawLighting</c> / <c>DrawLightmapOnScreen</c>: the game fills the lightmap with the lighting
        /// colour (drawn non-premultiplied, so the stored value is rgb × alpha), then subtracts lightmap × lightmap from
        /// the screen (ReverseSubtract with SourceColor). So the darkening per channel is (rgb × alpha)².
        /// </summary>
        private static Vector3 ComputeTargetFactors()
        {
            var config = ModEntry.Config.PortraitLighting;
            GameLocation location = Game1.currentLocation;

            // No lighting pass this frame → the world isn't darkened, so neither is the portrait.
            if (location == null || !Game1.drawLighting)
                return Vector3.One;

            Color lighting;
            if (location is MineShaft mine)
                lighting = mine.getLightingColor(Game1.currentGameTime);
            else if (Game1.ambientLight.Equals(Color.White) || (location.IsOutdoors && location.IsRainingHere()))
                lighting = Game1.outdoorLight;
            else
                lighting = Game1.ambientLight;

            float alpha = lighting.A / 255f;
            var stored = new Vector3(lighting.R / 255f * alpha, lighting.G / 255f * alpha, lighting.B / 255f * alpha);
            Vector3 darkening = stored * stored;

            if (config.NeutralColors)
            {
                float average = (darkening.X + darkening.Y + darkening.Z) / 3f;
                darkening = new Vector3(average);
            }

            float strength = Math.Clamp(config.Strength, 0, 100) / 100f;
            if (!location.IsOutdoors)
                strength *= Math.Clamp(config.IndoorStrength, 0, 100) / 100f;

            float minimum = Math.Clamp(config.MinimumBrightness, 0, 100) / 100f;
            return Vector3.Clamp(Vector3.One - darkening * strength, new Vector3(minimum), Vector3.One);
        }
    }
}
