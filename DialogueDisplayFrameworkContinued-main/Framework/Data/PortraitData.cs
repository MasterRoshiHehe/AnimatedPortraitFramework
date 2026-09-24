namespace DialogueDisplayFramework.Data
{
    public class PortraitData : BaseData, IPortraitData
    {
        public string TexturePath { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public bool TileSheet { get; set; }

        public PortraitData()
        {
            X = -1;
            Y = -1;
            W = 64;
            H = 64;
            TileSheet = true;
        }
    }
}
