using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tlv.Search.Common;
using VectorDb.Core;

namespace Tlv.Recall
{
    public class SearchBase
    {
        protected string GetConfigValue(string configKey)
        {
            string? value = Environment.GetEnvironmentVariable(configKey);
            Guard.Against.NullOrEmpty(value, configKey, $"Couldn't find '{configKey}' in configuration");

            return value;
        }

        protected async Task<List<SearchItem>> Search(string embeddingsProviderName,
                                                        string embeddingEngineKey,
                                                        string vectorDbHostUrl,
                                                        string vectorDbProviderKey,
                                                        string collectionName,
                                                        string prompt)
        {
            EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), embeddingsProviderName);
            IEmbeddingEngine? embeddingEngine = EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                                                    providerKey: embeddingEngineKey);
            Guard.Against.Null(embeddingEngine);

            ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);

            IVectorDb? vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant, vectorDbHostUrl, vectorDbProviderKey);
            Guard.Against.Null(vectorDb);

            return await vectorDb.Search($"{collectionName}_{embeddingsProviderName}", promptEmbedding);
        }

#pragma warning disable SKEXP0003, SKEXP0011, SKEXP0020, SKEXP0026, SKEXP0050, SKEXP0052, SKEXP0055
        protected async ValueTask<List<SearchItem>> Search(string apiKey,
                                                           string endpoint,
                                                           string collectionName,
                                                           string prompt)
        {   
            //var embeddingService =
            //    new AzureOpenAITextEmbeddingGenerationService("ada2", endpoint, apiKey);
            ISemanticTextMemory memory = new MemoryBuilder()
                                        //.WithTextEmbeddingGeneration(embeddingService)
                                        //.WithAzureOpenAITextEmbeddingGeneration("ada2", endpoint, apiKey)
                                        .WithOpenAITextEmbeddingGeneration("text-embedding-ada-002", apiKey)
                                        .WithQdrantMemoryStore("http://localhost:6333", 1536)
                                        .Build();
            var collections = await memory.GetCollectionsAsync();
            if (!collections.Contains(collectionName))
                return new List<SearchItem>();

            IAsyncEnumerable<MemoryQueryResult> memories = memory.SearchAsync(collectionName, prompt, limit: 1);

            var q = from res in memories
                    select new SearchItem()
                    {
                        summary = res.Metadata.Text,
                    };
            return q.ToListAsync().Result;
        }
#pragma warning restore SKEXP0003, SKEXP0011, SKEXP0026, SKEXP0050, SKEXP0052, SKEXP0055

    }
}
