namespace Tlv.Search.models
{
    public class SearchItem
    {
        public ulong id { get; set;  }
        public string? title { get; set; }
        public string? imageUrl { get; set; }
        public string? url { get; set; }
        public float similarity { get; set; }
    }
}
