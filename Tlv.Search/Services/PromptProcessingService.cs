using StackExchange.Redis;

namespace Tlv.Search.Services
{
    public class PromptProcessingService : IPromptProcessingService
    {
        private readonly IConnectionMultiplexer _multiplexer;
        public PromptProcessingService(IConnectionMultiplexer multiplexer)
        {
            _multiplexer = multiplexer;
        }

        /// <summary>
        /// Excludes all keywords from the input that appears in input parameter more that N times
        /// </summary>
        /// <param name="input"></param>
        /// <returns>The string with excludes</returns>
        public string FilterKeywords(string input, int excludeFrequency = 20)
        {
            string _input = input.Trim();
            List<string> fromKeyWords = ["הנחה", "בארנונה"];

            //var endpoint = _multiplexer.GetEndPoints().First();
            //var server = _multiplexer.GetServer(endpoint);
            //var keys = server.Keys(pattern: "*");
            IDatabase cache = _multiplexer.GetDatabase();

            foreach (var keyword in fromKeyWords)
            {
                // if key does not exists in Redis - StringGet() safely returns 0
                int count = (int)cache.StringGet(keyword); // explicit cast
                if (count > excludeFrequency)
                    _input = input.Replace(keyword, string.Empty);
            }

            return _input;
        }
    }
}
