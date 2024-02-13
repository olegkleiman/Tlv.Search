using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tlv.Search.Common
{
    public class PromptContext
    {
        public PromptContext(string prompt)
        {
            Prompt = prompt;
        }

        public string? Prompt { get; }
        public string? GeoCondition { get; set; }
        public string? FilteredPrompt { get; set; }
    }
}
