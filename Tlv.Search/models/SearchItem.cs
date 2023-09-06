using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tlv.Search.models
{
    public class SearchItem
    {
        public int id { get; set;  }
        public string? title { get; set; }
        public string? doc { get; set; }
        public string? url { get; set; }
        public double distance { get; set; }
    }
}
