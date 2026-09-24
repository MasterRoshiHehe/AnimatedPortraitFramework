using Microsoft.Xna.Framework.Graphics;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using System;

namespace DialogueDisplayFramework
{
    public interface IDialogueDisplayApi
    {
        /*********
         * Render events
         *********/

        /// <summary>Raised before any of the portrait box rendering begins, but after the dialogue box is created and rendered.</summary>
        public event EventHandler<IRenderEventArgs<IDialogueDisplayData>> RenderingDialogueBox;

        /// <summary>Raised after the dialogue portrait box is fully rendered.</summary>
        public event EventHandler<IRenderEventArgs<IDialogueDisplayData>> RenderedDialogueBox;

        /// <summary>Raised before the dialogue string is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IDialogueStringData>> RenderingDialogueString;

        /// <summary>Raised after the dialogue string rendered.</summary>
        public event EventHandler<IRenderEventArgs<IDialogueStringData>> RenderedDialogueString;

        /// <summary>Raised before the portrait is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IPortraitData>> RenderingPortrait;

        /// <summary>Raised after the portrait is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IPortraitData>> RenderedPortrait;

        /// <summary>Raised before the friendship jewel is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IBaseData>> RenderingJewel;

        /// <summary>Raised after the friendship jewel is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IBaseData>> RenderedJewel;

        /// <summary>Raised before the continue button is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IBaseData>> RenderingButton;

        /// <summary>Raised after the continue button is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IBaseData>> RenderedButton;

        /// <summary>Raised before the gifts component is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IGiftsData>> RenderingGifts;

        /// <summary>Raised after the gifts component is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IGiftsData>> RenderedGifts;

        /// <summary>Raised before the hearts are rendered.</summary>
        public event EventHandler<IRenderEventArgs<IHeartsData>> RenderingHearts;

        /// <summary>Raised after the hearts are rendered.</summary>
        public event EventHandler<IRenderEventArgs<IHeartsData>> RenderedHearts;

        /// <summary>Raised before each image is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IImageData>> RenderingImage;

        /// <summary>Raised after each image is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IImageData>> RenderedImage;

        /// <summary>Raised before each text is rendered, including the character's name.</summary>
        public event EventHandler<IRenderEventArgs<ITextData>> RenderingText;

        /// <summary>Raised after each text is rendered, including the character's name.</summary>
        public event EventHandler<IRenderEventArgs<ITextData>> RenderedText;

        /// <summary>Raised before each divider is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IDividerData>> RenderingDivider;

        /// <summary>Raised after each divider is rendered.</summary>
        public event EventHandler<IRenderEventArgs<IDividerData>> RenderedDivider;

        /*********
         * Methods
         *********/

        /// <summary>
        /// Fetches the relevent data used when rendering the speaker's dialogue box.
        /// </summary>
        /// <param name="key">The dictionary key where the data is stored.</param>
        /// <returns>A display data object.</returns>
        public IDialogueDisplayData GetSpeakerDisplayData(string key);
    }

    public interface IRenderEventArgs<T>
    {
        /// <summary>The spritebatch used for rendering.</summary>
        public SpriteBatch SpriteBatch { get; }

        /// <summary>The current dialogue box being rendered.</summary>
        public DialogueBox DialogueBox { get; }

        /// <summary>Relevent display data for rendering.</summary>
        public T Data { get; }
    }

    public interface IDialogueDisplayData
    {
        /// <summary>Key to a data entry used to fill in empty field values.</summary>
        public string CopyFrom { get; set; }

        /// <summary>Horizontal offset of the dialogue box, in pixels.</summary>
        /// <remarks>** The UI is typically scaled up by four, so a displayed pixel is actually 4 pixels wide.</remarks>
        public int? XOffset { get; set; }

        /// <summary>Vertical offset of the dialogue box, in pixels.</summary>
        /// <remarks>** The UI is typically scaled up by four, so a displayed pixel is actually 4 pixels wide.</remarks>
        public int? YOffset { get; set; }

