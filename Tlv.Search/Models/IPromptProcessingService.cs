using Tlv.Search.Common;

namespace Tlv.Search.Models
{
    public interface IPromptProcessingService
    {
        Task<PromptContext> CreateContext(string prompt);
    }
}
