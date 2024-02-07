using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tlv.Search.Services
{
    internal class NullPropmtProcessng : IPromptProcessingService
    {
        public async Task<string> FilterKeywords(string input, int excludeFrequency = 25)
        {
            return input;
        }
    }
}
