namespace DialogueDisplayFramework.Data
{
    public class HeartsData : BaseData, IHeartsData
    {
        public int HeartsPerRow { get; set; }
        public bool ShowEmptyHearts { get; set; }
        public bool ShowPartialhearts { get; set; }
        public bool Centered { get; set; }

        public HeartsData()
        {
            HeartsPerRow = 14;
            ShowEmptyHearts = true;
            ShowPartialhearts = true;
        }
    }
}
