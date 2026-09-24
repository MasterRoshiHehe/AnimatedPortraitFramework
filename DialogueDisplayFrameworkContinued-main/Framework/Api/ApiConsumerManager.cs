using DialogueDisplayFramework.Data;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;
using System;
using System.Collections.Generic;

namespace DialogueDisplayFramework.Api
{
    internal class ApiConsumerManager
    {
        internal static List<DialogueDisplayApi> ApiConsumers = new();

        internal static void RegisterApiConsumer(DialogueDisplayApi api)
        {
            ApiConsumers.Add(api);
        }

        internal static void RaiseRenderingDialogueBox(SpriteBatch b, DialogueBox box, DialogueDisplayData data)
        {
            var args = new RenderEventArgs<IDialogueDisplayData>(b, box, data.GetAdapter());
            ApiConsumers.ForEach(c => c.OnRaiseRenderingDialogueBox(args));
        }

        internal static void RaiseRenderedDialogueBox(SpriteBatch b, DialogueBox box, IDialogueDisplayData data)
        {
            var args = new RenderEventArgs<IDialogueDisplayData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderedDialogueBox(args));
        }

        internal static void RaiseRenderingDialogueString(SpriteBatch b, DialogueBox box, IDialogueStringData data)
        {
            var args = new RenderEventArgs<IDialogueStringData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderingDialogueString(args));
        }

        internal static void RaiseRenderedDialogueString(SpriteBatch b, DialogueBox box, IDialogueStringData data)
        {
            var args = new RenderEventArgs<IDialogueStringData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderedDialogueString(args));
        }

        internal static void RaiseRenderingPortrait(SpriteBatch b, DialogueBox box, IPortraitData data)
        {
            var args = new RenderEventArgs<IPortraitData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderingPortrait(args));
        }

        internal static void RaiseRenderedPortrait(SpriteBatch b, DialogueBox box, IPortraitData data)
        {
            var args = new RenderEventArgs<IPortraitData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderedPortrait(args));
        }

        internal static void RaiseRenderingJewel(SpriteBatch b, DialogueBox box, IBaseData data)
        {
            var args = new RenderEventArgs<IBaseData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderingJewel(args));
        }

        internal static void RaiseRenderedJewel(SpriteBatch b, DialogueBox box, IBaseData data)
        {
            var args = new RenderEventArgs<IBaseData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderedJewel(args));
        }

        internal static void RaiseRenderingButton(SpriteBatch b, DialogueBox box, IBaseData data)
        {
            var args = new RenderEventArgs<IBaseData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderingButton(args));
        }

        internal static void RaiseRenderedButton(SpriteBatch b, DialogueBox box, IBaseData data)
        {
            var args = new RenderEventArgs<IBaseData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderedButton(args));
        }

        internal static void RaiseRenderingGifts(SpriteBatch b, DialogueBox box, IGiftsData data)
        {
            var args = new RenderEventArgs<IGiftsData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderingGifts(args));
        }

        internal static void RaiseRenderedGifts(SpriteBatch b, DialogueBox box, IGiftsData data)
        {
            var args = new RenderEventArgs<IGiftsData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderedGifts(args));
        }

        internal static void RaiseRenderingHearts(SpriteBatch b, DialogueBox box, IHeartsData data)
        {
            var args = new RenderEventArgs<IHeartsData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderingHearts(args));
        }

        internal static void RaiseRenderedHearts(SpriteBatch b, DialogueBox box, IHeartsData data)
        {
            var args = new RenderEventArgs<IHeartsData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderedHearts(args));
        }

        internal static void RaiseRenderingImage(SpriteBatch b, DialogueBox box, IImageData data)
        {
            var args = new RenderEventArgs<IImageData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderingImage(args));
        }

        internal static void RaiseRenderedImage(SpriteBatch b, DialogueBox box, IImageData data)
        {
            var args = new RenderEventArgs<IImageData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderedImage(args));
        }

        internal static void RaiseRenderingText(SpriteBatch b, DialogueBox box, ITextData data)
        {
            var args = new RenderEventArgs<ITextData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderingText(args));
        }

        internal static void RaiseRenderedText(SpriteBatch b, DialogueBox box, ITextData data)
        {
            var args = new RenderEventArgs<ITextData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderedText(args));
        }

        internal static void RaiseRenderingDivider(SpriteBatch b, DialogueBox box, IDividerData data)
        {
            var args = new RenderEventArgs<IDividerData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderingDivider(args));
        }

        internal static void RaiseRenderedDivider(SpriteBatch b, DialogueBox box, IDividerData data)
        {
            var args = new RenderEventArgs<IDividerData>(b, box, data);
            ApiConsumers.ForEach(c => c.OnRaiseRenderedDivider(args));
        }
    }
}
