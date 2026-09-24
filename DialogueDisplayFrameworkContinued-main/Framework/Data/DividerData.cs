namespace DialogueDisplayFramework.Data
{
    public class DividerData : BaseData, IDividerData
    {
        public DividerData()
        {
            ID = DataHelpers.MISSING_ID_STR;
        }

        public string ID { get; set; }
        public bool Horizontal { get; set; }
        public bool Small { get; set; }
        public string Color { get; set; }
        public bool TopConnector { get; set; } = true;
        public bool BottomConnector { get; set; } = true;
    }
}
