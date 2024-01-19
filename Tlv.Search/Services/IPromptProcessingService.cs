namespace Tlv.Search.Services
{
    public interface IPromptProcessingService
    {
        string FilterKeywords(string input, int excludeFrequency = 20);
    }
}
