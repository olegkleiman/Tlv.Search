namespace Tlv.Search.Services
{
    public interface IPromptProcessingService
    {
        Task<string> FilterKeywords(string input, int excludeFrequency = 25);
    }
}
