using DialogueDisplayFramework.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley.Menus;
using System;

namespace DialogueDisplayFramework.Api
{
    public class DialogueDisplayApi : IDialogueDisplayApi
    {
        public IManifest ModManifest;

        public DialogueDisplayApi(IManifest mod)
        {
            ModManifest = mod;
        }

        public event EventHandler<IRenderEventArgs<IDialogueDisplayData>> RenderingDialogueBox;

        public event EventHandler<IRenderEventArgs<IDialogueDisplayData>> RenderedDialogueBox;

        public event EventHandler<IRenderEventArgs<IDialogueStringData>> RenderingDialogueString;

        public event EventHandler<IRenderEventArgs<IDialogueStringData>> RenderedDialogueString;

        public event EventHandler<IRenderEventArgs<IPortraitData>> RenderingPortrait;

        public event EventHandler<IRenderEventArgs<IPortraitData>> RenderedPortrait;

        public event EventHandler<IRenderEventArgs<IBaseData>> RenderingJewel;

        public event EventHandler<IRenderEventArgs<IBaseData>> RenderedJewel;

        public event EventHandler<IRenderEventArgs<IBaseData>> RenderingButton;

        public event EventHandler<IRenderEventArgs<IBaseData>> RenderedButton;

        public event EventHandler<IRenderEventArgs<IGiftsData>> RenderingGifts;

        public event EventHandler<IRenderEventArgs<IGiftsData>> RenderedGifts;

        public event EventHandler<IRenderEventArgs<IHeartsData>> RenderingHearts;

        public event EventHandler<IRenderEventArgs<IHeartsData>> RenderedHearts;

        public event EventHandler<IRenderEventArgs<IImageData>> RenderingImage;

        public event EventHandler<IRenderEventArgs<IImageData>> RenderedImage;

        public event EventHandler<IRenderEventArgs<ITextData>> RenderingText;

        public event EventHandler<IRenderEventArgs<ITextData>> RenderedText;

        public event EventHandler<IRenderEventArgs<IDividerData>> RenderingDivider;

        public event EventHandler<IRenderEventArgs<IDividerData>> RenderedDivider;

        public IDialogueDisplayData GetSpeakerDisplayData(string key)
        {
            return DialogueBoxInterface.GetSpecialDisplay(key).GetAdapter();
        }
        public void OnRaiseRenderingDialogueBox(IRenderEventArgs<IDialogueDisplayData> args)
        {
            OnRaiseEvent(RenderingDialogueBox, args);
        }

        public void OnRaiseRenderedDialogueBox(IRenderEventArgs<IDialogueDisplayData> args)
        {
            OnRaiseEvent(RenderedDialogueBox, args);
        }

        public void OnRaiseRenderingDialogueString(IRenderEventArgs<IDialogueStringData> args)
        {
            OnRaiseEvent(RenderingDialogueString, args);
        }

        public void OnRaiseRenderedDialogueString(IRenderEventArgs<IDialogueStringData> args)
        {
            OnRaiseEvent(RenderedDialogueString, args);
        }

        public void OnRaiseRenderingPortrait(IRenderEventArgs<IPortraitData> args)
        {
            OnRaiseEvent(RenderingPortrait, args);
        }

        public void OnRaiseRenderedPortrait(IRenderEventArgs<IPortraitData> args)
        {
            OnRaiseEvent(RenderedPortrait, args);
        }

        public void OnRaiseRenderingJewel(IRenderEventArgs<IBaseData> args)
        {
            OnRaiseEvent(RenderingJewel, args);
        }

        public void OnRaiseRenderedJewel(IRenderEventArgs<IBaseData> args)
        {
            OnRaiseEvent(RenderedJewel, args);
        }

        public void OnRaiseRenderingButton(IRenderEventArgs<IBaseData> args)
        {
            OnRaiseEvent(RenderingButton, args);
        }

        public void OnRaiseRenderedButton(IRenderEventArgs<IBaseData> args)
        {
            OnRaiseEvent(RenderedButton, args);
        }

        public void OnRaiseRenderingGifts(IRenderEventArgs<IGiftsData> args)
        {
            OnRaiseEvent(RenderingGifts, args);
        }

        public void OnRaiseRenderedGifts(IRenderEventArgs<IGiftsData> args)
        {
            OnRaiseEvent(RenderedGifts, args);
        }

        public void OnRaiseRenderingHearts(IRenderEventArgs<IHeartsData> args)
        {
            OnRaiseEvent(RenderingHearts, args);
        }

        public void OnRaiseRenderedHearts(IRenderEventArgs<IHeartsData> args)
        {
            OnRaiseEvent(RenderedHearts, args);
        }

        public void OnRaiseRenderingImage(IRenderEventArgs<IImageData> args)
        {
            OnRaiseEvent(RenderingImage, args);
        }

        public void OnRaiseRenderedImage(IRenderEventArgs<IImageData> args)
        {
            OnRaiseEvent(RenderedImage, args);
        }

        public void OnRaiseRenderingText(IRenderEventArgs<ITextData> args)
        {
            OnRaiseEvent(RenderingText, args);
        }

        public void OnRaiseRenderedText(IRenderEventArgs<ITextData> args)
        {
            OnRaiseEvent(RenderedText, args);
        }

        public void OnRaiseRenderingDivider(IRenderEventArgs<IDividerData> args)
        {
            OnRaiseEvent(RenderingDivider, args);
        }

        public void OnRaiseRenderedDivider(IRenderEventArgs<IDividerData> args)
        {
            OnRaiseEvent(RenderedDivider, args);
        }

        internal void OnRaiseEvent<T>(EventHandler<T> raiseEvent, T args)
        {
            if (raiseEvent is null)
                return;

            try
            {
                raiseEvent.DynamicInvoke(this, args);
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.LogOnce($"{ModManifest.Name} is crashing, please report the following error to them:\n[{ModManifest.Name}] {ex}", LogLevel.Error);
            }
        }
    }
}
