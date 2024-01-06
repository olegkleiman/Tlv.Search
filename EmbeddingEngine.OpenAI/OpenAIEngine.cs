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
        public async Task<float[]?> GenerateEmbeddingsAsync(string input)
        {
            try
            {
                var client = new OpenAIClient(m_providerKey, new OpenAIClientOptions());

                string? content = input;
                if (string.IsNullOrEmpty(content))
                    return Array.Empty<float>();

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
                return Array.Empty<float>();
            }
        }

    }
}