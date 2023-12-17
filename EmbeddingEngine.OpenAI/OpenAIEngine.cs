using Ardalis.GuardClauses;
using Azure;
using Azure.AI.OpenAI;
using EmbeddingEngine.Core;
using Microsoft.Extensions.Azure;
using Tlv.Search.Common;

namespace EmbeddingEngine.OpenAI
{
    public class OpenAIEngine(string providerKey) : IEmbeddingEngine
    {
        public string? m_providerKey { get; set; } = providerKey;

        public async Task<Single[]> Embed(Doc doc)
        {
            try
            {
                string content = doc.Content;
                var client = new OpenAIClient(m_providerKey, new OpenAIClientOptions());
                EmbeddingsOptions eo = new(deploymentName: "text-embedding-ada-002",
                                            input: [content]);
                Response<Embeddings> response = await client.GetEmbeddingsAsync(eo);
                if (response is not null)
                {
                    var items = response.Value.Data;
                    Guard.Against.Zero(items.Count);
                    return items[0].Embedding.ToArray();
                }

                return [];

            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
