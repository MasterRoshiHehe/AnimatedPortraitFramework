namespace DialogueDisplayFramework.Data
{
    public class GiftsData : BaseData, IGiftsData
    {
        public float IconScale { get; set; }
        public bool Inline { get; set; }
        public GiftsData()
        {
            IconScale = 1f;
        }
    }
}
