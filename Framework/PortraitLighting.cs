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
    /// Nearby light sources (lamps, torches, campfires, window lights, lava, glowing sprites; anything in
    /// <see cref="Game1.currentLightSources"/>, including lights added or recoloured by other mods) then undo part of
    /// that darkness, depending on distance, and tint the portrait with the light's colour.
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
                    _factors = ComputeTargetFactors(box);
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

            Vector3 target = ComputeTargetFactors(box);

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
        private static Vector3 ComputeTargetFactors(DialogueBox box)
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
            Vector3 factors = Vector3.Clamp(Vector3.One - darkening * strength, new Vector3(minimum), Vector3.One);

            // Nearby lights give back part of the lost brightness, in their own colour.
            // They can only undo darkness, never make the portrait brighter than normal.
            if (config.LightSources && factors != Vector3.One)
            {
                Vector3 light = GatherLight(box?.characterDialogue?.speaker, location, config);
                light = Vector3.Clamp(light * (Math.Clamp(config.LightStrength, 0, 200) / 100f), Vector3.Zero, Vector3.One);
                factors += (Vector3.One - factors) * light;
            }

            return factors;
        }

        /// <summary>
        /// Add up the light reaching an NPC from every light source in the current location.
        /// Returns per-channel light (0 = none, 1 = fully lit), before the Light strength setting.
        /// </summary>
        /// <remarks>
        /// Game light facts (from the game code):
        /// - A light is drawn as its light texture, centred on its position, scaled by its radius; so its reach is
        ///   about half the texture's width × radius (a lantern at radius 1 reaches ~2 tiles).
        /// - Light colours are stored inverted (the lightmap is subtracted from the screen): new Color(0, 80, 160) is a
        ///   warm orange torch, Color.Black is white light. The visible colour is 255 − value.
        /// - The colour's alpha scales the light's strength (fading lights, e.g. Color.HotPink * 0.75).
        /// </remarks>
        private static Vector3 GatherLight(NPC speaker, GameLocation location, PortraitLightingConfig config)
        {
            if (speaker == null || Game1.currentLightSources == null)
                return Vector3.Zero;

            // Light sources belong to the player's current location; skip NPCs somewhere else (e.g. on the phone).
            if (speaker.currentLocation != null && !ReferenceEquals(speaker.currentLocation, location))
                return Vector3.Zero;

            // Roughly the NPC's upper body / face, not their feet.
            Vector2 target = speaker.getStandingPosition() + new Vector2(0f, -48f);
            float reachMultiplier = Math.Clamp(config.LightReach, 10, 300) / 100f;
            float hue = Math.Clamp(config.LightHue, 0, 100) / 100f;
            Vector3 total = Vector3.Zero;

            // Game1.currentLightSources is a Dictionary<string, LightSource> since SDV 1.6.9.
            foreach (LightSource light in Game1.currentLightSources.Values)
            {
                if (light == null)
                    continue;

                // Player-carried lights (lantern, glow ring).
                long owner = light.PlayerID;
                if (owner != 0)
                {
                    if (!config.PlayerLights)
                        continue;
                    if (owner != Game1.player.UniqueMultiplayerID
                        && !ReferenceEquals(Game1.getFarmerMaybeOffline(owner)?.currentLocation, location))
                        continue;
                }

                Color color = light.color.Value;
                if (color.A == 0)
                    continue;

                float halfWidth = (light.lightTexture?.Width ?? 256) / 2f;
                float reach = halfWidth * light.radius.Value * reachMultiplier;
                if (reach <= 1f)
                    continue;

                float distance = Vector2.Distance(target, light.position.Value);
                if (distance >= reach)
                    continue;

                float closeness = 1f - distance / reach;
                float intensity = closeness * closeness * (color.A / 255f);

                var visible = new Vector3(1f - color.R / 255f, 1f - color.G / 255f, 1f - color.B / 255f);
                total += Vector3.Lerp(Vector3.One, visible, hue) * intensity;
            }

            return total;
        }
    }
}
