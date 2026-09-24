using StardewValley.BellsAndWhistles;
using System;

namespace DialogueDisplayFramework.Data
{
    public class TextData : BaseData, ITextData
    {
        private bool _isSpeakerDisplayName = false;

        public TextData()
        {
            ID = DataHelpers.MISSING_ID_STR;
            ScrollType = -1;
            Alignment = SpriteText.ScrollTextAlignment.Center;
        }

        public string ID { get; set; }
        public string Color { get; set; }
        public string Text { get; set; }
        public bool Junimo { get; set; }
        public bool Scroll { get; set; }
        public string PlaceholderText { get; set; }
        public int ScrollType { get; set; }
        public SpriteText.ScrollTextAlignment Alignment { get; set; }

        [Obsolete("Use Alignment property instead.")]
        public bool Centered { get; set; }

        public bool IsSpeakerDisplayName()
        {
            return _isSpeakerDisplayName;
        }

        internal void MarkAsSpeakerDisplayName()
        {
            _isSpeakerDisplayName = true;
        }
    }
}
