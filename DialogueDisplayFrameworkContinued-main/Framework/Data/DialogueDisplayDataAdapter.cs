namespace DialogueDisplayFramework.Data
{
    public class DialogueDisplayDataAdapter : IDialogueDisplayData
    {
        private readonly DialogueDisplayData _data;

        public DialogueDisplayDataAdapter(DialogueDisplayData data)
        {
            _data = data;
        }

        public string CopyFrom
        {
            get => _data.CopyFrom;
            set => _data.CopyFrom = value;
        }

        public string PackName
        {
            get => _data.PackName;
            set => _data.PackName = value;
        }

        public int? XOffset
        {
            get => _data.XOffset;
            set => _data.XOffset = value;
        }

        public int? YOffset
        {
            get => _data.YOffset;
            set => _data.YOffset = value;
        }

        public int? Width
        {
            get => _data.Width;
            set => _data.Width = value;
        }

        public int? Height
        {
            get => _data.Height;
            set => _data.Height = value;
        }

        public IDialogueStringData Dialogue
        {
            get => _data.Dialogue;
            set => _data.Dialogue = (DialogueStringData)value;
        }

        public IPortraitData Portrait
        {
            get => _data.Portrait;
            set => _data.Portrait = (PortraitData)value;
        }

        public ITextData Name
        {
            get => _data.Name;
            set => _data.Name = (TextData)value;
        }

        public IBaseData Jewel
        {
            get => _data.Jewel;
            set => _data.Jewel = (BaseData)value;
        }

        public IBaseData Button
        {
            get => _data.Button;
            set => _data.Button = (BaseData)value;
        }

        public IGiftsData Gifts
        {
            get => _data.Gifts;
            set => _data.Gifts = (GiftsData)value;
        }

        public IHeartsData Hearts
        {
            get => _data.Hearts;
            set => _data.Hearts = (HeartsData)value;
        }

        public bool Disabled
        {
            get => _data.Disabled;
            set => _data.Disabled = value;
        }

        public string Condition
        {
            get => _data.Condition;
            set => _data.Condition = value;
        }
    }
}