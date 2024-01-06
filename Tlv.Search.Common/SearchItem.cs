﻿namespace Tlv.Search.Common
{
    public class SearchItem
    {
        public ulong id { get; set;  }
        public string? summary { get; set; }
        public string? title { get; set; }
        public string? imageUrl { get; set; }
        public string? url { get; set; }
        public double similarity { get; set; }
    }
}
