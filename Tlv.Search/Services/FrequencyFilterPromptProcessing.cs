using Catalyst;
using Mosaik.Core;
using StackExchange.Redis;

namespace Tlv.Search.Services
{
    public class FrequencyFilterPromptProcessing : IPromptProcessingService
    {
        private readonly IConnectionMultiplexer _multiplexer;
        public FrequencyFilterPromptProcessing(IConnectionMultiplexer multiplexer)
        {
            _multiplexer = multiplexer;
        }

        /// <summary>
        /// Excludes all tokens from the input that appears in cache more that N times
        /// </summary>
        /// <param name="input"></param>
        /// <returns>The string with excludes</returns>
        public async Task<string> FilterKeywords(string input, int excludeFrequency = 25)
        {
            string _input = input.Trim();
            List<string> tokens = [];

            IDatabase cache = _multiplexer.GetDatabase();

            Catalyst.Models.Hebrew.Register();
            var nlp = await Pipeline.ForAsync(Language.Hebrew);
            var doc = new Document(_input, Language.Hebrew);
            nlp.ProcessSingle(doc);

            PartOfSpeech[] specialTokens = [PartOfSpeech.PUNCT,
                PartOfSpeech.SYM,
                PartOfSpeech.NONE];

            foreach (var tokenData in doc.TokensData[0])
            {
                var token = input.Substring(tokenData.LowerBound,
                                             tokenData.UpperBound - tokenData.LowerBound + 1);

                if (specialTokens.Contains(tokenData.Tag))
                    _input = _input.Replace(token, string.Empty);
                else
                {
                    // if key does not exists in Redis - StringGet() safely returns 0
                    int count = (int)cache.StringGet(token); // explicit cast
                    if (count > excludeFrequency)
                    {
                        _input = _input.Replace(token, string.Empty);
                    }
                }
            }

            return string.Join(" ", _input.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).ToList().Select(x => x.Trim()));
        }
    }
}
