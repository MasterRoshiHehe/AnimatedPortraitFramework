using StardewValley.BellsAndWhistles;

namespace DialogueDisplayFramework.Data
{
    public class DialogueStringData : BaseData, IDialogueStringData
    {
        public string Color { get; set; }
        public SpriteText.ScrollTextAlignment Alignment { get; set; }
    }
}
