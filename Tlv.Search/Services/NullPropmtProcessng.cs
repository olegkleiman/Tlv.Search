using Tlv.Search.Common;
using Tlv.Search.Models;

namespace Tlv.Search.Services
{
    internal class NullPromptProcessing : IPromptProcessingService
    {
        public Task<PromptContext> CreateContext(string input)
        {
            throw new NotImplementedException();
        }

        public Task<string> FilterKeywords(string input, int excludeFrequency = 25)
        {
            throw new NotImplementedException();
        }
    }
}
