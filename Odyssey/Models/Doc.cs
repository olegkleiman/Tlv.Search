using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Odyssey.Models
{
    public class Doc
    {
        public int Id { get; set; }
        public string lang { get; set; }
        public string? doc { get; set; }
        public string? title { get; set; }
        public string? url { get; set; }
        public string? source { get; set; }
    }
}
