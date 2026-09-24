using System.Collections.Generic;

namespace DialogueDisplayFramework.Data
{
    public class DialogueDisplayData
    {
        private readonly DialogueDisplayDataAdapter _adapter;

        public DialogueDisplayData()
        {
            _adapter = new(this);
        }

        public string CopyFrom { get; set; }
        public string PackName { get; set; }
        public int? XOffset { get; set; }
        public int? YOffset { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public DialogueStringData Dialogue { get; set; }
        public PortraitData Portrait { get; set; }
        public TextData Name { get; set; }
        public BaseData Jewel { get; set; }
        public BaseData Button { get; set; }
        public GiftsData Gifts { get; set; }
        public HeartsData Hearts { get; set; }
        public List<ImageData> Images { get; set; } = new();
        public List<TextData> Texts { get; set; } = new();
        public List<DividerData> Dividers { get; set; } = new();
        public bool Disabled { get; set; }
        public string Condition { get; set; }

        /// <summary>
        /// Fetches the adapter that can be used in other assemblies which map to the <see cref="IDialogueDisplayApi"/> interface.
        /// </summary>
        /// <returns>An adapter that implements <see cref="IDialogueDisplayData"/></returns>
        public DialogueDisplayDataAdapter GetAdapter()
        {
            return _adapter;
        }
    }
}