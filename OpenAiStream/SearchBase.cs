using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
//using Microsoft.SemanticKernel.Connectors.OpenAI;
//using Microsoft.SemanticKernel.Connectors.Qdrant;
//using Microsoft.SemanticKernel.Memory;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tlv.Search.Common;
using VectorDb.Core;

public class SearchBase
{
    protected string? GetConfigValue(string configKey)
    {
        string? value = Environment.GetEnvironmentVariable(configKey);
        Guard.Against.NullOrEmpty(value, configKey, $"Couldn't find '{configKey}' in configuration");

        return value;
    }

    protected async Task<List<SearchItem>> Search(string embeddingsProviderName,
                                                    string embeddingEngineKey,
                                                    string vectorDbProviderKey,
                                                    string collectionName,
                                                    string prompt)
    {
        EmbeddingsProviders embeddingsProvider = (EmbeddingsProviders)Enum.Parse(typeof(EmbeddingsProviders), embeddingsProviderName);
        IEmbeddingEngine? embeddingEngine = EmbeddingEngine.Core.EmbeddingEngine.Create(embeddingsProvider,
                                                                                providerKey: embeddingEngineKey);
        Guard.Against.Null(embeddingEngine);

        ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);

        IVectorDb? vectorDb = VectorDb.Core.VectorDb.Create(VectorDbProviders.QDrant, vectorDbProviderKey);
        Guard.Against.Null(vectorDb);

        return await vectorDb.Search($"{collectionName}_{embeddingsProviderName}", promptEmbedding);
    }


}

