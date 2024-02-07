using Ardalis.GuardClauses;
using Azure;
using Azure.AI.OpenAI;
using EmbeddingEngine.Core;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Tlv.Search.Common;

namespace EmbeddingEngine.OpenAI
{
    public class OpenAIEngine : IEmbeddingEngine
    {
        public string m_providerKey { get; set; }
        public string m_endpoint { get; set; }
        public string m_modelName { get; set; }

        public EmbeddingsProviders provider { get; } = EmbeddingsProviders.OPENAI;
        public OpenAIEngine(string providerKey,
                            string endpoint, 
                            string modelName)
        {
            m_providerKey = providerKey;
            m_endpoint = endpoint;
            m_modelName = modelName;
        }

        public string ModelName
        {
            get
            {
                return m_modelName;
            }
        }

        public async Task<T?> GenerateEmbeddingsAsync<T>(string input)
        {
            try
            {
                var client = new OpenAIClient(m_providerKey, new OpenAIClientOptions());

                string? content = input;
                if (string.IsNullOrEmpty(content))
                    return default;

                EmbeddingsOptions eo = new(deploymentName: m_modelName,
                                            input: new List<string>() { content });
                Response<Embeddings> response = await client.GetEmbeddingsAsync(eo);
                if (response is not null)
                {
                    var items = response.Value.Data;
                    Guard.Against.Zero(items.Count);
                    return (T)Convert.ChangeType(items[0].Embedding.ToArray(), typeof(T));
                }

                return default;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return default;
            }
        }

        public async Task<float[]?> GenerateEmbeddingsAsync(string input, 
                                                            string representation,
                                                            ILogger? logger)
        {
            try
            {
                var client = new OpenAIClient(m_providerKey, new OpenAIClientOptions());

                string? content = input;
                if (string.IsNullOrEmpty(content))
                    return Array.Empty<float>();

                EmbeddingsOptions eo = new(deploymentName: m_modelName,
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
                logger?.LogError(ex.Message);
                return Array.Empty<float>();
            }
        }

    }
}