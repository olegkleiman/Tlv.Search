using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
using Microsoft.Extensions.Logging;
using Tlv.Search.Common;
using VectorDb.Core;

namespace Tlv.Search.Services
{
    public class SearchService(IVectorDb _vectorDb,
                                IEmbeddingEngine _embeddingEngine,
                                string _collectionName)
    {
        public IVectorDb vectorDb { get; set; } = _vectorDb;
        public IEmbeddingEngine embeddingEngine { get; set; } = _embeddingEngine;
        public string collectionName { get; set; } = _collectionName;

        public async Task<List<SearchItem>> Search(string prompt,
                                                   ulong limit,
                                                   ILogger? logger)
        {
            Guard.Against.Null(embeddingEngine);
            Guard.Against.Null(vectorDb);

            try
            {
                ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt, "query", logger);
                Guard.Against.NegativeOrZero(promptEmbedding.Length, "promptEmbedding", "0-lenght prompt embedding");

                return await vectorDb.Search(collectionName,
                                            promptEmbedding,
                                            limit: limit);
            }
            catch(Exception)
            {
                throw;
            }
        }

    }
}
