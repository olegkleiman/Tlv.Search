using Azure.AI.OpenAI;
using Catalyst;
using Microsoft.Identity.Client;
using Mosaik.Core;
using StackExchange.Redis;
using System.Data.SqlTypes;
using Tlv.Search.Common;
using Tlv.Search.Models;

namespace Tlv.Search.Services
{
    public class DefaultPromptProcessing : IPromptProcessingService
    {
        private readonly IConnectionMultiplexer? _multiplexer;
        private readonly OpenAIClient?           _openAIClient;

        private PromptContext?                   PromptContext { get; set; }

        public DefaultPromptProcessing(IConnectionMultiplexer? multiplexer,
                                       string openai_key)
        {
            _multiplexer = multiplexer;
            if( !string.IsNullOrEmpty(openai_key) )
                _openAIClient = new OpenAIClient(openai_key, new OpenAIClientOptions());
        }

        public async Task<PromptContext> CreateContext(string prompt)
        {
            PromptContext = new (prompt);
            await FilterKeywords(prompt);
            await DetectGeoLocation(prompt);

            return PromptContext;
        }

        /// <summary>
        /// Excludes all tokens from the input that appears in cache more that N times
        /// </summary>
        /// <param name="input"></param>
        /// <returns>The string with excludes</returns>
        public async Task FilterKeywords(string input, int excludeFrequency = 25)
        {
            if( PromptContext is null )
                return;
            if ( _multiplexer is null )
                return;

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

            PromptContext.FilteredPrompt = 
                string.Join(" ", _input.Split(new char[0], StringSplitOptions.RemoveEmptyEntries).ToList().Select(x => x.Trim()));
        }

        public async Task DetectGeoLocation(string prompt)
        {
            if( PromptContext is null )
                return;
            if( _openAIClient is null )
                return;

            string context = "At which geographical location happens the following sentence. Report only the geographical name. Say 'No' if there is no geographical location detected. Answer in Hebrew: ";
            context += prompt;

            ChatCompletionsOptions cco = new ChatCompletionsOptions()
            {
                Temperature = (float)0.7,
                MaxTokens = 800,
                NucleusSamplingFactor = (float)0.95,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
                DeploymentName = "gpt-4",
                Messages =
                        {
                            new ChatRequestSystemMessage(@"You are a help assistant that analyzes the user input."),
                            new ChatRequestUserMessage(context)
                        },
            };
            var chat = await _openAIClient.GetChatCompletionsAsync(cco);
            ChatResponseMessage responseMessage = chat.Value.Choices[0].Message;
            string content = responseMessage.Content;
            if( content.CompareTo("No") == 0
                || string.IsNullOrEmpty(content))
                PromptContext.GeoCondition = string.Empty;
            else
                PromptContext.GeoCondition = content;
        }

    }
}
