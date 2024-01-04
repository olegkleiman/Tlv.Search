using Ardalis.GuardClauses;
using Azure;
using Azure.AI.OpenAI;
using EmbeddingEngine.Core;
using Microsoft.Extensions.Azure;
using Tlv.Search.Common;

namespace EmbeddingEngine.OpenAI
{
    public class OpenAIEngine : IEmbeddingEngine
    {
        public string? m_providerKey { get; set; }

        public EmbeddingsProviders provider { get; } = EmbeddingsProviders.OPENAI;
        public OpenAIEngine(string providerKey)
        {
            m_providerKey = providerKey;
        }
        public async Task<float[]>? GenerateEmbeddingsAsync(string input)
        {
            try
            {
                var client = new OpenAIClient(m_providerKey, new OpenAIClientOptions());
                //string userMessage = "סכם בבקשה את הטקסט הבא \n: ";

                //ChatCompletionsOptions cco = new()
                //{
                //    DeploymentName = "gpt-3.5-turbo-1106", //,"gpt-4"
                //    Messages =
                //        {
                //            new ChatRequestSystemMessage(@"You are a help assistant that summarized the user input."),
                //            new ChatRequestUserMessage(userMessage + doc.Text)
                //        },
                //    Temperature = (float)0.7,
                //    MaxTokens = 800,
                //    NucleusSamplingFactor = (float)0.95,
                //    FrequencyPenalty = 0,
                //    PresencePenalty = 0,
                //};

                //Response<ChatCompletions> responseWithoutStream = await client.GetChatCompletionsAsync(cco);
                //ChatResponseMessage summaryMessage = responseWithoutStream.Value.Choices[0].Message;
                //string summary = summaryMessage.Content;
                //doc.Summary = summary;

                string? content = input;
                if (string.IsNullOrEmpty(content))
                    return new float[] {};

                EmbeddingsOptions eo = new(deploymentName: "text-embedding-ada-002",
                                            input: new List<string>() { content });
                Response<Embeddings> response = await client.GetEmbeddingsAsync(eo);
                if (response is not null)
                {
                    var items = response.Value.Data;
                    Guard.Against.Zero(items.Count);
                    return items[0].Embedding.ToArray();
                }

                return new float[] { };

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

    }
}