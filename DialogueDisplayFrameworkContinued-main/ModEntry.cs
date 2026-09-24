using DialogueDisplayFramework.Api;
using DialogueDisplayFramework.Data;
using DialogueDisplayFramework.Framework;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DialogueDisplayFramework
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {
        /*********
         * Public properties
         ********/

        public static IManifest SModManifest { get; private set; }
        public static IMonitor SMonitor { get; private set; }
        public static IModHelper SHelper { get; private set; }
        public static ModConfig Config { get; private set; }
        public static IAssetName DictAssetName { get; private set; }
        public static Dictionary<string, Texture2D> ImageDict { get; } = new Dictionary<string, Texture2D>();
        public static string DefaultKey { get; } = "default";

        /*********
         * Private fields
         ********/

        private static readonly string dictPath = "aedenthorn.DialogueDisplayFramework/dictionary";

        private static EventHandler<UpdateTickedEventArgs> _OnContentPatcherReady_Handler;
        private static int validationDelay = 5;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            SModManifest = ModManifest;
            SMonitor = Monitor;
            SHelper = helper;
            Config = Helper.ReadConfig<ModConfig>();

            DictAssetName = helper.GameContent.ParseAssetName(dictPath);

            _OnContentPatcherReady_Handler = OnContentPatcherReady;

            // Game event listeners
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.Content.AssetRequested += OnAssetRequested_Post; // After CP edits
            helper.Events.Content.AssetsInvalidated += OnAssetInvalidated;

            // Temp game event listeners
            helper.Events.GameLoop.UpdateTicked += _OnContentPatcherReady_Handler;

            var harmony = new Harmony(ModManifest.UniqueID);
            DialogueBoxPatches.Apply(harmony, Config, Monitor, Helper);

            DialogueGameStateQueries.Register();
        }

        public override object GetApi(IModInfo mod)
        {
            var api = new DialogueDisplayApi(mod.Manifest);
            ApiConsumerManager.RegisterApiConsumer(api);
            return api;
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!Config.EnableMod)
                return;

            if (e.NameWithoutLocale.IsEquivalentTo(DictAssetName))
            {
                e.LoadFrom(() => new Dictionary<string, DialogueDisplayData>
                {
                    { DefaultKey, DataHelpers.DefaultValues }
                }, AssetLoadPriority.Exclusive);
            }
        }

        [EventPriority(EventPriority.Low)]
        private void OnAssetRequested_Post(object sender, AssetRequestedEventArgs e)
        {
            if (!Config.EnableMod)
                return;

            if (e.NameWithoutLocale.IsEquivalentTo(DictAssetName))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, DialogueDisplayData>().Data;
                    var hasModsWithMissingID = false;

                    ImageDict.Clear();

                    foreach (var (key, entry) in data)
                    {
                        if (entry.Disabled)
                            continue;

                        // Validate copy references

                        if (entry.CopyFrom != null)
                        {
                            var target = entry;
                            var traceStack = new List<DialogueDisplayData>() { entry };
                            var cyclicRef = false;

                            while (!cyclicRef && target.CopyFrom != null && data.TryGetValue(target.CopyFrom, out target))
                            {
                                foreach (var traceStep in traceStack)
                                {
                                    if (traceStep == target)
                                    {
                                        SMonitor.Log($"{key} > {traceStack.Select(d => d.CopyFrom).Join(delimiter: " > ")} : Cyclic reference detected. Disabling.", LogLevel.Error);

                                        foreach (var d in traceStack)
                                            d.Disabled = true;

                                        cyclicRef = true;
                                        break;
                                    }
                                }
                                traceStack.Add(target);
                            }

                            if (!data.ContainsKey(entry.CopyFrom))
                            {
                                SMonitor.Log($"{key} : CopyFrom key points to a non-existant entry: {entry.CopyFrom}", LogLevel.Warn);
                            }
                        }

                        foreach (var image in entry.Images)
                        {
                            if (!ImageDict.ContainsKey(image.TexturePath))
                                ImageDict[image.TexturePath] = Game1.content.Load<Texture2D>(image.TexturePath);
                        }

                        if (entry.Portrait?.TexturePath != null && !ImageDict.ContainsKey(entry.Portrait.TexturePath))
                        {
                            ImageDict[entry.Portrait.TexturePath] = Game1.content.Load<Texture2D>(entry.Portrait.TexturePath);
                        }

                        var imagesWithMissingID = entry.Images.Select(i => (!i.Disabled && (i.ID == null || i.ID == DataHelpers.MISSING_ID_STR)) ? 1 : 0).Sum();
                        if (imagesWithMissingID > 0)
                            SMonitor.Log($"{key} : References {imagesWithMissingID} image{(imagesWithMissingID > 1 ? "s" : "")} with missing ID.", LogLevel.Warn);

                        var textsWithMissingID = entry.Texts.Select(i => (!i.Disabled && (i.ID == null || i.ID == DataHelpers.MISSING_ID_STR)) ? 1 : 0).Sum();
                        if (textsWithMissingID > 0)
                            SMonitor.Log($"{key} : References {textsWithMissingID} text{(textsWithMissingID > 1 ? "s" : "")} with missing ID.", LogLevel.Warn);

                        var dividersWithMissingID = entry.Dividers.Select(i => (!i.Disabled && (i.ID == null || i.ID == DataHelpers.MISSING_ID_STR)) ? 1 : 0).Sum();
                        if (dividersWithMissingID > 0)
                            SMonitor.Log($"{key} : References {dividersWithMissingID} divider{(dividersWithMissingID > 1 ? "s" : "")} with missing ID.", LogLevel.Warn);

                        hasModsWithMissingID = hasModsWithMissingID || (imagesWithMissingID + textsWithMissingID + dividersWithMissingID > 0);
                    }

                    if (hasModsWithMissingID)
                        SMonitor.Log($"Please make sure to include a unique ID on each image, text and divider entries for better support.", LogLevel.Warn);
                });
            }
        }

        private void OnAssetInvalidated(object sender, AssetsInvalidatedEventArgs e)
        {
            if (!Config.EnableMod)
                return;

            if (e.NamesWithoutLocale.Contains(DictAssetName))
            {
                DialogueBoxInterface.InvalidateCache();
            }
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Mod Enabled",
                getValue: () => Config.EnableMod,
                setValue: value => Config.EnableMod = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Dialogue Width Offset",
                tooltip: () => "Size offset to the dialogue box's width. Negative input shrinks size.\nDon't forget to adjust the x offset.",
                getValue: () => Config.DialogueWidthOffset,
                setValue: (value) => Config.DialogueWidthOffset = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Dialogue Height Offset",
                tooltip: () => "Size offset to the dialogue box's height. Negative input shrinks size.\nDon't forget to adjust the y offset.",
                getValue: () => Config.DialogueHeightOffset,
                setValue: (value) => Config.DialogueHeightOffset = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Dialogue X Offset",
                tooltip: () => "Position offset to the dialogue box's x position. Negative input moves the box to the left.",
                getValue: () => Config.DialogueXOffset,
                setValue: (value) => Config.DialogueXOffset = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Dialogue Y Offset",
                tooltip: () => "Position offset to the dialogue box's y position. Negative input moves the box up.",
                getValue: () => Config.DialogueYOffset,
                setValue: (value) => Config.DialogueYOffset = value
            );
        }

        private void OnContentPatcherReady(object sender, UpdateTickedEventArgs e)
        {
            if (validationDelay-- < 0)
            {
                // Load our data to trigger validation
                SHelper.GameContent.Load<Dictionary<string, DialogueDisplayData>>(DictAssetName);
                SHelper.Events.GameLoop.UpdateTicked -= _OnContentPatcherReady_Handler;
            }
        }
    }
}