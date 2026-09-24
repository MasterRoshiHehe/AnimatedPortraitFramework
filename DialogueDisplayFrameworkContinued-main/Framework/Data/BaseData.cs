namespace DialogueDisplayFramework.Data
{
    public class BaseData : IBaseData
    {
        public int XOffset { get; set; }
        public int YOffset { get; set; }
        public bool Right { get; set; }
        public bool Bottom { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float Alpha { get; set; }
        public float Scale { get; set; }
        public float LayerDepth { get; set; }
        public bool Disabled { get; set; }

        public BaseData()
        {
            Width = -1;
            Alpha = 1;
            Scale = 4;
            LayerDepth = 0.88f;
        }
    }
}