        /// <summary>Width of the dialogue box, in pixels.</summary>
        /// <remarks>** The UI is typically scaled up by four, so a displayed pixel is actually 4 pixels wide.</remarks>
        public int? Width { get; set; }

        /// <summary>Height of the dialogue box, in pixels.</summary>
        /// <remarks>** The UI is typically scaled up by four, so a displayed pixel is actually 4 pixels wide.</remarks>
        public int? Height { get; set; }

        /// <summary>Dialogue string display customizations.</summary>
        public IDialogueStringData Dialogue { get; set; }

        /// <summary>Portrait display customizations.</summary>
        public IPortraitData Portrait { get; set; }

        /// <summary>Name display customizations.</summary>
        public ITextData Name { get; set; }

        /// <summary>Friendship jewel display customizations.</summary>
        public IBaseData Jewel { get; set; }

        /// <summary>Continue button display customizations.</summary>
        public IBaseData Button { get; set; }

        /// <summary>Gifts display customizations.</summary>
        public IGiftsData Gifts { get; set; }

        /// <summary>Hearts display customizations.</summary>
        public IHeartsData Hearts { get; set; }

        // /// <summary>Images to be displayed.</summary>
        // public List<ImageData> Images { get; set; }

        // /// <summary>Text to be displayed.</summary>
        //public List<TextData> Texts { get; set; }

        // /// <summary>Dividers to be displayed.</summary>
        // public List<DividerData> Dividers { get; set; }

        /// <summary>Prevents any of the customizations to be rendered.</summary>
        public bool Disabled { get; set; }

        /// <summary>GSQ condition to toggle the patch.</summary>
        public string Condition { get; set; }
    }

    public interface IBaseData
    {
        /// <summary>Horizontal offset of the element, in pixels.</summary>
        /// <remarks>** The UI is typically scaled up by four, so a displayed pixel is actually 4 pixels wide.</remarks>
        public int XOffset { get; set; }

        /// <summary>Vertical offset of the element, in pixels.</summary>
        /// <remarks>** The UI is typically scaled up by four, so a displayed pixel is actually 4 pixels wide.</remarks>
        public int YOffset { get; set; }

        /// <summary>Whether the offset should be anchored to the right of the screen.</summary>
        public bool Right { get; set; }

        /// <summary>Whether the offset should be anchored to the bottom of the screen.</summary>
        public bool Bottom { get; set; }

        /// <summary>Width of the element, in pixels.</summary>
        /// <remarks>** The UI is typically scaled up by four, so a displayed pixel is actually 4 pixels wide.</remarks>
        public int Width { get; set; }

        /// <summary>Height of the element, in pixels.</summary>
        /// <remarks>** The UI is typically scaled up by four, so a displayed pixel is actually 4 pixels wide.</remarks>
        public int Height { get; set; }

        /// <summary>The opacity of the element.</summary>
        public float Alpha { get; set; }

        /// <summary>The scale of the element.</summary>
        public float Scale { get; set; }

        /// <summary>The layer depth of the element for mods that enable this feature.</summary>
        public float LayerDepth { get; set; }

        /// <summary>Prevents any of the customizations to be rendered.</summary>
        public bool Disabled { get; set; }
    }

    public interface IDialogueStringData : IBaseData
    {
        /// <summary>The text color.</summary>
        /// <remarks>** Supports color names, hex and RGB formats.</remarks>
        public string Color { get; set; }

        /// <summary>The text alignment.</summary>
        public SpriteText.ScrollTextAlignment Alignment { get; set; }
    }

    public interface IPortraitData : IBaseData
    {
        /// <summary>The asset name of the texture to render.</summary>
        public string TexturePath { get; set; }

        /// <summary>Horizontal position of the source rectangle.</summary>
        public int X { get; set; }

