using Ardalis.GuardClauses;
using EmbeddingEngine.Core;
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
                                                   ulong limit = 1)
        {
            Guard.Against.Null(embeddingEngine);
            Guard.Against.Null(vectorDb);

            try
            {
                ReadOnlyMemory<float> promptEmbedding = await embeddingEngine.GenerateEmbeddingsAsync(prompt);
                return await vectorDb.Search(collectionName,
                                            promptEmbedding,
                                            limit: limit);
            }
            catch(Exception)
            {
                return new List<SearchItem>();
            }
        }

    }
}