        /// <summary>Vertical position of the source rectangle.</summary>
        public int Y { get; set; }

        /// <summary>The width of the source rectangle.</summary>
        public int W { get; set; }

        /// <summary>The height of the source rectangle.</summary>
        public int H { get; set; }

        /// <summary>Whether the texture should be used as a portrait tilesheet.</summary>
        public bool TileSheet { get; set; }
    }

    public interface IGiftsData : IBaseData
    {
        /// <summary>Scale of the gift icon (default 1).</summary>
        public float IconScale { get; set; }

        /// <summary>Whether to place the check boxes next to eachother.</summary>
        public bool Inline { get; set; }
    }

    public interface IHeartsData : IBaseData
    {
        /// <summary>The number of hearts rendered per row.</summary>
        /// <remarks>** The maximum heart count is 14.</remarks>
        public int HeartsPerRow { get; set; }

        /// <summary>Whether to render empty hearts.</summary>
        public bool ShowEmptyHearts { get; set; }

        /// <summary>Whether to render partial hearts.</summary>
        public bool ShowPartialhearts { get; set; }

        /// <summary>Whether to center the element on the horizontal offset.</summary>
        public bool Centered { get; set; }
    }

    public interface IImageData : IBaseData
    {
        /// <summary>Unique string identifier.</summary>
        public string ID { get; set; }

        /// <summary>The asset name of the texture to render.</summary>
        public string TexturePath { get; set; }

        /// <summary>Horizontal position of the source rectangle.</summary>
        public int X { get; set; }

        /// <summary>Vertical position of the source rectangle.</summary>
        public int Y { get; set; }

        /// <summary>The width of the source rectangle.</summary>
        public int W { get; set; }

        /// <summary>The height of the source rectangle.</summary>
        public int H { get; set; }
    }

    public interface ITextData : IBaseData
    {
        /// <summary>Unique string identifier.</summary>
        public string ID { get; set; }

        /// <summary>The text color.</summary>
        /// <remarks>** Supports color names, hex and RGB formats.</remarks>
        public string Color { get; set; }

        /// <summary>The displayed text.</summary>
        public string Text { get; set; }

        /// <summary>Whether to render the text in Junimo speach.</summary>
        public bool Junimo { get; set; }

        /// <summary>Whether to render a scroll behind the text.</summary>
        public bool Scroll { get; set; }

        /// <summary>The scroll type to render.</summary>
        /// <remarks>Supported values:<br/>
        /// * -1 = No scroll;<br/>
        /// * 0 = Sizeable scroll;<br/>
        /// * 1 = Speech bubble;<br/>
        /// * 2 = Cave depth plate;<br/>
        /// * 3 = Mastery text plate
        /// </remarks>
        public int ScrollType { get; set; }

        /// <summary>Affects the width of the scroll</summary>
        public string PlaceholderText { get; set; }

        /// <summary>The text alignment.</summary>
        public SpriteText.ScrollTextAlignment Alignment { get; set; }

        /// <summary>
        /// Determines whether this is the text component containing the speaker's name.
        /// </summary>
        /// <returns>True or false</returns>
        public bool IsSpeakerDisplayName();
    }

    public interface IDividerData : IBaseData
    {
        /// <summary>Unique string identifier.</summary>
        public string ID { get; set; }

        /// <summary>Whether to render as a horizontal divider.</summary>
        public bool Horizontal { get; set; }

        /// <summary>Rendered a thinner divider.</summary>
        public bool Small { get; set; }

        /// <summary>The color hue to apply.</summary>
        /// <remarks>** Supports color names, hex and RGB formats.</remarks>
        public string Color { get; set; }

        /// <summary>Whther to render the top connector when using vertical dividers.</summary>
        public bool TopConnector { get; set; }

        /// <summary>Whther to render the bottom connector when using vertical dividers.</summary>
        public bool BottomConnector { get; set; }
    }
}
